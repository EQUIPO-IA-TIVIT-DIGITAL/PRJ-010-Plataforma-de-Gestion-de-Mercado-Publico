using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MPM.Modules.Propuestas.Models;

namespace MPM.Modules.Propuestas.Services;

public sealed record CertificationDocument(
    CertificacionCatalogoDto Certification,
    byte[]? PdfBytes,
    string? Warning);

public sealed record ProposalDocumentInput(
    IReadOnlyList<CapituloCatalogoDto> Chapters,
    IReadOnlyList<CertificationDocument> Certifications,
    IReadOnlyList<ExperienciaCatalogoDto> Experiences,
    string? ExecutiveSummary);

/// <summary>
/// Renderiza la plantilla corporativa con Open XML SDK. La plantilla se conserva como base
/// de estilos, tema, fuentes y recursos corporativos, pero su contenido mutable se reconstruye
/// desde snapshots de catálogo para no arrastrar placeholders ni datos de la base PRJ-001.
/// </summary>
public sealed class DocxProposalGenerator(ProposalTemplateProvider templateProvider)
{
    private static readonly Regex EmailRegex = new(
        @"\b[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CensusDataRegex = new(
        @"(?i)\b(census|user-certifications|corporateid|userid|securitytoken)\b",
        RegexOptions.Compiled);

    public byte[] Generate(ProposalDocumentInput input)
    {
        try
        {
            using var source = templateProvider.OpenRead();
            using var output = new MemoryStream();
            source.CopyTo(output);
            output.Position = 0;

            using (var document = WordprocessingDocument.Open(output, true))
            {
                var mainPart = document.MainDocumentPart
                    ?? throw new ProposalGenerationException("PRO_009", "La plantilla no contiene el documento principal");
                var body = mainPart.Document.Body
                    ?? throw new ProposalGenerationException("PRO_009", "La plantilla no contiene cuerpo Word");
                var sectionProperties = body.GetFirstChild<SectionProperties>();
                body.RemoveAllChildren();

                AppendCover(body);
                foreach (var chapter in input.Chapters.OrderBy(c => c.Orden).ThenBy(c => c.Id))
                {
                    AppendPageBreak(body);
                    AppendChapter(body, chapter, input, mainPart);
                }

                if (sectionProperties != null)
                    body.Append(sectionProperties);
                else
                    body.Append(new SectionProperties());

                ReplaceHeaderAndFooter(mainPart);
                mainPart.Document.Save();
            }

            output.Position = 0;
            return output.ToArray();
        }
        catch (ProposalGenerationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ProposalGenerationException("PRO_009", "No se pudo generar el documento DOCX", ex);
        }
    }

    private static void AppendCover(Body body)
    {
        body.Append(Paragraph("TIVIT", bold: true, size: 36));
        body.Append(Paragraph("Propuesta técnica y comercial", bold: true, size: 22));
        body.Append(Paragraph("Documento corporativo para evaluación de oferta", italic: true, size: 12));
        body.Append(Paragraph("La carátula utiliza la razón social fija TIVIT y no incluye datos personales."));
    }

    private static void AppendChapter(Body body, CapituloCatalogoDto chapter, ProposalDocumentInput input, MainDocumentPart mainPart)
    {
        body.Append(Paragraph($"{chapter.Orden}. {Sanitize(chapter.Titulo)}", bold: true, size: 20));
        var content = Sanitize(chapter.ContenidoMarkdown);
        if (!string.IsNullOrWhiteSpace(content))
            AppendWrappedText(body, content ?? string.Empty);

        switch (chapter.Orden)
        {
            case 3 when !string.IsNullOrWhiteSpace(input.ExecutiveSummary):
                body.Append(Paragraph("Resumen del análisis comercial", bold: true, size: 14));
            AppendWrappedText(body, Sanitize(input.ExecutiveSummary) ?? string.Empty);
                break;
            case 4:
                AppendCertifications(body, mainPart, input.Certifications);
                break;
            case 5:
                AppendExperiences(body, input.Experiences);
                break;
            case 7:
                body.Append(Paragraph("La estructura del servicio se organiza por funciones y responsabilidades. Los nombres y contactos de personas se gestionan fuera de este documento."));
                break;
        }
    }

