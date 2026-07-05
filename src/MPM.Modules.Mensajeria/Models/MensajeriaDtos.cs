namespace MPM.Modules.Mensajeria.Models;

public class ConversacionResumenDto
{
    public long Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string? Asunto { get; set; }
    public long? LicitacionId { get; set; }
    public string? LicitacionNombre { get; set; }
    public List<ParticipanteItemDto> Participantes { get; set; } = new();
    public MensajeResumenDto? UltimoMensaje { get; set; }
    public long NoLeidos { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ConversacionDetalleDto
{
    public long Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string? Asunto { get; set; }
    public long? LicitacionId { get; set; }
    public string? LicitacionNombre { get; set; }
    public List<ParticipanteItemDto> Participantes { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ParticipanteItemDto
{
    public string UserId { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime? JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
}

public class MensajeResumenDto
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class MensajeDetalleDto
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string? Contenido { get; set; }
    public MensajeResumenDto? ReplyTo { get; set; }
    public List<AdjuntoItemDto> Adjuntos { get; set; } = new();
    public List<MensajeEstadoDto> Estados { get; set; } = new();
    public DateTime? EditedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdjuntoItemDto
{
    public long Id { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long TamanioBytes { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AdjuntoDetalleDto
{
    public long Id { get; set; }
    public long MensajeId { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long TamanioBytes { get; set; }
    public string RutaStorage { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class MensajeEstadoDto
{
    public string UserId { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public class PresenciaDto
{
    public string UserId { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
}

public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public int TotalPages { get; set; }
}
