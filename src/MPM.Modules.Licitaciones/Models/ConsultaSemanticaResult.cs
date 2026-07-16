namespace MPM.Modules.Licitaciones.Models;

/// <summary>
/// Interpretación en lenguaje natural de una consulta de búsqueda (018-buscador-inteligente-nl),
/// calculada por <see cref="Services.ConsultaSemanticaService"/> vía Gemini. No se persiste --
/// vive solo en memoria durante el request. Si la interpretación falla o Vertex no está
/// configurado, el llamador recibe null y debe degradar a búsqueda literal (FR-005).
/// </summary>
public class ConsultaSemanticaResult
{
    public List<string> TerminosExpandidos { get; set; } = new();
    public short? EstadoInferido { get; set; }
    public decimal? MontoDesde { get; set; }
    public decimal? MontoHasta { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public ConfianzaInterpretacion Confianza { get; set; } = ConfianzaInterpretacion.Baja;
}

public enum ConfianzaInterpretacion
{
    Baja,
    Alta,
}
