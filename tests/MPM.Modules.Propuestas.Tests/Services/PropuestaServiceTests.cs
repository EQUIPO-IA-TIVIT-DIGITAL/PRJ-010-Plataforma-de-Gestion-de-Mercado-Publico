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

public class PropuestaServiceTests
{
    private readonly Mock<PropuestasHandler> _handlerMock;
    private readonly Mock<IProposalLicitacionLookup> _lookupMock;
    private readonly Mock<IProposalSummaryProvider> _summaryMock;
    private readonly Mock<ICertificationFileProvider> _certFileMock;
    private readonly Mock<IStorageService> _storageMock;
    private readonly Mock<IGoogleDriveService> _driveMock;
    private readonly ProposalTemplateProvider _templateProvider;
    private readonly DocxProposalGenerator _generator;

    public PropuestaServiceTests()
    {
        _handlerMock = new Mock<PropuestasHandler>(new DbConnectionFactory("Host=unused"));
        _lookupMock = new Mock<IProposalLicitacionLookup>();
        _summaryMock = new Mock<IProposalSummaryProvider>();
        _certFileMock = new Mock<ICertificationFileProvider>();
        _storageMock = new Mock<IStorageService>();
        _driveMock = new Mock<IGoogleDriveService>();

        _templateProvider = new ProposalTemplateProvider(AppContext.BaseDirectory);
        _generator = new DocxProposalGenerator(_templateProvider);
    }

    private PropuestaService CreateService()
    {
        return new PropuestaService(
            _handlerMock.Object,
            _lookupMock.Object,
            _summaryMock.Object,
            _certFileMock.Object,
            _templateProvider,
            _generator,
            _storageMock.Object,
            _driveMock.Object,
            NullLogger<PropuestaService>.Instance);
    }

    [Fact]
    public async Task GenerarAsync_LicitacionInexistente_ThrowsLic001()
    {
        var service = CreateService();
        _lookupMock.Setup(l => l.ObtenerPorCodigoAsync("LIC-404", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LicitacionDetalleDto?)null);

        var act = () => service.GenerarAsync("LIC-404", new GenerarPropuestaRequest(), "user@tivit.com");

        var ex = await act.Should().ThrowAsync<PropuestaService.PropuestaException>();
        ex.Which.Code.Should().Be("LIC_001");
    }

