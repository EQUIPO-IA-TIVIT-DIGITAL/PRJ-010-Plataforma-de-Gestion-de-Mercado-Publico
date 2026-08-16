namespace MPM.Modules.Licitaciones.Models;

/// <summary>Documento (adjunto) de una licitación — vista pública para la UI.</summary>
public class AdjuntoDocumentoDto
{
    public long Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string NombreArchivo { get; set; } = string.Empty;
    public long? TamanioBytes { get; set; }
    public string? MimeType { get; set; }
    public string? Sha256Hash { get; set; }
    public string? FechaGrilla { get; set; }
    public int Version { get; set; }
    public bool EsActa { get; set; }
    public string DescargaEstado { get; set; } = "pendiente";
    public DateTime? DescargadoAt { get; set; }
}

/// <summary>Estado del conjunto de documentos de una licitación (para cache y polling).</summary>
public class EstadoDocumentosDto
{
    public string EstadoConjunto { get; set; } = "pendiente";
    public string? DescargaError { get; set; }
    public string? ConjuntoHash { get; set; }
    public List<AdjuntoDocumentoDto> Documentos { get; set; } = new();
}

public class DescargarDocumentosRequest
{
    /// <summary>true re-descarga todos los adjuntos aunque los metadatos del portal coincidan.</summary>
    public bool Forzar { get; set; }
}

public class DescargarDocumentosResultDto
{
    public string EstadoConjunto { get; set; } = "descargando";
    public string Accion { get; set; } = "descargando";
    public int Descargados { get; set; }
    public int Reutilizados { get; set; }
    public int Actualizados { get; set; }
    public int Errores { get; set; }
    public string? DescargaError { get; set; }
    public string? ConjuntoHash { get; set; }
}
