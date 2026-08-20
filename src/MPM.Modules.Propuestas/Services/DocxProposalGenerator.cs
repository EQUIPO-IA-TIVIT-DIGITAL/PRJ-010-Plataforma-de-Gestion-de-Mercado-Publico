using System.Globalization;
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
    string? ExecutiveSummary,
    string? LicitacionTitulo = null,
    string? LicitacionCodigo = null,
    string? OrganismoComprador = null,
    string? ContactName = null,
    string? ContactEmail = null,
    string? ContactTitle = null,
    string? ContactPhone = null);

/// <summary>
/// Genera la propuesta técnica corporativa en formato DOCX conservando intacta la plantilla
/// maestra oficial TIVIT (portada gráfica, estilos, tipografías, colores institucionales y encabezados).
/// Realiza el reemplazo de etiquetas dinámicas ({{ ... }} y {% ... %}), inyecta los capítulos,
/// certificaciones oficiales y tabla de casos de éxito, y habilita la auto-actualización del índice.
/// </summary>
public sealed class DocxProposalGenerator(ProposalTemplateProvider templateProvider)
{
    private static readonly Regex CensusDataRegex = new(
        @"(?i)\b(census-token|user-certifications|corporateid|securitytoken|@census\.example)\b",
        RegexOptions.Compiled);

