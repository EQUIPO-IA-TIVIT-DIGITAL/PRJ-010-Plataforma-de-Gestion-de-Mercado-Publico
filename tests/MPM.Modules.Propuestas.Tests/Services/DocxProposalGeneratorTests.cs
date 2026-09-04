using System.IO.Compression;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPM.Core.Data;
using MPM.Modules.Licitaciones.Models;
using MPM.Modules.Propuestas.Data;
using MPM.Modules.Propuestas.Models;
using MPM.Modules.Propuestas.Services;
using MPM.Shared.Services;
using Xunit;

namespace MPM.Modules.Propuestas.Tests.Services;

public sealed class DocxProposalGeneratorTests
{
    private static string TemplatePath => Path.Combine(AppContext.BaseDirectory, "Templates", "tivit_proposal_template.docx");

    [Fact]
    public void Generate_TenCanonicalChapters_ProducesValidOpenXmlAndNoCensusData()
    {
        var generator = CreateGenerator();
        var chapters = Enumerable.Range(1, 10).Select(i => new CapituloCatalogoDto
        {
            Id = i,
            Orden = i,
            Titulo = CanonicalTitle(i),
            ContenidoMarkdown = i == 7 ? "Roles corporativos. persona@census.example" : $"Contenido del capítulo {i}",
            Activo = true,
        }).ToList();
        var input = new ProposalDocumentInput(
            chapters,
            [new CertificationDocument(new CertificacionCatalogoDto { Id = 12, Nombre = "ISO 27001", Activo = true }, "%PDF-1.7"u8.ToArray(), null)],
            [new ExperienciaCatalogoDto { Id = 20, Titulo = "Proyecto de continuidad", Cliente = "Cliente corporativo", Descripcion = "Operación crítica" }],
            "Resumen ejecutivo sin datos de personal");

        var bytes = generator.Generate(input);
        bytes.Take(4).Should().Equal(0x50, 0x4B, 0x03, 0x04);
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        zip.GetEntry("word/document.xml").Should().NotBeNull();
        zip.Entries.Should().Contain(e => e.FullName.StartsWith("word/embeddings/", StringComparison.Ordinal));
        foreach (var xmlEntry in zip.Entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
        {
            using var reader = new StreamReader(xmlEntry.Open());
            var xml = reader.ReadToEnd();
            xml.ToLowerInvariant().Should().NotContain("@census.example");
            xml.ToLowerInvariant().Should().NotContain("census-token");
        }

        using var document = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        var errors = new OpenXmlValidator(FileFormatVersions.Microsoft365).Validate(document)
            .Where(e => e.ErrorType != ValidationErrorType.Schema || (!e.Id.Contains("UndeclaredAttribute") && e.Part?.Uri?.ToString() != "/word/settings.xml"))
            .ToList();
        Assert.Empty(errors);
        var text = string.Join("\n", document.MainDocumentPart!.Document.Descendants<Text>().Select(t => t.Text));
        text.Should().Contain("TIVIT");
        text.Should().NotContain("@census.example");
        text.Should().NotContain("census-token");
    }

    [Fact]
    public void Generate_CertificationWithoutPdf_KeepsTextAndWarning()
    {
        var generator = CreateGenerator();
        var bytes = generator.Generate(new ProposalDocumentInput(
            [new CapituloCatalogoDto { Id = 4, Orden = 4, Titulo = "Certificaciones TIVIT", ContenidoMarkdown = "Base" }],
            [new CertificationDocument(new CertificacionCatalogoDto { Id = 1, Nombre = "ISO 9001" }, null, "No se pudo descargar el PDF")],
            [], null));

        using var document = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        var text = string.Join("\n", document.MainDocumentPart!.Document.Descendants<Text>().Select(t => t.Text));
        text.Should().Contain("ISO 9001");
        text.Should().Contain("Advertencia");
    }

    [Fact]
    public void TemplateProvider_MissingTemplate_ReturnsPro010()
    {
        var provider = new ProposalTemplateProvider(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.docx"));

        var act = () => provider.ResolvePath();

        act.Should().Throw<ProposalGenerationException>().Where(e => e.Code == "PRO_010");
    }

    [Fact]
    public async Task Generar_NoGo_RejectsBeforeRendering()
    {
        var handler = new Mock<PropuestasHandler>(new DbConnectionFactory("Host=unused"));
        handler.Setup(h => h.ObtenerDecisionAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DecisionProposalRow { LicitacionId = 42, Decision = "no_go" });
        var service = CreateService(handler, new Mock<ICertificationFileProvider>().Object);

        var act = () => service.GenerarAsync("1425525-3-LE26", new GenerarPropuestaRequest(), "gerente@tivit.com");

        await act.Should().ThrowAsync<PropuestaService.PropuestaException>()
            .Where(e => e.Code == "PRO_003");
        handler.Verify(h => h.ListarCapitulosActivosAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Generar_TwoCalls_PersistsVersionsOneAndTwo()
    {
        var handler = CreateGeneratingHandler("go");
        handler.SetupSequence(h => h.GenerarPropuestaAsync(
                42, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                "gerente@tivit.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProposalMutationResult { Id = 100, Version = 1 })
            .ReturnsAsync(new ProposalMutationResult { Id = 101, Version = 2 });
        var service = CreateService(handler, new Mock<ICertificationFileProvider>().Object);

        var first = await service.GenerarAsync("1425525-3-LE26", new GenerarPropuestaRequest(), "gerente@tivit.com");
        var second = await service.GenerarAsync("1425525-3-LE26", new GenerarPropuestaRequest(), "gerente@tivit.com");

        first.Version.Should().Be(1);
        second.Version.Should().Be(2);
        handler.Verify(h => h.GenerarPropuestaAsync(
            42, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            "gerente@tivit.com", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Generar_CertificationDownloadFailure_DoesNotFailProposal()
    {
        var handler = CreateGeneratingHandler("go");
        var files = new Mock<ICertificationFileProvider>();
        files.Setup(f => f.DownloadAsync("file-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Census unavailable"));
        var service = CreateService(handler, files.Object);
        var request = new GenerarPropuestaRequest { CertificacionesIds = [12] };

        var result = await service.GenerarAsync("1425525-3-LE26", request, "gerente@tivit.com");

        result.Resumen.CertificacionesSinPdf.Should().Be(1);
        result.Estado.Should().Be("generada");
    }

    private static DocxProposalGenerator CreateGenerator()
        => new(new ProposalTemplateProvider(TemplatePath));

    private static Mock<PropuestasHandler> CreateGeneratingHandler(string decision)
    {
        var handler = new Mock<PropuestasHandler>(new DbConnectionFactory("Host=unused"));
        handler.Setup(h => h.ObtenerDecisionAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DecisionProposalRow { LicitacionId = 42, Decision = decision });
        handler.Setup(h => h.ListarCapitulosActivosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogoPage<CapituloCatalogoDto>
            {
                Items = Enumerable.Range(1, 10).Select(i => new CapituloCatalogoDto
                {
                    Id = i, Orden = i, Titulo = CanonicalTitle(i), ContenidoMarkdown = $"Contenido {i}", Activo = true,
                }).ToList(),
            });
        handler.Setup(h => h.ListarCertificacionesActivasAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogoPage<CertificacionCatalogoDto>
            {
                Items = [new CertificacionCatalogoDto { Id = 12, Nombre = "ISO 27001", FileIdCensus = "file-1", Activo = true }],
            });
        handler.Setup(h => h.ListarExperienciasActivasAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogoPage<ExperienciaCatalogoDto> { Items = [] });
        handler.Setup(h => h.GenerarPropuestaAsync(
                42, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                "gerente@tivit.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProposalMutationResult { Id = 99, Version = 1 });
        return handler;
    }

    private static PropuestaService CreateService(Mock<PropuestasHandler> handler, ICertificationFileProvider files)
    {
        var lookup = new Mock<IProposalLicitacionLookup>();
        lookup.Setup(l => l.ObtenerPorCodigoAsync("1425525-3-LE26", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicitacionDetalleDto { Id = 42, CodigoExterno = "1425525-3-LE26" });
        var summary = new Mock<IProposalSummaryProvider>();
        summary.Setup(s => s.ObtenerResumenAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync("Resumen");
        var storage = new Mock<IStorageService>();
        storage.Setup(s => s.UploadAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, string name, Stream _, string _, CancellationToken _) => $"{path}/{name}");
        return new PropuestaService(
            handler.Object, lookup.Object, summary.Object, files,
            new ProposalTemplateProvider(TemplatePath), CreateGenerator(), storage.Object,
            new Mock<IGoogleDriveService>().Object,
            NullLogger<PropuestaService>.Instance);
    }

    private static string CanonicalTitle(int order) => order switch
    {
        1 => "Carátula",
        2 => "Declaración de confidencialidad",
        3 => "Resumen ejecutivo",
        4 => "Certificaciones TIVIT",
        5 => "Experiencias TIVIT",
        6 => "Alcance del servicio",
        7 => "Organigrama",
        8 => "Aportes de las partes",
        9 => "Listado de entregables",
        10 => "Capítulos teóricos",
        _ => $"Capítulo {order}",
    };
}
