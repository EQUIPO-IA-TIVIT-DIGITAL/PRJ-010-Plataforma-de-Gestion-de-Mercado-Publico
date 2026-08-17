namespace MPM.Modules.Censo.Models;

/// <summary>Requisitos para el match de capacidades (se pueden pasar explícitos o usar los del análisis).</summary>
public class CensoMatchRequest
{
    public List<string>? Tecnologias { get; set; }
    public List<string>? Certificaciones { get; set; }
    public bool? FiltrarPais { get; set; }
    public string? Pais { get; set; }
}

public class CensoMatchResultDto
{
    public DateTime EjecutadoEn { get; set; }
    public int Consultas { get; set; }
    public int CacheUsadas { get; set; }
    public List<string> TecnologiasExpandidas { get; set; } = new();
    public List<CensoPersonaDto> Personas { get; set; } = new();
    public CensoResumenDto Resumen { get; set; } = new();
}

public class CensoPersonaDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CorporateId { get; set; } = string.Empty;
    public string Pais { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public int Cobertura { get; set; }           // skills matcheados
    public int TotalRequeridos { get; set; }      // total de skills buscados
    public List<string> Skills { get; set; } = new();
    public List<string> Certificaciones { get; set; } = new();
}

/// <summary>Estado + resultado del último match (GET /match-capacidades).</summary>
public class CensoMatchEstadoDto
{
    public string Estado { get; set; } = "no_ejecutado"; // no_ejecutado | en_curso | completado | error
    public DateTime? UltimoEjecutadoAt { get; set; }
    public CensoMatchResultDto? Match { get; set; }
}

public class CensoCatalogoListadoDto
{
    public List<CensoCatalogoItemDto> Items { get; set; } = new();
    public CensoCatalogoResumenDto Resumen { get; set; } = new();
}

public class CensoCatalogoResumenDto
{
    public int Types { get; set; }
    public int Tecnologias { get; set; }
    public DateTime? ActualizadoAt { get; set; }
}

/// <summary>Conteo del refresh del catálogo (POST /censo/catalogo/refrescar).</summary>
public class CensoRefrescoResultDto
{
    public int Grupos { get; set; }
    public int Categorias { get; set; }
    public int Types { get; set; }
    public int Tecnologias { get; set; }
    public long DurationMs { get; set; }
}

public class CensoResumenDto
{
    public int TotalPersonas { get; set; }
    public int MaxCobertura { get; set; }
    public int PersonasConCoberturaAlta { get; set; } // >= 70%
}

public class CensoCatalogoItemDto
{
    public string Grupo { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string Tecnologia { get; set; } = string.Empty;
}

public class CensoPreferenciasDto
{
    public bool FiltrarPais { get; set; }
    public string Pais { get; set; } = "Chile";
}

public class CensoPreferenciasUpdateDto
{
    public bool? FiltrarPais { get; set; }
    public string? Pais { get; set; }
}

/// <summary>
/// Proyección acotada del endpoint Census user-certifications. Los identificadores de
/// usuario sólo viven en memoria durante la sincronización y nunca se persisten en MPM.
/// </summary>
public sealed record CensusCertificationRecord(
    string CertificationTypeName,
    string? FileId,
    string? Institution,
    string? Validity,
    string? UserId,
    string? CorporateId);
