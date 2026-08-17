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