    private static readonly HashSet<string> BuiltinSectionTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Carátula",
        "Caratula",
        "Declaración de confidencialidad",
        "Declaracion de confidencialidad",
        "Resumen ejecutivo",
        "Certificaciones TIVIT",
        "Experiencias TIVIT",
        "Alcance del servicio",
        "Organigrama",
        "Aportes de las partes",
        "Listado de entregables",
        "Capítulos teóricos",
        "Capitulos teoricos"
    };

    private const string SedeTivitChile = "TIVIT Chile Tercerización de Procesos, Servicios y Tecnología SpA";
    private const string DireccionTivitChile = "Av. Los Jardines 927, Ciudad Empresarial, Huechuraba, Santiago, Chile";

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

                // 1. Construir diccionario de reemplazos de variables simples
                var replacements = BuildReplacementDictionary(input);

                // 2. Reemplazar variables en encabezados y pies de página (conservando logos e imágenes)
                ReplaceInHeadersAndFooters(mainPart, replacements);

                // 3. Procesar tablas dinámicas (Casos de Éxito / Experiencias y Contacto)
                ProcessTables(mainPart, input, replacements);

                // 4. Procesar párrafos del cuerpo del documento principal
                ProcessBodyParagraphs(mainPart, input, replacements);

                // 5. Incrustar paquetes binarios PDF para certificaciones que cuenten con archivo
                EmbedCertificationPdfs(mainPart, input.Certifications);

                // 6. Activar auto-actualización de tabla de contenidos (TOC / Índice) al abrir en Word
                EnableAutoUpdateTableOfContents(mainPart);

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
            throw new ProposalGenerationException("PRO_009", $"No se pudo generar el documento DOCX: {ex.Message} -> {ex.StackTrace}", ex);
        }
    }

    private static Dictionary<string, string> BuildReplacementDictionary(ProposalDocumentInput input)
    {
        var now = DateTime.Now;
        var culturaEs = new CultureInfo("es-CL");
        var titulo = Sanitize(input.LicitacionTitulo) ?? "Propuesta Técnica y Comercial";
        var codigo = Sanitize(input.LicitacionCodigo) ?? "S/C";
        var cliente = Sanitize(input.OrganismoComprador) ?? "Organismo Comprador";
        var contactoNombre = Sanitize(input.ContactName) ?? "Equipo Comercial TIVIT";
        var contactoEmail = Sanitize(input.ContactEmail) ?? "comercial.chile@tivit.com";
        var contactoCargo = Sanitize(input.ContactTitle) ?? "Key Account Manager";
        var contactoFono = Sanitize(input.ContactPhone) ?? "+56 2 2480 0000";
        var resumen = Sanitize(input.ExecutiveSummary)
            ?? "El presente documento detalla la propuesta técnica y metodología integral de servicios propuesta por TIVIT para dar cumplimiento a todos los requerimientos y bases de la licitación.";

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{{title}}"] = titulo,
            ["{{ title }}"] = titulo,
            ["{{tvt}}"] = codigo,
            ["{{ tvt }}"] = codigo,
            ["{{client_name}}"] = cliente,
            ["{{ client_name }}"] = cliente,
            ["{{client_acronym}}"] = cliente,
            ["{{ client_acronym }}"] = cliente,
            ["{{current_date}}"] = now.ToString("dd/MM/yyyy"),
            ["{{ current_date }}"] = now.ToString("dd/MM/yyyy"),
            ["{{current_month_year}}"] = now.ToString("MMMM yyyy", culturaEs),
            ["{{ current_month_year }}"] = now.ToString("MMMM yyyy", culturaEs),
            ["{{country}}"] = "Chile",
            ["{{ country }}"] = "Chile",
            ["{{country|capitalize}}"] = "Chile",
            ["{{ country|capitalize }}"] = "Chile",
            ["{{sede_tivit}}"] = SedeTivitChile,
            ["{{ sede_tivit }}"] = SedeTivitChile,
            ["{{direccion_tivit}}"] = DireccionTivitChile,
            ["{{ direccion_tivit }}"] = DireccionTivitChile,
            ["{{contact_name}}"] = contactoNombre,
            ["{{ contact_name }}"] = contactoNombre,
            ["{{contact_title}}"] = contactoCargo,
            ["{{ contact_title }}"] = contactoCargo,
            ["{{contact_phone}}"] = contactoFono,
            ["{{ contact_phone }}"] = contactoFono,
            ["{{contact_email}}"] = contactoEmail,
            ["{{ contact_email }}"] = contactoEmail,
            ["{{summary}}"] = resumen,
            ["{{ summary }}"] = resumen,
        };
    }

    private static void ReplaceInHeadersAndFooters(MainDocumentPart mainPart, Dictionary<string, string> replacements)
    {
        foreach (var headerPart in mainPart.HeaderParts)
        {
            foreach (var p in headerPart.Header.Descendants<Paragraph>())
                ReplaceTextInParagraph(p, replacements);
            headerPart.Header.Save();
        }

        foreach (var footerPart in mainPart.FooterParts)
        {
            foreach (var p in footerPart.Footer.Descendants<Paragraph>())
                ReplaceTextInParagraph(p, replacements);
            footerPart.Footer.Save();
        }
    }

    private static void ProcessTables(MainDocumentPart mainPart, ProposalDocumentInput input, Dictionary<string, string> replacements)
    {
        var body = mainPart.Document.Body;
        if (body == null) return;

        foreach (var table in body.Descendants<Table>().ToList())
        {
            var rows = table.Elements<TableRow>().ToList();
            TableRow? templateLoopRow = null;
            TableRow? endforRow = null;

            foreach (var row in rows)
            {
                var rowText = string.Concat(row.Descendants<Text>().Select(t => t.Text));

                if (rowText.Contains("{% for exp in experiencias %}", StringComparison.OrdinalIgnoreCase))
                {
                    templateLoopRow = row;
                }
                else if (rowText.Contains("{% endfor %}", StringComparison.OrdinalIgnoreCase))
                {
                    endforRow = row;
                }
                else
                {
                    // Reemplazo normal de celdas en otras tablas
                    foreach (var cell in row.Elements<TableCell>())
                    {
                        foreach (var p in cell.Elements<Paragraph>())
                            ReplaceTextInParagraph(p, replacements);
                    }
                }
            }

            // Si encontramos la tabla de experiencias Jinja2:
            if (templateLoopRow != null)
            {
                var insertBeforeRef = templateLoopRow;

                if (input.Experiences.Count == 0)
                {
                    var emptyRow = (TableRow)templateLoopRow.CloneNode(true);
                    SetRowCellValues(emptyRow, "1", "Experiencia general en servicios de Tecnología, Cloud y Ciberseguridad TIVIT.",
                        "Clientes Sector Público / Privado", "Chile", "-", "Vigente", "-");
                    table.InsertBefore(emptyRow, insertBeforeRef);
                }
                else
                {
                    int itemIndex = 1;
                    foreach (var exp in input.Experiences)
                    {
                        var expRow = (TableRow)templateLoopRow.CloneNode(true);
                        var desc = $"{Sanitize(exp.Titulo)}: {Sanitize(exp.Descripcion) ?? "Servicios TI"}";
                        var cliente = Sanitize(exp.Cliente) ?? "Cliente Corporativo";
                        var pais = Sanitize(exp.Pais) ?? "Chile";
                        var inicio = exp.FechaInicio?.ToString("yyyy") ?? "-";
                        var fin = exp.FechaFin?.ToString("yyyy") ?? "Vigente";
                        var monto = exp.MontoUsd.HasValue ? $"USD {exp.MontoUsd.Value:N0}" : "-";

                        SetRowCellValues(expRow, itemIndex.ToString(), desc, cliente, pais, inicio, fin, monto);
                        table.InsertBefore(expRow, insertBeforeRef);
                        itemIndex++;
                    }
                }

                // Eliminar fila de plantilla y fila endfor
                templateLoopRow.Remove();
                endforRow?.Remove();
            }
        }
    }

    private static void SetRowCellValues(TableRow row, params string[] values)
    {
        var cells = row.Elements<TableCell>().ToList();
        for (int i = 0; i < cells.Count && i < values.Length; i++)
        {
            var cell = cells[i];
            var p = cell.GetFirstChild<Paragraph>() ?? cell.AppendChild(new Paragraph());
            var texts = p.Descendants<Text>().ToList();
            if (texts.Count > 0)
            {
                texts[0].Text = values[i];
                for (int t = 1; t < texts.Count; t++)
                    texts[t].Text = string.Empty;
            }
            else
            {
                p.AppendChild(new Run(new Text(values[i])));
            }
        }
    }

    private static void ProcessBodyParagraphs(MainDocumentPart mainPart, ProposalDocumentInput input, Dictionary<string, string> replacements)
    {
        var body = mainPart.Document.Body;
        if (body == null) return;

        var paragraphs = body.Elements<Paragraph>().ToList();

        for (int i = 0; i < paragraphs.Count; i++)
        {
            var p = paragraphs[i];
            var fullText = string.Concat(p.Descendants<Text>().Select(t => t.Text));

            // Caso 1: Bucle de Certificaciones Jinja2
            if (fullText.Contains("{% for cert in certifications_section %}", StringComparison.OrdinalIgnoreCase))
            {
                var certInsertRef = p;

                // Inyectar certificaciones de empresa estructuradas
                if (input.Certifications.Count == 0)
                {
                    body.InsertBefore(CreateParagraph("TIVIT cuenta con certificaciones internacionales de gestión de calidad, seguridad y data center vigentes."), certInsertRef);
                }
                else
                {
                    foreach (var certDoc in input.Certifications)
                    {
                        var cert = certDoc.Certification;
                        var titular = string.IsNullOrWhiteSpace(cert.Titular) ? "TIVIT SpA" : cert.Titular;
                        var inst = string.IsNullOrWhiteSpace(cert.Institucion) ? "" : $" — {cert.Institucion}";
                        var vig = string.IsNullOrWhiteSpace(cert.Vigencia) ? "" : $" (Vigencia: {cert.Vigencia})";

                        var pCert = CreateParagraph($"• {Sanitize(cert.Nombre)}{inst}{vig}", bold: true, colorHex: "0F172A");
                        body.InsertBefore(pCert, certInsertRef);

                        if (!string.IsNullOrWhiteSpace(titular))
                        {
                            var pDesc = CreateParagraph($"   Titular oficial: {Sanitize(titular)}", italic: true, colorHex: "475569");
                            body.InsertBefore(pDesc, certInsertRef);
                        }

                        if (certDoc.Warning != null)
                        {
                            var pWarn = CreateParagraph($"   Advertencia: {Sanitize(certDoc.Warning)}", italic: true, colorHex: "DC2626");
                            body.InsertBefore(pWarn, certInsertRef);
                        }
                    }
                }

                // Ahora eliminar párrafo plantilla y siguientes hasta {% endfor %}
                p.Remove();
                while (i + 1 < paragraphs.Count)
                {
                    var nextP = paragraphs[i + 1];
                    var nextText = string.Concat(nextP.Descendants<Text>().Select(t => t.Text));
                    if (nextText.Contains("{% endfor %}", StringComparison.OrdinalIgnoreCase))
                    {
                        nextP.Remove();
                        i++;
                        break;
                    }
                    nextP.Remove();
                    i++;
                }

                continue;
            }

            // Caso 2: Bucle de Capítulos Jinja2
            if (fullText.Contains("{% for chapter in chapters_section %}", StringComparison.OrdinalIgnoreCase))
            {
                var chapInsertRef = p;

                // Inyectar únicamente capítulos personalizados o anexos técnicos adicionales
                // (Los 10 capítulos estándar 1..10 ya forman parte de la estructura visual nativa de la plantilla)
                var customChapters = input.Chapters
                    .Where(c => c.Orden > 10 || (!BuiltinSectionTitles.Contains(c.Titulo) && !string.IsNullOrWhiteSpace(c.ContenidoMarkdown) && !c.ContenidoMarkdown.StartsWith("## TIVIT", StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(c => c.Orden).ThenBy(c => c.Id)
                    .ToList();

                foreach (var chapter in customChapters)
                {
                    var chapTitle = CreateParagraph($"{chapter.Orden}. {Sanitize(chapter.Titulo)}", bold: true, sizePt: 14, colorHex: "E30613");
                    body.InsertBefore(chapTitle, chapInsertRef);

                    var content = Sanitize(chapter.ContenidoMarkdown);
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        foreach (var line in content.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
                        {
                            var chapBody = CreateParagraph(line.Trim());
                            body.InsertBefore(chapBody, chapInsertRef);
                        }
                    }
                }

                // Ahora eliminar párrafo plantilla y siguientes hasta {% endfor %}
                p.Remove();
                while (i + 1 < paragraphs.Count)
                {
                    var nextP = paragraphs[i + 1];
                    var nextText = string.Concat(nextP.Descendants<Text>().Select(t => t.Text));
                    if (nextText.Contains("{% endfor %}", StringComparison.OrdinalIgnoreCase))
                    {
                        nextP.Remove();
                        i++;
                        break;
                    }
                    nextP.Remove();
                    i++;
                }

                continue;
            }

            // Caso 3: Tags condicionales de estimación de costos {% if cost_estimation %}
            if (fullText.Contains("{% if cost_estimation %}", StringComparison.OrdinalIgnoreCase))
            {
                p.Remove();
                while (i + 1 < paragraphs.Count)
                {
                    var nextP = paragraphs[i + 1];
                    var nextText = string.Concat(nextP.Descendants<Text>().Select(t => t.Text));
                    if (nextText.Contains("{% endif %}", StringComparison.OrdinalIgnoreCase))
                    {
                        nextP.Remove();
                        i++;
                        break;
                    }
                    nextP.Remove();
                    i++;
                }
                continue;
            }

            if (fullText.Contains("{% endif %}", StringComparison.OrdinalIgnoreCase))
            {
                p.Remove();
                continue;
            }

            // Caso 4: Reemplazo normal de placeholders en el párrafo
            ReplaceTextInParagraph(p, replacements);
        }
    }

    private static void ReplaceTextInParagraph(Paragraph p, Dictionary<string, string> replacements)
    {
        var textElements = p.Descendants<Text>().ToList();
        if (textElements.Count == 0) return;

        var fullText = string.Concat(textElements.Select(t => t.Text));
        var modified = false;
        var resultText = fullText;

        foreach (var (key, value) in replacements)
        {
            if (resultText.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                resultText = Regex.Replace(resultText, Regex.Escape(key), value, RegexOptions.IgnoreCase);
                modified = true;
            }
        }

        // Limpiar cualquier residuo de etiquetas Jinja2 no coincidentes
        if (resultText.Contains("{{") && resultText.Contains("}}"))
        {
            resultText = Regex.Replace(resultText, @"\{\{[^}]+\}\}", "", RegexOptions.Compiled);
            modified = true;
        }

        if (modified)
        {
            textElements[0].Text = resultText;
            for (int i = 1; i < textElements.Count; i++)
                textElements[i].Text = string.Empty;
        }
    }

    private static void EmbedCertificationPdfs(MainDocumentPart mainPart, IReadOnlyList<CertificationDocument> certifications)
    {
        foreach (var certDoc in certifications)
        {
            if (certDoc.PdfBytes is { Length: > 0 })
            {
                try
                {
                    var embeddedPart = mainPart.AddEmbeddedPackagePart("application/pdf");
                    using var stream = embeddedPart.GetStream(FileMode.Create, FileAccess.Write);
                    stream.Write(certDoc.PdfBytes, 0, certDoc.PdfBytes.Length);
                }
                catch
                {
                    // No bloquear la generación si falla el empaquetado binario OLE
                }
            }
        }
    }

    private static void EnableAutoUpdateTableOfContents(MainDocumentPart mainPart)
    {
        try
        {
            var settingsPart = mainPart.DocumentSettingsPart ?? mainPart.AddNewPart<DocumentSettingsPart>();
            if (settingsPart.Settings == null)
                settingsPart.Settings = new Settings();

            if (settingsPart.Settings.GetFirstChild<UpdateFieldsOnOpen>() == null)
            {
                settingsPart.Settings.PrependChild(new UpdateFieldsOnOpen { Val = true });
            }
            settingsPart.Settings.Save();
        }
        catch
        {
            // Best effort para TOC
        }
    }

    private static Paragraph CreateParagraph(string text, bool bold = false, bool italic = false, int? sizePt = null, string? colorHex = null)
    {
        var runProps = new RunProperties();
        if (bold) runProps.Append(new Bold());
        if (italic) runProps.Append(new Italic());
        if (sizePt.HasValue) runProps.Append(new FontSize { Val = (sizePt.Value * 2).ToString() });
        if (!string.IsNullOrWhiteSpace(colorHex)) runProps.Append(new Color { Val = colorHex });

        var run = new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return new Paragraph(run);
    }

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        return CensusDataRegex.Replace(value, "[dato corporativo omitido]");
    }
}
