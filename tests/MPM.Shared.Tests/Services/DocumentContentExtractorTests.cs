using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MPM.Shared.Services;
using Xunit;

namespace MPM.Shared.Tests.Services;

public class DocumentContentExtractorTests
{
    [Fact]
    public void ExtractText_FromTxtFile_ReturnsDecodedContent()
    {
        var text = "Requisitos técnicos de la licitación de prueba.";
        var bytes = Encoding.UTF8.GetBytes(text);

        var result = DocumentContentExtractor.ExtractText(bytes, "bases.txt");

        Assert.Equal(text, result);
    }

    [Fact]
    public void ExtractText_FromEmptyOrNull_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, DocumentContentExtractor.ExtractText([], "bases.pdf"));
        Assert.Equal(string.Empty, DocumentContentExtractor.ExtractText(null!, "bases.docx"));
    }

    [Fact]
    public void ExtractFromDocx_ValidDocxBytes_ExtractsParagraphsAndTables()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(
                new Paragraph(new Run(new Text("Título de las Bases"))),
                new Paragraph(new Run(new Text("Párrafo 1 con requerimientos.")))));
            mainPart.Document.Save();
        }
        var docxBytes = ms.ToArray();

        var result = DocumentContentExtractor.ExtractFromDocx(docxBytes);

        Assert.Contains("Título de las Bases", result);
        Assert.Contains("Párrafo 1 con requerimientos.", result);
    }

    [Fact]
    public void FormatForPrompt_WrapsWithDocumentHeaders()
    {
        var result = DocumentContentExtractor.FormatForPrompt("anexo.docx", "Contenido del anexo");

        Assert.Contains("=== DOCUMENTO: anexo.docx ===", result);
        Assert.Contains("Contenido del anexo", result);
        Assert.Contains("=== FIN DOCUMENTO: anexo.docx ===", result);
    }
}
