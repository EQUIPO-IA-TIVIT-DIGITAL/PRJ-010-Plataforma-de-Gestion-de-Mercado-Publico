using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace MPM.Shared.Services;

/// <summary>
/// Extractor universal de contenido textual para documentos de licitaciones (.docx, .doc, .pdf, .txt).
/// Permite alimentar LLMs de texto puro (como Qwen 3.7 / OpenAI-compatible) y normalizar anexos Word
/// para Gemini sin depender de binarios pesados.
/// </summary>
public static class DocumentContentExtractor
{
    /// <summary>
    /// Extrae el texto plano estructurado de un archivo según su extensión.
    /// </summary>
    public static string ExtractText(byte[] bytes, string fileName)
    {
        if (bytes == null || bytes.Length == 0)
            return string.Empty;

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".docx" => ExtractFromDocx(bytes),
            ".pdf" => ExtractFromPdf(bytes),
            ".txt" => Encoding.UTF8.GetString(bytes),
            _ => string.Empty
        };
    }

    public static string ExtractTextFromPdf(byte[] pdfBytes) => ExtractFromPdf(pdfBytes);
    public static string ExtractTextFromDocx(byte[] docxBytes) => ExtractFromDocx(docxBytes);

    /// <summary>
    /// Extrae texto y tablas de un documento Word (.docx) usando OpenXML.
    /// </summary>
    public static string ExtractFromDocx(byte[] docxBytes)
    {
        try
        {
            using var ms = new MemoryStream(docxBytes);
            using var doc = WordprocessingDocument.Open(ms, false);
            var body = doc.MainDocumentPart?.Document.Body;
            if (body == null)
                return string.Empty;

            var sb = new StringBuilder();

            foreach (var element in body.Elements())
            {
                if (element is Paragraph p)
                {
                    var text = p.InnerText.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                    }
                }
                else if (element is Table table)
                {
                    sb.AppendLine();
                    foreach (var row in table.Elements<TableRow>())
                    {
                        var cells = row.Elements<TableCell>()
                            .Select(c => c.InnerText.Trim().Replace("\r", " ").Replace("\n", " "))
                            .ToList();
                        if (cells.Any(c => !string.IsNullOrWhiteSpace(c)))
                        {
                            sb.AppendLine("| " + string.Join(" | ", cells) + " |");
                        }
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString().Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Extrae texto de un documento PDF página por página usando PdfPig.
    /// </summary>
    public static string ExtractFromPdf(byte[] pdfBytes)
    {
        try
        {
            using var document = PdfDocument.Open(pdfBytes);
            var sb = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                var text = page.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine($"--- Página {page.Number} ---");
                    sb.AppendLine(text);
                    sb.AppendLine();
                }
            }

            return sb.ToString().Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Formatea el documento con encabezado y separadores para ser inyectado en un prompt LLM.
    /// </summary>
    public static string FormatForPrompt(string fileName, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine($"=== DOCUMENTO: {fileName} ===");
        sb.AppendLine(text);
        sb.AppendLine($"=== FIN DOCUMENTO: {fileName} ===");
        sb.AppendLine();
        return sb.ToString();
    }
}
