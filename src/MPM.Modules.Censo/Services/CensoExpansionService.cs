using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MPM.Core.SystemConfig;
using MPM.Modules.Censo.Data;
using MPM.Modules.Censo.Models;
using MPM.Shared.Services;

namespace MPM.Modules.Censo.Services;

/// <summary>
/// Expansión de conceptos de licitación a tecnologías concretas de Census (D7.7/D7.8,
/// CEN-R004): Capa 1 fuzzy contra types del catálogo (≥80, $0, ~22 ms), Capa 2 fuzzy contra
/// tecnologías, Capa 3 IA fallback (prompt MANTENER/TRADUCIR/DESCARTAR) validada contra el
/// catálogo y persistida en <c>censo_expansiones</c> — se paga 1 vez por concepto.
/// Normalización sin acentos + token_set_ratio (patrón mcp-v2 de Caso-01).
/// </summary>
public class CensoExpansionService(
    CensoHandler handler,
    CensoCatalogoService catalogoService,
    LlmClientResolver llmResolver,
    ILogger<CensoExpansionService> logger)
{
    private const int UmbralFuzzy = 80;
    private const int MaxTecnologiasPorConcepto = 4;

    /// <summary>
    /// Expande un concepto a tecnologías validadas contra el catálogo. Devuelve la lista y
    /// la fuente ('catalogo' para capas 1-2, 'ia' para el fallback del LLM).
    /// </summary>
    public virtual async Task<(List<string> Tecnologias, string Fuente)> ExpandirAsync(string concepto, CancellationToken ct = default)
    {
        var normalizado = Normalizar(concepto);
        if (string.IsNullOrWhiteSpace(normalizado))
            return (new List<string>(), "catalogo");

        // Cache de expansión: concepto → tecnologías validadas (no se re-paga IA).
        var cacheado = await handler.ExpansionObtenerAsync(normalizado, ct);
        if (cacheado != null && cacheado.Count > 0)
            return (cacheado, "catalogo");

        // Capa 1: fuzzy contra types (los types YA son conceptos amplios: "Front-END" → react...).
        var catalogo = await catalogoService.ListarAsync(null, null, null, ct);
        var types = catalogo.Items.Select(i => i.TypeName).Distinct().ToList();
        var mejorType = types
            .Select(t => (Nombre: t, Score: TokenSetRatio(normalizado, Normalizar(t))))
            .Where(x => x.Score >= UmbralFuzzy)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (mejorType.Nombre != null)
        {
            var tecnologias = catalogo.Items
                .Where(i => i.TypeName == mejorType.Nombre)
                .Select(i => i.Tecnologia)
                .Distinct()
                .Take(MaxTecnologiasPorConcepto)
                .ToList();
            if (tecnologias.Count > 0)
            {
                await handler.ExpansionUpsertAsync(normalizado, tecnologias, "catalogo", ct);
                return (tecnologias, "catalogo");
            }
        }

        // Capa 2: fuzzy directo contra tecnologías del catálogo.
        var mejorTecnologia = catalogo.Items
            .Select(i => i.Tecnologia)
            .Distinct()
            .Select(t => (Nombre: t, Score: TokenSetRatio(normalizado, Normalizar(t))))
            .Where(x => x.Score >= UmbralFuzzy)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (mejorTecnologia.Nombre != null)
        {
            var lista = new List<string> { mejorTecnologia.Nombre };
            await handler.ExpansionUpsertAsync(normalizado, lista, "catalogo", ct);
            return (lista, "catalogo");
        }

        // Capa 3: IA fallback (persistida en censo_expansiones, una vez por concepto).
        var ia = await ExpandirConIaAsync(concepto, normalizado, catalogo.Items, ct);
        await handler.ExpansionUpsertAsync(normalizado, ia, "ia", ct);
        return (ia, "ia");
    }

    // ── Capa 3 (IA) ─────────────────────────────────────────────────────────────────

    private async Task<List<string>> ExpandirConIaAsync(
        string concepto, string normalizado, List<CensoCatalogoItemDto> catalogo, CancellationToken ct)
    {
        try
        {
            // Hint del catálogo: types resumidos para que el LLM traduzca contra términos reales.
            var typesResumidos = catalogo
                .Select(i => i.TypeName)
                .Distinct()
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var request = new LlmRequest(
                Messages: [new LlmMessage("user", [new LlmTextPart(PromptExpansion(concepto, typesResumidos))])],
                Temperature: 0.2,
                MaxOutputTokens: 65536,
                JsonResponse: true);

            // Timeout explícito: el SDK del proveedor IA no usa el HttpClient del DI.
            using var llamadaCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            llamadaCts.CancelAfter(TimeSpan.FromMinutes(2));

            var client = await llmResolver.GetClientAsync(llamadaCts.Token);
            var result = await client.GenerarContenidoAsync(request, llamadaCts.Token);

            var candidatas = ParseTecnologias(result.Text);
            var validadas = new List<string>();
            foreach (var candidata in candidatas)
            {
                var norm = Normalizar(candidata);
                if (string.IsNullOrWhiteSpace(norm)) continue;

                // El LLM propone, el catálogo valida (≥80): se toma el nombre canónico del catálogo.
                var mejor = catalogo
                    .Select(i => i.Tecnologia)
                    .Distinct()
                    .Select(t => (Nombre: t, Score: TokenSetRatio(norm, Normalizar(t))))
                    .Where(x => x.Score >= UmbralFuzzy)
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault();

                if (mejor.Nombre != null && !validadas.Contains(mejor.Nombre))
                    validadas.Add(mejor.Nombre);
            }

            if (validadas.Count == 0)
            {
                logger.LogInformation("IA descartó todos los candidatos para '{Concepto}' — fallback término original", concepto);
                return new List<string> { concepto };
            }

            return validadas.Take(MaxTecnologiasPorConcepto).ToList();
        }
        catch (Exception ex)
        {
            // Fallback degradado: el término original (Census busca por substring igualmente).
            logger.LogWarning(ex, "Fallo la expansión IA de '{Concepto}' — se usa el término original", concepto);
            return new List<string> { concepto };
        }
    }

    /// <summary>Prompt MANTENER/TRADUCIR/DESCARTAR adaptado de Caso-01 (D7.7/D7.8) con hint de types.</summary>
    internal static string PromptExpansion(string concepto, List<string> typesResumidos)
    {
        // Hint acotado (el catálogo completo es ~939 tecnologías; los types son ~210 — se
        // truncan para no inflar el prompt; los más relevantes ya cubren la traducción).
        var hint = string.Join(", ", typesResumidos.Take(120));

        return $$"""
            Eres un experto en el catálogo de conocimiento de skills de TIVIT (empresa de tecnología).
            Recibes un término extraído de una licitación y debes mapearlo a tecnologías concretas del catálogo.

            Término a expandir: "{{concepto}}"

            Decide, en este orden:
            - MANTENER: si el término YA es una tecnología concreta del catálogo → devuélvelo tal cual.
            - TRADUCIR: si el término es un concepto amplio (ej: "frontend", "ciberseguridad") →
              devuelve hasta 4 tecnologías concretas del catálogo que lo cubren (usa los types como guía).
            - DESCARTAR: si el término no corresponde a ninguna tecnología del catálogo (es un
              requisito no técnico, un servicio, o irrelevante) → devuelve lista vacía.

            Catálogo disponible — types (conceptos amplios): {{hint}}

            RESPONDE SOLO CON UN ÚNICO OBJETO JSON VÁLIDO:
            {"tecnologias": ["tecnología-1", "tecnología-2"]}
            """;
    }

    /// <summary>Parsea la respuesta JSON del LLM: {"tecnologias": [...]} o un array directo.</summary>
    internal static List<string> ParseTecnologias(string? texto)
    {
        var lista = new List<string>();
        if (string.IsNullOrWhiteSpace(texto)) return lista;

        try
        {
            var limpio = texto.Replace("\0", "").Trim();
            using var doc = JsonDocument.Parse(limpio);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in root.EnumerateArray())
                    if (t.ValueKind == JsonValueKind.String) lista.Add(t.GetString()!);
                return lista;
            }

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("tecnologias", out var arr) &&
                arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in arr.EnumerateArray())
                    if (t.ValueKind == JsonValueKind.String) lista.Add(t.GetString()!);
            }
        }
        catch (JsonException)
        {
            // Respuesta no JSON → sin candidatas (capa 3 degrada al término original).
        }

        return lista;
    }

    // ── Normalización y fuzzy (patrón mcp-v2) ──────────────────────────────────────

    /// <summary>Minúsculas + sin acentos (FormD, strip combining marks) — ES→PT para types.</summary>
    internal static string Normalizar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "";
        var formD = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>RapidFuzz token_set_ratio (0-100): tokens como conjuntos, mejor combinación sort+ratio.</summary>
    internal static int TokenSetRatio(string a, string b)
    {
        var aTokens = Tokenizar(a);
        var bTokens = Tokenizar(b);
        if (aTokens.Count == 0 || bTokens.Count == 0)
            return aTokens.Count == bTokens.Count ? 100 : 0;

        var setA = aTokens.ToHashSet(StringComparer.Ordinal);
        var setB = bTokens.ToHashSet(StringComparer.Ordinal);
        var interseccion = setA.Intersect(setB).OrderBy(t => t, StringComparer.Ordinal).ToList();
        var soloA = setA.Except(setB).OrderBy(t => t, StringComparer.Ordinal).ToList();
        var soloB = setB.Except(setA).OrderBy(t => t, StringComparer.Ordinal).ToList();

        var baseStr = string.Join(" ", interseccion);
        var aStr = baseStr.Length > 0 && soloA.Count > 0 ? $"{baseStr} {string.Join(" ", soloA)}" : baseStr.Length > 0 ? baseStr : string.Join(" ", soloA);
        var bStr = baseStr.Length > 0 && soloB.Count > 0 ? $"{baseStr} {string.Join(" ", soloB)}" : baseStr.Length > 0 ? baseStr : string.Join(" ", soloB);

        var combos = new (string X, string Y)[]
        {
            (aStr, baseStr),
            (baseStr, bStr),
            (aStr, bStr),
        };

        var mejor = 0;
        foreach (var (x, y) in combos)
        {
            var ratio = Ratio(x, y);
            if (ratio > mejor) mejor = ratio;
        }
        return mejor;
    }

    private static List<string> Tokenizar(string texto)
        => texto.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    /// <summary>Ratio Levenshtein (0-100): 100 × (1 − dist / max(longitudes)).</summary>
    internal static int Ratio(string a, string b)
    {
        if (a == b) return 100;
        if (a.Length == 0 || b.Length == 0) return 0;

        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var costo = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + costo);
            }
            (prev, curr) = (curr, prev);
        }

        var distancia = prev[b.Length];
        var maxLen = Math.Max(a.Length, b.Length);
        return (int)Math.Round(100.0 * (1.0 - (double)distancia / maxLen));
    }
}
