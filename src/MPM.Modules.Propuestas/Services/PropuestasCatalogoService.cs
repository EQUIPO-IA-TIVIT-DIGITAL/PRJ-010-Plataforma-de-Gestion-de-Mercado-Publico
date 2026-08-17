using MPM.Modules.Propuestas.Data;
using MPM.Modules.Propuestas.Models;

namespace MPM.Modules.Propuestas.Services;

public class PropuestasCatalogoService(PropuestasHandler handler)
{
    public class PropuestasValidationException(string code, string message) : Exception(message)
    {
        public string Code { get; } = code;
    }

    public Task<CatalogoPage<ExperienciaCatalogoDto>> ListarExperienciasAsync(string? q, bool activo, int page, int size, CancellationToken ct = default)
    {
        ValidatePage(page, size);
        return handler.ListarExperienciasAsync(CleanQuery(q), activo, page, size, ct);
    }

    public Task<ExperienciaCatalogoDto> CrearExperienciaAsync(ExperienciaCatalogoRequest request, CancellationToken ct = default)
    {
        Require(request.Titulo, "titulo"); Require(request.Cliente, "cliente");
        if (request.MontoUsd < 0) throw new PropuestasValidationException("VAL_007", "montoUsd no puede ser negativo");
        return handler.CrearExperienciaAsync(Normalize(request), ct);
    }

    public Task<ExperienciaCatalogoDto> ActualizarExperienciaAsync(long id, ExperienciaCatalogoRequest request, CancellationToken ct = default)
    {
        ValidateId(id); Require(request.Titulo, "titulo"); Require(request.Cliente, "cliente");
        return handler.ActualizarExperienciaAsync(id, Normalize(request), ct);
    }

    public Task EliminarExperienciaAsync(long id, CancellationToken ct = default) { ValidateId(id); return handler.EliminarExperienciaAsync(id, ct); }

    public Task<CatalogoPage<CertificacionCatalogoDto>> ListarCertificacionesAsync(string? q, bool activo, bool? conArchivo, int page, int size, CancellationToken ct = default)
    {
        ValidatePage(page, size);
        return handler.ListarCertificacionesAsync(CleanQuery(q), activo, conArchivo, page, size, ct);
    }

    public Task<CertificacionCatalogoDto> CrearCertificacionAsync(CertificacionCatalogoRequest request, CancellationToken ct = default)
    {
        Require(request.Nombre, "nombre");
        var normalized = NormalizeCertification(request);
        return handler.CrearCertificacionAsync(normalized, CertificationNameNormalizer.NormalizeKey(normalized.Nombre), ct);
    }

    public Task<CertificacionCatalogoDto> ActualizarCertificacionAsync(long id, CertificacionCatalogoRequest request, CancellationToken ct = default)
    {
        ValidateId(id); Require(request.Nombre, "nombre");
        var normalized = NormalizeCertification(request);
        return handler.ActualizarCertificacionAsync(id, normalized, CertificationNameNormalizer.NormalizeKey(normalized.Nombre), ct);
    }

    public Task EliminarCertificacionAsync(long id, CancellationToken ct = default) { ValidateId(id); return handler.EliminarCertificacionAsync(id, ct); }

    public Task<CatalogoPage<CapituloCatalogoDto>> ListarCapitulosAsync(string? q, bool activo, int page, int size, CancellationToken ct = default)
    {
        ValidatePage(page, size);
        return handler.ListarCapitulosAsync(CleanQuery(q), activo, page, size, ct);
    }

    public Task<CapituloCatalogoDto> CrearCapituloAsync(CapituloCatalogoRequest request, CancellationToken ct = default)
    {
        Require(request.Titulo, "titulo"); ValidateOrder(request.Orden);
        return handler.CrearCapituloAsync(Normalize(request), ct);
    }

    public Task<CapituloCatalogoDto> ActualizarCapituloAsync(long id, CapituloCatalogoRequest request, CancellationToken ct = default)
    {
        ValidateId(id); Require(request.Titulo, "titulo"); ValidateOrder(request.Orden);
        return handler.ActualizarCapituloAsync(id, Normalize(request), ct);
    }

    public Task EliminarCapituloAsync(long id, CancellationToken ct = default) { ValidateId(id); return handler.EliminarCapituloAsync(id, ct); }

    private static ExperienciaCatalogoRequest Normalize(ExperienciaCatalogoRequest r) => new() { Titulo = r.Titulo.Trim(), Cliente = r.Cliente.Trim(), Descripcion = CleanNullable(r.Descripcion), FechaInicio = r.FechaInicio, FechaFin = r.FechaFin, MontoUsd = r.MontoUsd, Pais = CleanNullable(r.Pais), Activo = r.Activo };
    private static CertificacionCatalogoRequest NormalizeCertification(CertificacionCatalogoRequest r) => new() { Nombre = CertificationNameNormalizer.NormalizeDisplay(r.Nombre), FileIdCensus = CleanNullable(r.FileIdCensus), Institucion = CleanNullable(r.Institucion), Vigencia = CleanNullable(r.Vigencia), Activo = r.Activo };
    private static CapituloCatalogoRequest Normalize(CapituloCatalogoRequest r) => new() { Titulo = r.Titulo.Trim(), ContenidoMarkdown = CleanNullable(r.ContenidoMarkdown), Orden = r.Orden, Activo = r.Activo };
    private static string? CleanNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? CleanQuery(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void Require(string? value, string field) { if (string.IsNullOrWhiteSpace(value)) throw new PropuestasValidationException("VAL_001", $"El campo '{field}' es obligatorio"); }
    private static void ValidateId(long id) { if (id <= 0) throw new PropuestasValidationException("VAL_007", "El id debe ser positivo"); }
    private static void ValidateOrder(int order) { if (order <= 0) throw new PropuestasValidationException("VAL_007", "El orden debe ser positivo"); }
    private static void ValidatePage(int page, int size) { if (page < 1) throw new PropuestasValidationException("VAL_007", "page debe ser mayor o igual a 1"); if (size < 1 || size > 100) throw new PropuestasValidationException("VAL_007", "size debe estar entre 1 y 100"); }
}
