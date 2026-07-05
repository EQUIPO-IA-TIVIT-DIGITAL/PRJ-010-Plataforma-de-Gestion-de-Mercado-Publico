namespace MPM.Modules.Notificaciones.Models;

public class NotificacionItemDto
{
    public long Id { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public string? Metadata { get; set; }
    public bool Leido { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? TotalCount { get; set; }
}

public class NotificacionesCountDto
{
    public long Count { get; set; }
}

public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public long TotalRecords { get; set; }
    public int TotalPages { get; set; }
}
