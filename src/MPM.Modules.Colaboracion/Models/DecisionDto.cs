namespace MPM.Modules.Colaboracion.Models;

/// <summary>Body del POST /decision: la decisión es SIEMPRE humana (DEC-R004).</summary>
public class DecisionRequest
{
    public string Decision { get; set; } = string.Empty; // go | no_go
    public string? Motivo { get; set; }
}

/// <summary>Decisión registrada (POST /decision) — incluye el snapshot IA (V142).</summary>
public class DecisionDto
{
    public long? DecisionId { get; set; }
    public string CodigoExterno { get; set; } = string.Empty;
    public string? Decision { get; set; }            // go | no_go
    public string? Motivo { get; set; }
    public string? RecomendacionIa { get; set; }      // snapshot: strong_go|go|no_go|strong_no_go
    public decimal? ScoreConfianza { get; set; }      // snapshot 0-1
    public string? DecididoPor { get; set; }          // email del gerente (JWT)
    public DateTime? DecididoAt { get; set; }
    public List<string>? Notificados { get; set; }    // Fase 3 (queda NULL)
    public DateTime? NotificadoAt { get; set; }
}

/// <summary>Estado vigente de la decisión (GET /decision) para la ficha de la licitación.</summary>
public class DecisionEstadoDto
{
    public long? DecisionId { get; set; }
    public bool Decidida { get; set; }
    public string? Decision { get; set; }
    public string? Motivo { get; set; }
    public string? RecomendacionIa { get; set; }
    public decimal? ScoreConfianza { get; set; }
    public string? DecididoPor { get; set; }
    public DateTime? DecididoAt { get; set; }
    public List<string>? Notificados { get; set; }
    public DateTime? NotificadoAt { get; set; }
}
