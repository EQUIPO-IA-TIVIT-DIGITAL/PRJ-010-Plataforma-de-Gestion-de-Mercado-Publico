namespace MPM.Modules.Alertas.Models;

public class ReglaAlertaDto
{
    public long Id { get; set; }
    public string Keyword { get; set; } = "";
    public List<string>? SinonimosIa { get; set; }
    public decimal? MontoMinimo { get; set; }
    public decimal? MontoMaximo { get; set; }
    public string[]? TiposLicitacion { get; set; }
    public string[]? Organismos { get; set; }
    public bool Activa { get; set; } = true;
    public bool NotificarTelegram { get; set; }
}

public class CrearReglaRequest
{
    public string Keyword { get; set; } = "";
    public decimal? MontoMinimo { get; set; }
    public decimal? MontoMaximo { get; set; }
    public string[]? TiposLicitacion { get; set; }
    public string[]? Organismos { get; set; }
    public bool NotificarTelegram { get; set; }
}

/// <summary>Regla activa candidata a evaluar en el motor de matching (research.md §1).</summary>
public record ReglaActiva(
    long Id,
    string UsuarioId,
    string Keyword,
    List<string>? SinonimosIa,
    decimal? MontoMinimo,
    decimal? MontoMaximo,
    int[]? TiposLicitacion,
    string[]? Organismos);

/// <summary>Licitación mínima necesaria para evaluar el matching, sin acoplar a los DTOs del módulo Licitaciones.</summary>
public record LicitacionParaMatching(
    long LicitacionId,
    string CodigoExterno,
    string Nombre,
    string? Descripcion,
    decimal? Monto,
    string? TipoLicitacion,
    string? Organismo,
    DateTime? FechaCierre = null,
    string? Link = null);

public record ResumenEnriquecido(
    string? Requisitos,
    string? Competidores,
    string? Presupuesto,
    string? FormaPago,
    string? Multas,
    bool? EsRenovacion,
    string? ProveedorActual);

public class AlertaDisparadaDto
{
    public long Id { get; set; }
    public long LicitacionId { get; set; }
    public string? TerminoMatch { get; set; }
    public ResumenEnriquecido? ResumenEnriquecido { get; set; }
    public bool EsPrueba { get; set; }
    public DateTime DisparadaEn { get; set; }
}

/// <summary>
/// El frontend ya tiene estos datos al elegir la licitación de una lista (viene del módulo
/// Licitaciones) — se pasan directo en vez de que Alertas haga un lookup cross-module
/// (Alertas no referencia el proyecto de Licitaciones; es al revés, Principio I).
/// </summary>
public record ProbarAlertaRequest(
    long LicitacionId,
    string CodigoExterno,
    string Nombre,
    string? Descripcion,
    decimal? Monto,
    string? TipoLicitacion,
    string? Organismo);

public record GuardarTelegramChatIdRequest(string TelegramChatId);

public record GuardarEmailAlertasRequest(string EmailAlertas);

public class HistorialAlertasDto
{
    public List<AlertaDisparadaDto> Items { get; set; } = [];
    public long TotalCount { get; set; }
}

public record ProbarAlertaResponse(
    long AlertaDisparadaId,
    bool EsPrueba,
    bool NotificacionInAppCreada,
    bool NotificacionTelegramEnviada,
    string? NotificacionTelegramError);
