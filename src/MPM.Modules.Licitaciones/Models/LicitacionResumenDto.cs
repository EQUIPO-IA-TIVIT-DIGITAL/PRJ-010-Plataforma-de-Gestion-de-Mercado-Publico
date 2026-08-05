using System.Text.Json.Serialization;
using MPM.Modules.Catalogo.Models;

namespace MPM.Modules.Licitaciones.Models;

public class LicitacionResumenDto
{
    /// <summary>Id interno (bigint), agregado 2026-07-06 para el selector de "probar alerta" de 003-fase6-alertas-keywords.</summary>
    public long Id { get; set; }
    public string CodigoExterno { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;

    [JsonIgnore]
    public short CodigoEstado { get; set; }

    [JsonIgnore]
    public string EstadoNombre { get; set; } = string.Empty;

    public EstadoItemDto Estado => new() { Codigo = CodigoEstado, Nombre = EstadoNombre };
    public string Organismo { get; set; } = string.Empty;
    public DateTime? FechaPublicacion { get; set; }
    public DateTime? FechaCierre { get; set; }
    public decimal? MontoEstimado { get; set; }
    public string Moneda { get; set; } = string.Empty;
    public int ItemsCount { get; set; }

    [JsonIgnore]
    public int TotalCount { get; set; }
}

public class LicitacionDetalleDto : LicitacionResumenDto
{
    public string? Descripcion { get; set; }
    public string? UnidadTecnica { get; set; }
    public DateTime? FechaAdjudicacion { get; set; }
    public DateTime? FechaEstimadaAdjudicacion { get; set; }
    public string? Link { get; set; }
    public List<LicitacionItemDto> Items { get; set; } = new();
}

public class LicitacionItemDto
{
    public int Codigo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int? Cantidad { get; set; }
    public string? UnidadMedida { get; set; }
    public decimal? PrecioEstimado { get; set; }
    public string? Categoria { get; set; }
}

public class LicitacionSearchResult
{
    public string CodigoExterno { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Organismo { get; set; } = string.Empty;
}

public class LicitacionNaturalSearchResult
{
    public long Id { get; set; }
    public string CodigoExterno { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Organismo { get; set; }
    public short CodigoEstado { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public DateTime? FechaPublicacion { get; set; }
    public float Relevancia { get; set; }
    public long? TotalCount { get; set; }
}

public class NaturalSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public short? Estado { get; set; }
}

public class SeguimientoToggleDto
{
    public string CodigoExterno { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
}

public class EsSeguidaDto
{
    public bool EsSeguida { get; set; }
}

public class LicitacionSeguidaDto
{
    public string CodigoExterno { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public short CodigoEstado { get; set; }
    public DateTime? FechaPublicacion { get; set; }
    public DateTime? FechaCierre { get; set; }
    public DateTime SeguidaDesde { get; set; }
}

public class LicitacionParaMonitorDto
{
    public string CodigoExterno { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public short CodigoEstado { get; set; }
    public string[] UsuarioIds { get; set; } = Array.Empty<string>();
}

public class LicitacionFilter
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public short? Estado { get; set; }
    public string? Tipo { get; set; }
    public string? Organismo { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public string SortBy { get; set; } = "fecha_publicacion";
    public string SortDir { get; set; } = "desc";
    public short? Area { get; set; }
    public bool? SinClasificar { get; set; }
}

public class EstadoConteoDto
{
    public short CodigoEstado { get; set; }
    public string NombreEstado { get; set; } = string.Empty;
    public int Cantidad { get; set; }
}

public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalRecords / (double)PageSize);
}

public class SyncStatusDto
{
    public int SyncId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
}
