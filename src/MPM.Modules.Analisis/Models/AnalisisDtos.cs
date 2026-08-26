namespace MPM.Modules.Analisis.Models;

public class WorkspaceItemDto
{
    public long Id { get; set; }
    public long? LicitacionId { get; set; }
    public string? LicitacionNombre { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public long DocumentosCount { get; set; }
    public long? UltimoAnalisisId { get; set; }
    public long? TotalCount { get; set; }  // Mapeado desde totalcount del SP
    public DateTime? UltimoAnalisisFecha { get; set; }
    public DateTime CreatedAt { get; set; }

    // spec 031 (US3): fecha de adjudicación de la licitación asociada -- es el campo por el
    // que ahora se ordena el listado (V121), expuesto para que el frontend muestre por qué.
    public DateTime? FechaAdjudicacion { get; set; }
}

public class WorkspaceDetalleDto
{
    public long Id { get; set; }
    public long? LicitacionId { get; set; }
    public string? LicitacionNombre { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public long DocumentosCount { get; set; }
    public long? UltimoAnalisisId { get; set; }
    public long? UltimoAnalisisDocumentoId { get; set; }
    public string? UltimoAnalisisDocumentoNombre { get; set; }
    public DateTime? UltimoAnalisisFecha { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DocumentoItemDto
{
    public long Id { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long TamanioBytes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DocumentoDetalleDto
{
    public long Id { get; set; }
    public long WorkspaceId { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long TamanioBytes { get; set; }
    public string RutaStorage { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ResultadoDto
{
    public long Id { get; set; }
    public long WorkspaceId { get; set; }
    public long DocumentoId { get; set; }
    public string DocumentoNombre { get; set; } = string.Empty;
    public string? ContenidoJson { get; set; }
    public string ModeloUsado { get; set; } = string.Empty;
    public int TokensEntrada { get; set; }
    public int TokensSalida { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AnalisisResumenDto
{
    public long Id { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? ModeloUsado { get; set; }
    public int? TokensEntrada { get; set; }
    public int? TokensSalida { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ChatMensajeDto
{
    public long Id { get; set; }
    public string Rol { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ChatResponseDto
{
    public string Respuesta { get; set; } = string.Empty;
    public long ConversacionId { get; set; }
    public List<ChatMensajeDto> Mensajes { get; set; } = new();
}

public class ChatHistorialDto
{
    public long ConversacionId { get; set; }
    public List<ChatMensajeDto> Mensajes { get; set; } = new();
}

public class CrearWorkspaceRequest
{
    public long? LicitacionId { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public class AnalizarRequest
{
    public long? DocumentoId { get; set; }
}

public class ChatRequest
{
    public string Mensaje { get; set; } = string.Empty;
}

// --- Fase 3: Dashboard Ejecutivo (US2) ---

public class ResultadoCompletoDto
{
    public long WorkspaceId { get; set; }
    public string WorkspaceNombre { get; set; } = string.Empty;
    public long? LicitacionId { get; set; }
    public string ModeloUsado { get; set; } = string.Empty;
    public int TokensEntrada { get; set; }
    public int TokensSalida { get; set; }
    public DateTime CreadoEn { get; set; }
    public string ContenidoJson { get; set; } = string.Empty;
}

public class LicitacionResumenEjecutivoDto
{
    public long WorkspaceId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool TivitGano { get; set; }
    public string ResultadoTivit { get; set; } = string.Empty;
    public decimal? MontoAdjudicado { get; set; }
    public decimal? MontoTivit { get; set; }
    public string? Adjudicatario { get; set; }
    public string? AdjudicatarioRut { get; set; }
    public double? PuntajeTivit { get; set; }
    public double? PuntajeGanador { get; set; }
    public double? PuntajeMaximo { get; set; }
    public DateTime FechaAnalisis { get; set; }
    public List<string> CompetidoresNombres { get; set; } = new();
    public bool? CompetidorGano { get; set; }
    public string? ResultadoCompetidor { get; set; }
    public decimal? MontoCompetidor { get; set; }
}

public class CompetidorRankingDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Rut { get; set; }
    public int VecesCompetidor { get; set; }
    public int VecesGanador { get; set; }
    public decimal MontoTotalAdjudicado { get; set; }
    public List<LicitacionResumenEjecutivoDto> Licitaciones { get; set; } = new();
}

public class ComparacionAnualDto
{
    public int AnioActual { get; set; }
    public int AnioAnterior { get; set; }
    public decimal MontoActual { get; set; }
    public decimal MontoAnterior { get; set; }
    public double? VariacionPorcentaje { get; set; }
    public bool TieneDatosAnioAnterior { get; set; }
}

public class DashboardEjecutivoDto
{
    public int TotalAnalizadas { get; set; }
    public int TotalGanadas { get; set; }
    public int TotalPerdidas { get; set; }
    public decimal MontoTotalGanado { get; set; }
    public decimal MontoTotalPerdido { get; set; }
    public double? PuntajePromedioTivit { get; set; }
    public double? PuntajePromedioGanador { get; set; }
    public List<CompetidorRankingDto> RankingCompetidores { get; set; } = new();
    public List<string> FactoresPerdidaFrecuentes { get; set; } = new();
    public List<LicitacionResumenEjecutivoDto> Licitaciones { get; set; } = new();
    public List<int> AniosDisponibles { get; set; } = new();
    public ComparacionAnualDto? ComparacionAnual { get; set; }

    // Track2 ligero — CM Convenio Marco (ADR-016 opción B sin zip)
    public decimal MontoConvenioMarco { get; set; }
    public decimal MontoTotalGanadoConCm { get; set; }
}

public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public int TotalPages { get; set; }
}
