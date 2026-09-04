using System.Security.Cryptography;
using System.Text;
using MPM.Modules.Licitaciones.Data;

namespace MPM.Modules.Licitaciones.Services;

/// <summary>
/// Hash del conjunto de documentos de una licitación (036-flujo-comercial-ofertas).
/// Clave de cache: si el conjunto no cambió, ni la descarga ni el análisis IA se re-pagan.
/// </summary>
public static class AdjuntoDocumentosHash
{
    /// <summary>
    /// SHA-256 de los hashes de contenido ordenados por nombre. Null si falta el hash de
    /// algún documento (el cache del conjunto no es fiable con hashes parciales).
    /// </summary>
    public static string? CalcularConjuntoHash(List<AdjuntoDocumentosHandler.AdjuntoDocumentoFila> filas)
    {
        var hashes = filas
            .Where(f => !string.IsNullOrWhiteSpace(f.Sha256Hash))
            .OrderBy(f => f.NombreArchivo, StringComparer.Ordinal)
            .Select(f => f.Sha256Hash!)
            .ToList();

        if (hashes.Count == 0 || hashes.Count != filas.Count) return null;

        var joined = string.Join('|', hashes);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(joined))).ToLowerInvariant();
    }
}
