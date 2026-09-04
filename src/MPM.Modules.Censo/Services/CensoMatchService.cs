using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MPM.Modules.Censo.Data;
using MPM.Modules.Censo.Models;

namespace MPM.Modules.Censo.Services;

/// <summary>
/// Match de capacidades TIVIT contra Census (D7.9/D7.10/D7.12, CEN-R001..R010): requisitos
/// (body > análisis comercial), expansión catálogo-first, consultas paralelas con semáforo
/// máx 8, cache por tecnología+país (TTL 24 h), dedup por email/corporateId y scoring de
/// cobertura con bonus por país de ejecución cuando el filtro está OFF.
/// Resultado persistido en <c>censo_match</c> (GET lee solo local).
/// </summary>
public class CensoMatchService(
    CensoHandler handler,
    CensusClient censusClient,
    CensoExpansionService expansionService,
    ILogger<CensoMatchService> logger)
{
    // Guard in-process (CEN_003): un solo POST por licitación a la vez (ventana ~3 s).
    private static readonly ConcurrentDictionary<long, byte> EnCurso = new();
    // CEN-R005: semáforo máx 8 consultas Census concurrentes (benchmark: 16 = 3.044 ms).
    private static readonly SemaphoreSlim SemaphoreCensus = new(8, 8);

    public class SinRequisitosException(string message) : Exception(message);
    public class SinAnalisisException(string message) : Exception(message);
    public class CensusInaccesibleException(string message) : Exception(message);
    public class MatchEnCursoException(string message) : Exception(message);

    /// <summary>Ejecuta el match completo y persiste el resultado en censo_match.</summary>
    public virtual async Task<CensoMatchResultDto> EjecutarMatchAsync(
        long licitacionId, string userId, CensoMatchRequest? request, CancellationToken ct = default)
    {
        if (!EnCurso.TryAdd(licitacionId, 0))
            throw new MatchEnCursoException("Ya hay un match en curso para esta licitación");
        try
        {
            // 1. Requisitos: body > análisis comercial (CEN_004/CEN_001 si no hay).
            var (tecnologias, certificaciones) = await ResolverRequisitosAsync(licitacionId, request, ct);

            // 2. País: body > preferencias del usuario > defaults (CEN-R010, D7.12).
            var (filtrarPais, pais) = await ResolverPaisAsync(userId, request, ct);

            // 3. Expansión de tecnologías (catálogo first, IA cacheada — CEN-R004).
            var tecnologiasExpandidas = new List<string>();
            try
            {
                foreach (var concepto in tecnologias)
                {
                    var (expandidas, fuente) = await expansionService.ExpandirAsync(concepto, ct);
                    foreach (var t in expandidas)
                        if (!tecnologiasExpandidas.Contains(t))
                            tecnologiasExpandidas.Add(t);
                    if (fuente == "ia")
                        logger.LogInformation("Expansión IA cacheada para concepto '{Concepto}' → {Tecnologias}",
                            concepto, string.Join(", ", expandidas));
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // CEN_002: el refresco lazy del catálogo (o la IA) tocó a Census y falló.
                throw new CensusInaccesibleException($"Census inalcanzable durante la expansión: {ex.Message}");
            }

            // Conceptos a consultar: tecnologías expandidas + certificaciones (clave cert:...).
            var conceptos = new List<ConceptoBusqueda>();
            foreach (var t in tecnologiasExpandidas)
                conceptos.Add(new ConceptoBusqueda(t, t, EsCertificacion: false));
            foreach (var c in certificaciones)
                conceptos.Add(new ConceptoBusqueda($"cert:{c}", c, EsCertificacion: true));

            if (conceptos.Count == 0)
                throw new SinRequisitosException("No hay tecnologías ni certificaciones para consultar");

            // 4. Consultas paralelas (semáforo 8) con cache 24 h (CEN-R005/R006).
            var consultas = 0;
            var cacheUsadas = 0;
            var paisCache = filtrarPais ? pais : "";
            var resultados = await Task.WhenAll(conceptos.Select(async concepto =>
            {
                Interlocked.Increment(ref consultas);

                var frescos = await handler.CachePersonasFrescoAsync(concepto.Clave, paisCache, ct);
                if (frescos != null)
                {
                    Interlocked.Increment(ref cacheUsadas);
                    return new ResultadoConcepto(concepto, frescos);
                }

                await SemaphoreCensus.WaitAsync(ct);
                try
                {
                    List<JsonElement> personas;
                    try
                    {
                        personas = concepto.EsCertificacion
                            ? await censusClient.GetUsersByCertificationAsync(concepto.Termino, filtrarPais ? pais : null, ct)
                            : await censusClient.GetUsersByTechnologyAsync(concepto.Termino, filtrarPais ? pais : null, ct);
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                    {
                        // CEN_002: fallo de red/auth persistente (tras retry 401 interno).
                        throw new CensusInaccesibleException(
                            $"Census inalcanzable al consultar '{concepto.Termino}': {ex.Message}");
                    }

                    await handler.CachePersonasUpsertAsync(concepto.Clave, paisCache, personas, ct);
                    return new ResultadoConcepto(concepto, personas);
                }
                finally
                {
                    SemaphoreCensus.Release();
                }
            }));

            // 5. Dedup por email/corporateId + cobertura (CEN-R001/R007) + bonus país (CEN-R009).
            var totalRequeridos = tecnologiasExpandidas.Count + certificaciones.Count;
            var personas = MergeYScoring(resultados, totalRequeridos, filtrarPais, pais);

            // 6. Resultado + persistencia (GET lee solo local).
            var resultado = new CensoMatchResultDto
            {
                EjecutadoEn = DateTime.UtcNow,
                Consultas = consultas,
                CacheUsadas = cacheUsadas,
                TecnologiasExpandidas = tecnologiasExpandidas,
                Personas = personas,
                Resumen = new CensoResumenDto
                {
                    TotalPersonas = personas.Count,
                    MaxCobertura = personas.Count > 0 ? personas.Max(p => p.Cobertura) : 0,
                    // >= 70% del total requerido (CEN-R001: cobertura parcial válida).
                    PersonasConCoberturaAlta = totalRequeridos > 0
                        ? personas.Count(p => p.Cobertura * 10 >= totalRequeridos * 7)
                        : 0,
                },
            };

            await handler.MatchGuardarAsync(licitacionId, JsonSerializer.Serialize(resultado), ct);
            logger.LogInformation("Match completado para licitación {LicitacionId}: {Consultas} consultas, {Cache} de cache, {Personas} personas únicas",
                licitacionId, consultas, cacheUsadas, personas.Count);
            return resultado;
        }
        finally
        {
            EnCurso.TryRemove(licitacionId, out _);
        }
    }

    /// <summary>Último match persistido de la licitación (null si nunca se ejecutó).</summary>
    public virtual async Task<CensoMatchResultDto?> ObtenerMatchAsync(long licitacionId, CancellationToken ct = default)
    {
        var json = await handler.MatchObtenerAsync(licitacionId, ct);
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize<CensoMatchResultDto>(json);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Resuelve tecnologías/certificaciones: body con contenido > análisis comercial
    /// (solo certificaciones_requeridas; tecnologías vacías) > CEN_004 / CEN_001.
    /// </summary>
    private async Task<(List<string> Tecnologias, List<string> Certificaciones)> ResolverRequisitosAsync(
        long licitacionId, CensoMatchRequest? request, CancellationToken ct)
    {
        if (request is { Tecnologias.Count: > 0 } || request is { Certificaciones.Count: > 0 })
        {
            return (request!.Tecnologias ?? new List<string>(), request!.Certificaciones ?? new List<string>());
        }

        // Sin body (o body con listas vacías) → requisitos del último análisis completado.
        var analisis = await handler.AnalisisRequisitosAsync(licitacionId, ct);
        if (!analisis.TieneAnalisisCompletado)
            throw new SinAnalisisException("No hay un análisis comercial completado para esta licitación");

        if (analisis.Certificaciones.Count == 0 && analisis.Tecnologias.Count == 0)
            throw new SinRequisitosException("La licitación no tiene requisitos ni tecnologías extraíbles para el match");

        return (analisis.Tecnologias, analisis.Certificaciones);
    }

    /// <summary>Precedencia del país (CEN-R010): body del match > preferencias > defaults.</summary>
    private async Task<(bool FiltrarPais, string Pais)> ResolverPaisAsync(
        string userId, CensoMatchRequest? request, CancellationToken ct)
    {
        var prefs = await handler.PreferenciasObtenerAsync(userId, ct);
        var filtrar = request?.FiltrarPais ?? prefs?.FiltrarPais ?? false;
        var pais = request?.Pais ?? prefs?.Pais ?? "Chile";
        if (string.IsNullOrWhiteSpace(pais)) pais = "Chile";
        return (filtrar, pais);
    }

    /// <summary>Dedup por email (minúsculas) / corporateId + cobertura + bonus país de ejecución.</summary>
    private List<CensoPersonaDto> MergeYScoring(
        IEnumerable<ResultadoConcepto> resultados, int totalRequeridos, bool filtrarPais, string pais)
    {
        var personasDict = new Dictionary<string, CensoPersonaDto>(StringComparer.Ordinal);

        foreach (var resultado in resultados)
        {
            foreach (var persona in resultado.Personas)
            {
                var email = GetStr(persona, "userEmail") ?? "";
                var corporateId = GetStr(persona, "corporateId") ?? "";
                var key = !string.IsNullOrWhiteSpace(email)
                    ? email.ToLowerInvariant()
                    : corporateId.ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(key)) continue;

                if (!personasDict.TryGetValue(key, out var dto))
                {
                    dto = new CensoPersonaDto
                    {
                        Nombre = GetStr(persona, "userName") ?? "",
                        Email = email,
                        CorporateId = corporateId,
                        Pais = GetStr(persona, "workCountry") ?? "",
                        Cargo = GetStr(persona, "functionFullName") ?? "",
                        TotalRequeridos = totalRequeridos,
                    };
                    personasDict[key] = dto;
                }

                if (resultado.Concepto.EsCertificacion)
                {
                    var certInfo = CertificacionCoincidente(persona, resultado.Concepto.Termino);
                    var certNombre = certInfo?.Nombre ?? resultado.Concepto.Termino;
                    var fileId = certInfo?.FileId;

                    if (!dto.Certificaciones.Contains(certNombre))
                        dto.Certificaciones.Add(certNombre);

                    if (!dto.CertificacionesDetalle.Any(cd => cd.Nombre.Equals(certNombre, StringComparison.OrdinalIgnoreCase)))
                    {
                        dto.CertificacionesDetalle.Add(new CensoPersonaCertificacionDto
                        {
                            Nombre = certNombre,
                            FileId = fileId
                        });
                    }
                }
                else
                {
                    var techInfo = TecnologiaCoincidente(persona, resultado.Concepto.Termino);
                    if (techInfo != null)
                    {
                        var nombreReal = techInfo.Value.Nombre;
                        var nivel = techInfo.Value.Nivel;
                        if (!dto.Skills.Contains(nombreReal))
                            dto.Skills.Add(nombreReal);

                        if (!dto.SkillsDetalle.Any(sd => sd.Nombre.Equals(nombreReal, StringComparison.OrdinalIgnoreCase)))
                        {
                            dto.SkillsDetalle.Add(new CensoPersonaSkillDto
                            {
                                Nombre = nombreReal,
                                Nivel = nivel,
                                NivelTexto = ObtenerTextoNivel(nivel),
                            });
                        }
                    }
                }
            }
        }

        // Scoring: cobertura (skills + certificaciones únicos) y bonus por país de ejecución
        // (D7.12/CEN-R009): con filtro OFF, las personas del país de ejecución rankean arriba
        // sin excluir a nadie; el bonus no altera la cobertura mostrada.
        return personasDict.Values
            .Select(p =>
            {
                p.Cobertura = p.Skills.Count + p.Certificaciones.Count;
                var bonus = (!filtrarPais && !string.IsNullOrWhiteSpace(pais) &&
                             p.Pais.Equals(pais, StringComparison.OrdinalIgnoreCase)) ? 1 : 0;
                return (Persona: p, Orden: p.Cobertura + bonus);
            })
            .OrderByDescending(x => x.Orden)
            .ThenByDescending(x => x.Persona.Cobertura)
            .Select(x => x.Persona)
            .ToList();
    }

    public static string? ObtenerTextoNivel(int? nivel) => nivel switch
    {
        1 => "Básico",
        2 => "Intermedio",
        3 => "Avanzado",
        4 => "Experto",
        _ => null,
    };

    /// <summary>¿La persona tiene la certificación consultada? Devuelve nombre y fileId.</summary>
    private static (string Nombre, string? FileId)? CertificacionCoincidente(JsonElement persona, string termino)
    {
        if (!persona.TryGetProperty("certifications", out var certs) || certs.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var c in certs.EnumerateArray())
        {
            var nombre = (c.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String) ? n.GetString() : null;
            if (string.IsNullOrWhiteSpace(nombre)) continue;
            if (nombre.Contains(termino, StringComparison.OrdinalIgnoreCase) || termino.Contains(nombre, StringComparison.OrdinalIgnoreCase))
            {
                var fileId = GetStr(c, "fileId") ?? GetStr(c, "fileID") ?? GetStr(c, "id");
                return (nombre, fileId);
            }
        }
        return null;
    }

    /// <summary>¿La persona tiene la tecnología consultada? Devuelve el nombre canónico y nivel (1..4).</summary>
    private static (string Nombre, int? Nivel)? TecnologiaCoincidente(JsonElement persona, string termino)
    {
        if (!persona.TryGetProperty("technologies", out var tecnologias) ||
            tecnologias.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var t in tecnologias.EnumerateArray())
        {
            var nombre = t.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
            if (string.IsNullOrWhiteSpace(nombre)) continue;
            if (nombre.Contains(termino, StringComparison.OrdinalIgnoreCase) ||
                termino.Contains(nombre, StringComparison.OrdinalIgnoreCase))
            {
                int? nivel = null;
                if (t.TryGetProperty("levelSkill", out var lvl) && lvl.ValueKind == JsonValueKind.Number)
                    nivel = lvl.GetInt32();
                else if (t.TryGetProperty("level", out var lvl2) && lvl2.ValueKind == JsonValueKind.Number)
                    nivel = lvl2.GetInt32();

                return (nombre, nivel);
            }
        }
        return null;
    }

    private static string? GetStr(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private sealed record ConceptoBusqueda(string Clave, string Termino, bool EsCertificacion);

    private sealed record ResultadoConcepto(ConceptoBusqueda Concepto, List<JsonElement> Personas);
}