    [Theory]
    [InlineData("no_go")]
    [InlineData("pendiente")]
    [InlineData(null)]
    public async Task GenerarAsync_SinDecisionGo_ThrowsPro003(string? decision)
    {
        var service = CreateService();
        _lookupMock.Setup(l => l.ObtenerPorCodigoAsync("LIC-100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicitacionDetalleDto { Id = 10, CodigoExterno = "LIC-100" });

        _handlerMock.Setup(h => h.ObtenerDecisionAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(decision == null ? null : new DecisionProposalRow { Decision = decision });

        var act = () => service.GenerarAsync("LIC-100", new GenerarPropuestaRequest(), "user@tivit.com");

        var ex = await act.Should().ThrowAsync<PropuestaService.PropuestaException>();
        ex.Which.Code.Should().Be("PRO_003");
    }

    [Fact]
    public async Task GenerarAsync_CatalogoVacioCuandoSePidenCertificaciones_ThrowsPro006()
    {
        var service = CreateService();
        _lookupMock.Setup(l => l.ObtenerPorCodigoAsync("LIC-100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicitacionDetalleDto { Id = 10, CodigoExterno = "LIC-100" });

        _handlerMock.Setup(h => h.ObtenerDecisionAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DecisionProposalRow { Decision = "go" });

        _handlerMock.Setup(h => h.ListarCapitulosActivosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogoPage<CapituloCatalogoDto> { Items = [new CapituloCatalogoDto { Id = 1, Titulo = "Cap 1", Orden = 1 }] });

        _handlerMock.Setup(h => h.ListarCertificacionesActivasAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogoPage<CertificacionCatalogoDto> { Items = [] });

        _handlerMock.Setup(h => h.ListarExperienciasActivasAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogoPage<ExperienciaCatalogoDto> { Items = [] });

        var request = new GenerarPropuestaRequest { CertificacionesIds = [1, 2] };

        var act = () => service.GenerarAsync("LIC-100", request, "user@tivit.com");

        var ex = await act.Should().ThrowAsync<PropuestaService.PropuestaException>();
        ex.Which.Code.Should().Be("PRO_006");
    }

    [Fact]
    public async Task GenerarAsync_CapituloInvalidoOInactivo_ThrowsPro002()
    {
        var service = CreateService();
        _lookupMock.Setup(l => l.ObtenerPorCodigoAsync("LIC-100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicitacionDetalleDto { Id = 10, CodigoExterno = "LIC-100" });

        _handlerMock.Setup(h => h.ObtenerDecisionAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DecisionProposalRow { Decision = "go" });

        _handlerMock.Setup(h => h.ListarCapitulosActivosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogoPage<CapituloCatalogoDto> { Items = [new CapituloCatalogoDto { Id = 1, Titulo = "Cap 1", Orden = 1 }] });

        _handlerMock.Setup(h => h.ListarCertificacionesActivasAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogoPage<CertificacionCatalogoDto> { Items = [] });

        _handlerMock.Setup(h => h.ListarExperienciasActivasAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogoPage<ExperienciaCatalogoDto> { Items = [] });

        var request = new GenerarPropuestaRequest { CapitulosIds = [99] }; // 99 does not exist in active chapters

        var act = () => service.GenerarAsync("LIC-100", request, "user@tivit.com");

        var ex = await act.Should().ThrowAsync<PropuestaService.PropuestaException>();
        ex.Which.Code.Should().Be("PRO_002");
    }

    [Fact]
    public async Task ActualizarEstadoAsync_TransicionValida_ActualizaYDevuelveDto()
    {
        var service = CreateService();
        _lookupMock.Setup(l => l.ObtenerPorCodigoAsync("LIC-100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicitacionDetalleDto { Id = 10, CodigoExterno = "LIC-100" });

        _handlerMock.Setup(h => h.ObtenerPropuestaAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PropuestaRow
            {
                Id = 5,
                LicitacionId = 10,
                Version = 1,
                Estado = "generada",
                GeneradoPor = "user@tivit.com",
                CapitulosSeleccionados = "[1, 2]",
                CertificacionesIds = "[]",
                ExperienciasIds = "[]",
            });

        _handlerMock.Setup(h => h.ActualizarEstadoPropuestaAsync(5, "enviada", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await service.ActualizarEstadoAsync("LIC-100", 5, "enviada");

        result.PropuestaId.Should().Be(5);
        result.Estado.Should().Be("enviada");
        result.Version.Should().Be(1);
        _handlerMock.Verify(h => h.ActualizarEstadoPropuestaAsync(5, "enviada", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("descartada", "enviada")]
    [InlineData("descartada", "generada")]
    [InlineData("enviada", "generada")]
    [InlineData("generada", "otra_cosa")]
    public async Task ActualizarEstadoAsync_TransicionInvalida_ThrowsPro008(string estadoActual, string nuevoEstado)
    {
        var service = CreateService();
        _lookupMock.Setup(l => l.ObtenerPorCodigoAsync("LIC-100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicitacionDetalleDto { Id = 10, CodigoExterno = "LIC-100" });

        _handlerMock.Setup(h => h.ObtenerPropuestaAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PropuestaRow
            {
                Id = 5,
                LicitacionId = 10,
                Version = 1,
                Estado = estadoActual,
                CapitulosSeleccionados = "[1]",
            });

        var act = () => service.ActualizarEstadoAsync("LIC-100", 5, nuevoEstado);

        var ex = await act.Should().ThrowAsync<PropuestaService.PropuestaException>();
        ex.Which.Code.Should().Be("PRO_008");
    }

    [Fact]
    public async Task ObtenerArchivoAsync_PropuestaDeOtraLicitacion_ThrowsPro001()
    {
        var service = CreateService();
        _lookupMock.Setup(l => l.ObtenerPorCodigoAsync("LIC-100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicitacionDetalleDto { Id = 10, CodigoExterno = "LIC-100" });

        _handlerMock.Setup(h => h.ObtenerPropuestaAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PropuestaRow
            {
                Id = 5,
                LicitacionId = 999, // Different licitacion
                RutaArchivo = "some/path.docx",
            });

        var act = () => service.ObtenerArchivoAsync("LIC-100", 5);

        var ex = await act.Should().ThrowAsync<PropuestaService.PropuestaException>();
        ex.Which.Code.Should().Be("PRO_001");
    }

    [Fact]
    public async Task ExportarDriveAsync_PropuestaValida_ExportaCorrectamente()
    {
        var service = CreateService();
        _lookupMock.Setup(l => l.ObtenerPorCodigoAsync("LIC-100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicitacionDetalleDto { Id = 10, CodigoExterno = "LIC-100" });

        _handlerMock.Setup(h => h.ObtenerPropuestaAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PropuestaRow
            {
                Id = 5,
                LicitacionId = 10,
                Version = 1,
                RutaArchivo = "licitaciones/LIC-100/propuestas/file.docx",
            });

        using var testStream = new MemoryStream([1, 2, 3]);
        _storageMock.Setup(s => s.DownloadAsync("licitaciones/LIC-100/propuestas/file.docx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testStream);

        _driveMock.Setup(d => d.ExportarArchivoAsync(
                "LIC-100", "Propuesta_LIC-100_v1.docx", It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExportarDriveResponse
            {
                DriveFileId = "drv-123",
                WebUrl = "https://drive.google.com/file/d/drv-123/view",
                NombreArchivo = "Propuesta_LIC-100_v1.docx",
                ExportadoAt = DateTime.UtcNow,
            });

        var result = await service.ExportarDriveAsync("LIC-100", 5);

        result.DriveFileId.Should().Be("drv-123");
        result.WebUrl.Should().Contain("drv-123");
        _driveMock.Verify(d => d.ExportarArchivoAsync("LIC-100", "Propuesta_LIC-100_v1.docx", It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
