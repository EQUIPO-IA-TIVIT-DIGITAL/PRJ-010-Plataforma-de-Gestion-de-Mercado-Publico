namespace MPM.Modules.Colaboracion.Models;

public class LicitacionInteresDto
{
    public long Id { get; set; }
    public long LicitacionId { get; set; }
    public long? WorkspaceId { get; set; }
    public long? ConversacionId { get; set; }
    public string MarcadoPor { get; set; } = string.Empty;
    public short EstadoLicitacionAlMarcar { get; set; }
    public short EstadoLicitacionActual { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // FR-017: aviso visual cuando el estado de la licitación cambió desde que se marcó de interés
    public bool EstadoCambio => EstadoLicitacionAlMarcar != EstadoLicitacionActual;
}

public class LicitacionInteresListItemDto : LicitacionInteresDto
{
    public string LicitacionNombre { get; set; } = string.Empty;
    public string CodigoExterno { get; set; } = string.Empty;
}

public class VincularInteresRequest
{
    public long? WorkspaceId { get; set; }
    public long? ConversacionId { get; set; }
}
