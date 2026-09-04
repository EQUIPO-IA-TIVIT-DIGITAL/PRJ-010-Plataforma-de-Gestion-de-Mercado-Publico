using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPM.Core.Data;
using MPM.Core.SystemConfig;
using MPM.Modules.Licitaciones.Data;
using MPM.Modules.Licitaciones.Services;
using MPM.Shared.Services;
using Xunit;

namespace MPM.Modules.Licitaciones.Tests.Services;

/// <summary>
/// 036-flujo-comercial-ofertas (Fase 1.3): contrato de AnalisisComercialService — cache por
/// conjuntoHash (no re-paga IA si la misma versión ya fue analizada), procesamiento con el
/// proveedor activo y saneado del JSON del LLM. No llama a un LLM real: mockea el resolver
/// (GetClientAsync es virtual) y el ILlmClient.
/// </summary>
public class AnalisisComercialServiceTests
{
    private readonly Mock<AdjuntoDocumentosHandler> _adjuntosMock;
    private readonly Mock<AnalisisComercialHandler> _analisisMock;
    private readonly Mock<IStorageService> _storageMock;
    private readonly Mock<LlmClientResolver> _resolverMock;
    private readonly Mock<ILlmClient> _llmMock;
    private readonly AnalisisComercialService _service;

    public AnalisisComercialServiceTests()
    {
        var dbFactory = new DbConnectionFactory("Host=localhost;Database=unused");
        _adjuntosMock = new Mock<AdjuntoDocumentosHandler>(dbFactory);
        _analisisMock = new Mock<AnalisisComercialHandler>(dbFactory);
        _storageMock = new Mock<IStorageService>();
        _resolverMock = new Mock<LlmClientResolver>(MockBehavior.Loose, null!, null!, null!);
        _llmMock = new Mock<ILlmClient>();

        _llmMock.Setup(c => c.ModelName).Returns("gemini-test");
        _llmMock.Setup(c => c.GenerarContenidoAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResult(
                JsonMinimo, "{}",
                new LlmUsage(100, 50, 150), "STOP"));

        _resolverMock.Setup(r => r.GetClientAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_llmMock.Object);

        _service = new AnalisisComercialService(
            NullLogger<AnalisisComercialService>.Instance,
            _resolverMock.Object,
            _adjuntosMock.Object,
            _analisisMock.Object,
            _storageMock.Object,
            new Mock<IServiceScopeFactory>().Object);
    }

    private const string JsonMinimo =
        """{"identificacion":{"nombre_licitacion":"Test"},"go_no_go":"go","score_confianza":0.7,"resumen_ejecutivo":"Resumen de prueba"}""";

    private static List<AdjuntoDocumentosHandler.AdjuntoDocumentoFila> FilasConHash() => new()
    {
        new AdjuntoDocumentosHandler.AdjuntoDocumentoFila
        {
            Id = 1, LicitacionId = 10, Tipo = "anexo", NombreArchivo = "bases.pdf",
            RutaStorage = "licitaciones/X/adjuntos/bases.pdf", RutaLocal = @"C:\tmp\bases.pdf",
            Sha256Hash = "hash-a", Version = 1, DescargaEstado = "completado",
        },
        new AdjuntoDocumentosHandler.AdjuntoDocumentoFila
        {
            Id = 2, LicitacionId = 10, Tipo = "anexo", NombreArchivo = "tecnicas.pdf",
            RutaStorage = "licitaciones/X/adjuntos/tecnicas.pdf", RutaLocal = @"C:\tmp\tecnicas.pdf",
            Sha256Hash = "hash-b", Version = 1, DescargaEstado = "completado",
        },
    };

    [Fact]
    public async Task IniciarAnalisisAsync_SinDocumentos_LanzaSinDocumentos()
    {
        _adjuntosMock.Setup(h => h.ListarAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AdjuntoDocumentosHandler.AdjuntoDocumentoFila>());

        var act = async () => await _service.IniciarAnalisisAsync(10, "729-134-LE26", "user@tivit.cl");

        await act.Should().ThrowAsync<AnalisisComercialService.SinDocumentosException>();
        _resolverMock.Verify(r => r.GetClientAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IniciarAnalisisAsync_CacheHit_NoLlamaAlLLM()
    {
        _adjuntosMock.Setup(h => h.ListarAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FilasConHash());
        var hash = AdjuntoDocumentosHash.CalcularConjuntoHash(FilasConHash());
        _analisisMock.Setup(h => h.ObtenerUltimoAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnalisisComercialHandler.AnalisisComercialFila
            {
                Id = 5, LicitacionId = 10, ConjuntoHash = hash!, Estado = "completado",
                ResultadoJson = JsonMinimo, GoNoGo = "go", ResumenEjecutivo = "Resumen de prueba",
            });

        var resultado = await _service.IniciarAnalisisAsync(10, "729-134-LE26", "user@tivit.cl");

        resultado.CacheHit.Should().BeTrue();
        resultado.Estado.Should().Be("completado");
        _resolverMock.Verify(r => r.GetClientAsync(It.IsAny<CancellationToken>()), Times.Never,
            "si el conjunto ya fue analizado no se debe volver a pagar IA");
        _analisisMock.Verify(h => h.IniciarAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcesarAsync_ConDocumentos_CompletaConResultadoDelLLM()
    {
        _storageMock.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));

        await _service.ProcesarAsync(9, FilasConHash(), "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890", CancellationToken.None);

        _analisisMock.Verify(h => h.CompletarAsync(
            9, "completado",
            It.Is<string>(j => j != null && j.Contains("\"go_no_go\":\"go\"")),
            "Resumen de prueba", "go", 0.7m,
            "gemini-test", 100, 50, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcesarAsync_RespuestaInvalida_MarcaError()
    {
        _storageMock.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));
        _llmMock.Setup(c => c.GenerarContenidoAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResult("no es json", "{}", new LlmUsage(1, 1, 2), "STOP"));

        await _service.ProcesarAsync(9, FilasConHash(), "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890", CancellationToken.None);

        _analisisMock.Verify(h => h.CompletarAsync(
            9, "error", null, null, null, null, null, null, null,
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void EsDocumentoPdf_FiltraXlsx()
    {
        // Verificado en vivo 2026-08-16: Gemini rechaza xlsx enviado como parte PDF
        // ("The document has no pages") — el filtro debe excluirlo.
        var pdf = FilasConHash()[0];
        var xlsx = FilasConHash()[0];
        xlsx.NombreArchivo = "LISTADO_HERRAMIENTAS.xlsx";
        xlsx.MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        AnalisisComercialService.EsDocumentoPdf(pdf).Should().BeTrue();
        AnalisisComercialService.EsDocumentoPdf(xlsx).Should().BeFalse();
    }

    [Fact]
    public void SanearYExtraer_RespuestaConArray_TomaPrimerElemento()
    {
        var (json, resumen, go, score) = AnalisisComercialService.SanearYExtraer($"[{JsonMinimo}]");

        resumen.Should().Be("Resumen de prueba");
        go.Should().Be("go");
        score.Should().Be(0.7m);
        json.Should().NotContain("[", "el primer elemento del array se toma como el objeto raíz");
    }
}
