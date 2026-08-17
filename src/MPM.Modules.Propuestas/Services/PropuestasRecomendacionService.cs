using MPM.Modules.Censo.Data;
using MPM.Modules.Licitaciones.Services;
using MPM.Modules.Propuestas.Data;
using MPM.Modules.Propuestas.Models;

namespace MPM.Modules.Propuestas.Services;

public class PropuestasRecomendacionService(
    PropuestasHandler handler,
    CensoHandler censoHandler,
    LicitacionService licitacionService)
{
    public class RecomendacionException(string code, string message) : Exception(message) { public string Code { get; } = code; }

    public virtual async Task<RecomendacionResponseDto> RecomendarAsync(RecomendacionRequest request, CancellationToken ct = default)
    {
        var (requisitos, fuente) = await ResolverRequisitosAsync(request, ct);
        if (requisitos.Certificaciones.Count == 0)
            throw new RecomendacionException("PRO_004", "No hay certificaciones requeridas para recomendar");

        var catalogo = await handler.ListarCertificacionesAsync(null, true, null, 1, 100, ct);
        if (catalogo.Items.Count == 0)
            throw new RecomendacionException("PRO_006", "El catálogo de certificaciones está vacío; sincronice Census primero");

        var recomendaciones = new List<CertificacionRecomendacionDto>();
        foreach (var cert in catalogo.Items)
        {
            var score = requisitos.Certificaciones
                .Select(r => Score(r, cert.Nombre))
                .DefaultIfEmpty(0m)
                .Max();
            if (score < 0.3m) continue;
            recomendaciones.Add(new CertificacionRecomendacionDto
            {
                Id = cert.Id, Nombre = cert.Nombre, Institucion = cert.Institucion,
                Score = score, Categoria = Category(score), TieneArchivo = !string.IsNullOrWhiteSpace(cert.FileIdCensus),
            });
        }

        var ordered = recomendaciones.OrderByDescending(x => x.Score).ThenBy(x => x.Nombre, StringComparer.OrdinalIgnoreCase).ToList();
        return new RecomendacionResponseDto
        {
            Fuente = fuente,
            RequisitosUsados = requisitos,
            Certificaciones = ordered,
            Experiencias = [],
            Resumen = new RecomendacionResumenDto
            {
                Recomendados = ordered.Count(x => x.Categoria == "recomendado"),
                Posibles = ordered.Count(x => x.Categoria == "posible"),
                Descartados = ordered.Count(x => x.Categoria == "descartado"),
            },
        };
    }

    internal static decimal Score(string requisito, string nombre)
    {
        var req = CertificationNameNormalizer.NormalizeKey(requisito);
        var candidate = CertificationNameNormalizer.NormalizeKey(nombre);
        if (req == candidate) return 1.0m;
        if (candidate.Contains(req, StringComparison.Ordinal) || req.Contains(candidate, StringComparison.Ordinal)) return 0.85m;
        var reqTokens = CertificationNameNormalizer.Tokens(requisito);
        var candidateTokens = CertificationNameNormalizer.Tokens(nombre);
        var overlap = reqTokens.Intersect(candidateTokens, StringComparer.Ordinal).Count();
        var denominator = Math.Max(reqTokens.Count, candidateTokens.Count);
        return denominator == 0 ? 0m : overlap / (decimal)denominator >= 0.5m ? 0.6m : 0m;
    }

    private async Task<(RequisitosRecomendacionDto Requisitos, string Fuente)> ResolverRequisitosAsync(RecomendacionRequest request, CancellationToken ct)
    {
        if (request.Requisitos?.Certificaciones.Any(string.IsNullOrWhiteSpace) == false && request.Requisitos.Certificaciones.Count > 0)
            return (request.Requisitos, "body");
        if (string.IsNullOrWhiteSpace(request.CodigoExterno))
            throw new RecomendacionException("PRO_004", "Se requieren requisitos o codigoExterno");

        var lic = await licitacionService.ObtenerPorCodigoAsync(request.CodigoExterno, ct);
        if (lic == null) throw new RecomendacionException("LIC_001", "Licitación no encontrada");
        var analysis = await censoHandler.AnalisisRequisitosAsync(lic.Id, ct);
        if (!analysis.TieneAnalisisCompletado)
            throw new RecomendacionException("PRO_004", "No hay un análisis comercial completado");
        return (new RequisitosRecomendacionDto { Certificaciones = analysis.Certificaciones }, "analisis");
    }

    private static string Category(decimal score) => score >= 0.8m ? "recomendado" : score >= 0.5m ? "posible" : "descartado";
}
