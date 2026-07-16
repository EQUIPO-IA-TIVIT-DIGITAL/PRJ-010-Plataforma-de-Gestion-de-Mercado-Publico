namespace MPM.Modules.Catalogo.Models;

public class EstadoItemDto
{
    public int Codigo { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public class TipoLicitacionItemDto
{
    // string, no int -- 027-catalogo-frontend-licitaciones-generales: el código real de tipo
    // de licitación del portal es texto (LE, LP, LQ...), no un id numérico interno.
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public class MonedaItemDto
{
    public int Codigo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Simbolo { get; set; } = string.Empty;
    public string CodigoIso { get; set; } = string.Empty;
}

public class CatalogosResponseDto
{
    public List<EstadoItemDto> EstadosLicitacion { get; set; } = new();
    public List<TipoLicitacionItemDto> TiposLicitacion { get; set; } = new();
    public List<MonedaItemDto> Monedas { get; set; } = new();
}