    private static void AppendCertifications(Body body, MainDocumentPart mainPart, IReadOnlyList<CertificationDocument> certifications)
    {
        if (certifications.Count == 0)
        {
            body.Append(Paragraph("No se seleccionaron certificaciones para esta versión."));
            return;
        }

        foreach (var document in certifications)
        {
            var certification = document.Certification;
            var details = $"{Sanitize(certification.Nombre)}" +
                (string.IsNullOrWhiteSpace(certification.Institucion) ? "" : $" — {Sanitize(certification.Institucion)}") +
                (string.IsNullOrWhiteSpace(certification.Vigencia) ? "" : $" ({Sanitize(certification.Vigencia)})");
            body.Append(Paragraph(details, bold: true));
            if (document.PdfBytes is { Length: > 0 } && TryEmbedPdf(mainPart, document.PdfBytes))
                body.Append(Paragraph("PDF original incorporado como anexo del paquete DOCX."));
            else
                body.Append(Paragraph($"Advertencia: el PDF de {Sanitize(certification.Nombre)} no estuvo disponible; se conserva la referencia textual."));
        }
    }

    private static bool TryEmbedPdf(MainDocumentPart mainPart, byte[] pdfBytes)
    {
        try
        {
            var embeddedPart = mainPart.AddEmbeddedPackagePart("application/pdf");
            using var stream = embeddedPart.GetStream(FileMode.Create, FileAccess.Write);
            stream.Write(pdfBytes, 0, pdfBytes.Length);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void AppendExperiences(Body body, IReadOnlyList<ExperienciaCatalogoDto> experiences)
    {
        if (experiences.Count == 0)
        {
            body.Append(Paragraph("No se seleccionaron experiencias para esta versión."));
            return;
        }

        foreach (var experience in experiences)
        {
            body.Append(Paragraph($"{Sanitize(experience.Titulo)} — {Sanitize(experience.Cliente)}", bold: true));
            AppendWrappedText(body, Sanitize(experience.Descripcion) ?? "Experiencia corporativa seleccionada del catálogo manual.");
            var period = $"Periodo: {experience.FechaInicio?.ToString("yyyy-MM-dd") ?? "n/d"} a {experience.FechaFin?.ToString("yyyy-MM-dd") ?? "n/d"}";
            body.Append(Paragraph(Sanitize(period) ?? string.Empty));
        }
    }

    private static void AppendWrappedText(Body body, string text)
    {
        foreach (var line in text.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
            body.Append(Paragraph(line.Trim()));
    }

    private static void AppendPageBreak(Body body)
        => body.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));

    private static Paragraph Paragraph(string text, bool bold = false, bool italic = false, int? size = null)
    {
        var runProperties = new RunProperties();
        if (bold) runProperties.Append(new Bold());
        if (italic) runProperties.Append(new Italic());
        if (size.HasValue) runProperties.Append(new FontSize { Val = (size.Value * 2).ToString() });
        var run = new Run(runProperties, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return new Paragraph(run);
    }

    private static void ReplaceHeaderAndFooter(MainDocumentPart mainPart)
    {
        foreach (var header in mainPart.HeaderParts)
        {
            header.Header.RemoveAllChildren();
            header.Header.Append(Paragraph("TIVIT | Propuesta técnica", bold: true));
            header.Header.Save();
        }

        foreach (var footer in mainPart.FooterParts)
        {
            footer.Footer.RemoveAllChildren();
            footer.Footer.Append(Paragraph("Documento corporativo TIVIT"));
            footer.Footer.Save();
        }
    }

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var sanitized = EmailRegex.Replace(value, "[dato de contacto omitido]");
        return CensusDataRegex.Replace(sanitized, "[dato corporativo omitido]");
    }
}
