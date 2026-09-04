using System.Text.Json;

namespace MPM.Modules.Licitaciones.Models;

/// <summary>Estado del análisis comercial de una licitación (zona IA, 036-flujo-comercial-ofertas).</summary>
public class AnalisisComercialEstadoDto
{
    public string Estado { get; set; } = "pendiente"; // pendiente|analizando|completado|error
    public string? Error { get; set; }
    public string? ConjuntoHash { get; set; }
    public bool Desactualizado { get; set; }
    public string? ResumenEjecutivo { get; set; }
    public string? GoNoGo { get; set; }
    public decimal? ScoreConfianza { get; set; }
    public string? ModeloUsado { get; set; }
    public int? TokensEntrada { get; set; }
    public int? TokensSalida { get; set; }
    public string? CreadoPor { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Resultado estructurado del análisis (JSON del LLM, ya saneado).</summary>
    public JsonElement? Resultado { get; set; }
}

public class IniciarAnalisisComercialResultDto
{
    public string Estado { get; set; } = "analizando";
    public bool CacheHit { get; set; }
    public string? ConjuntoHash { get; set; }
}
