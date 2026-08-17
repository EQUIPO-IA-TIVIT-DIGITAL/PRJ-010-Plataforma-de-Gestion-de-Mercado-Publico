namespace MPM.Modules.Propuestas.Models;

public sealed class CatalogoPage<T>
{
    public List<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int Size { get; init; }
    public long TotalRecords { get; init; }
    public int TotalPages { get; init; }
}

public sealed class ExperienciaCatalogoDto
{
    public long Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public decimal? MontoUsd { get; set; }
    public string? Pais { get; set; }
    public bool Activo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ExperienciaCatalogoRequest
{
    public string Titulo { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public decimal? MontoUsd { get; set; }
    public string? Pais { get; set; }
    public bool? Activo { get; set; }
}

public sealed class CertificacionCatalogoDto
{
    public long Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? FileIdCensus { get; set; }
    public string? Institucion { get; set; }
    public string? Vigencia { get; set; }
    public string? Titular { get; set; }
    public string Tipo { get; set; } = "corporativa";
    public bool Activo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class CertificacionCatalogoRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string? FileIdCensus { get; set; }
    public string? Institucion { get; set; }
    public string? Vigencia { get; set; }
    public string? Titular { get; set; }
    public string? Tipo { get; set; }
    public bool? Activo { get; set; }
}

public sealed class CapituloCatalogoDto
{
    public long Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? ContenidoMarkdown { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class CapituloCatalogoRequest
{
    public string Titulo { get; set; } = string.Empty;
    public string? ContenidoMarkdown { get; set; }
    public int Orden { get; set; }
    public bool? Activo { get; set; }
}

public sealed class CensusSyncResultDto
{
    public int Procesadas { get; init; }
    public int Insertadas { get; init; }
    public int Actualizadas { get; init; }
    public int SinArchivo { get; init; }
    public long DurationMs { get; init; }
}

public sealed class RecomendacionRequest
{
    public string? CodigoExterno { get; set; }
    public RequisitosRecomendacionDto? Requisitos { get; set; }
}

public sealed class RequisitosRecomendacionDto
{
    public List<string> Certificaciones { get; set; } = [];
    public List<string> Tecnologias { get; set; } = [];
    public string? Industria { get; set; }
}

public sealed class RecomendacionResponseDto
{
    public string Fuente { get; init; } = "body";
    public RequisitosRecomendacionDto RequisitosUsados { get; init; } = new();
    public List<CertificacionRecomendacionDto> Certificaciones { get; init; } = [];
    // Bundle B incorporará el proveedor de experiencias; no se inventa un proveedor aquí.
    public List<ExperienciaRecomendacionDto> Experiencias { get; init; } = [];
    public RecomendacionResumenDto Resumen { get; init; } = new();
}

public sealed class CertificacionRecomendacionDto
{
    public long Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string? Institucion { get; init; }
    public decimal Score { get; init; }
    public string Categoria { get; init; } = string.Empty;
    public bool TieneArchivo { get; init; }
}

public sealed class ExperienciaRecomendacionDto
{
    public long Id { get; init; }
    public string Titulo { get; init; } = string.Empty;
    public string Cliente { get; init; } = string.Empty;
    public decimal Score { get; init; }
    public string Categoria { get; init; } = string.Empty;
    public string? Motivo { get; init; }
}

public sealed class RecomendacionResumenDto
{
    public int Recomendados { get; init; }
    public int Posibles { get; init; }
    public int Descartados { get; init; }
}

public sealed class GenerarPropuestaRequest
{
    public List<long>? CapitulosIds { get; set; }
    public List<long>? CertificacionesIds { get; set; }
    public List<long>? ExperienciasIds { get; set; }
}

public sealed class GenerarPropuestaResponse
{
    public long PropuestaId { get; init; }
    public int Version { get; init; }
    public string Estado { get; init; } = "generada";
    public string RutaDescarga { get; init; } = string.Empty;
    public string GeneradoPor { get; init; } = string.Empty;
    public DateTime GeneradoAt { get; init; }
    public PropuestaResumenDto Resumen { get; init; } = new();
}

public sealed class PropuestaResumenDto
{
    public int Capitulos { get; init; }
    public int Certificaciones { get; init; }
    public int CertificacionesSinPdf { get; init; }
    public int Experiencias { get; init; }
    public string ArchivosStorage { get; init; } = "local";
}

public sealed class PropuestaHistorialDto
{
    public long PropuestaId { get; init; }
    public int Version { get; init; }
    public string Estado { get; init; } = string.Empty;
    public int Capitulos { get; init; }
    public int Certificaciones { get; init; }
    public int Experiencias { get; init; }
    public string? GeneradoPor { get; init; }
    public DateTime? GeneradoAt { get; init; }
    public string? RutaDescarga { get; set; }
}

public sealed class PropuestaEstadoRequest
{
    public string Estado { get; set; } = string.Empty;
}

public sealed class AvisarRequest
{
    public List<string> Destinatarios { get; set; } = [];
}

public sealed class AvisarResponse
{
    public long DecisionId { get; init; }
    public string CodigoExterno { get; init; } = string.Empty;
    public string Decision { get; init; } = string.Empty;
    public List<string> Notificados { get; init; } = [];
    public DateTime NotificadoAt { get; init; }
    public int Enviados { get; init; }
}

public sealed class PropuestaCatalogSnapshot
{
    public IReadOnlyList<CapituloCatalogoDto> Capitulos { get; init; } = [];
    public IReadOnlyList<CertificacionCatalogoDto> Certificaciones { get; init; } = [];
    public IReadOnlyList<ExperienciaCatalogoDto> Experiencias { get; init; } = [];
}

public sealed class ExportarDriveResponse
{
    public string DriveFileId { get; init; } = string.Empty;
    public string WebUrl { get; init; } = string.Empty;
    public string NombreArchivo { get; init; } = string.Empty;
    public DateTime ExportadoAt { get; init; }
}
