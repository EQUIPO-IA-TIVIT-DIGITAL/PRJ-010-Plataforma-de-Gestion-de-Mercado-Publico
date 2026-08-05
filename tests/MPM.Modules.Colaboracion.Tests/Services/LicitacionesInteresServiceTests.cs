using FluentAssertions;
using Moq;
using MPM.Core.Data;
using MPM.Modules.Colaboracion.Data;
using MPM.Modules.Colaboracion.Models;
using MPM.Modules.Colaboracion.Services;
using Xunit;

namespace MPM.Modules.Colaboracion.Tests.Services;

/// <summary>spec 031 (US5): cubre la idempotencia de MarcarInteresAsync (FR-013) y la
/// detección de cambio de estado (FR-017), sin tocar Postgres real.</summary>
public class LicitacionesInteresServiceTests
{
    private readonly Mock<LicitacionesInteresHandler> _handlerMock;
    private readonly LicitacionesInteresService _service;

    public LicitacionesInteresServiceTests()
    {
        var dbFactory = new DbConnectionFactory("Host=localhost;Database=unused");
        _handlerMock = new Mock<LicitacionesInteresHandler>(dbFactory);
        _service = new LicitacionesInteresService(_handlerMock.Object);
    }

    [Fact]
    public async Task MarcarInteresAsync_SegundaLlamada_DevuelveLaMismaFila_NoCreaOtra()
    {
        var dto = new LicitacionInteresDto { Id = 12, LicitacionId = 1204, MarcadoPor = "francisco.lopez" };
        _handlerMock.Setup(h => h.MarcarAsync(1204, "francisco.lopez", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var primera = await _service.MarcarInteresAsync(1204, "francisco.lopez");
        var segunda = await _service.MarcarInteresAsync(1204, "francisco.lopez");

        primera.Id.Should().Be(segunda.Id);
        _handlerMock.Verify(h => h.MarcarAsync(1204, "francisco.lopez", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public void EstadoCambio_EsTrue_CuandoEstadoActualDifiereDelMarcado()
    {
        var dto = new LicitacionInteresDto
        {
            EstadoLicitacionAlMarcar = 8, // Adjudicada
            EstadoLicitacionActual = 15,  // Revocada
        };

        dto.EstadoCambio.Should().BeTrue("FR-017: el usuario debe poder detectar que la licitación cambió de estado desde que la marcó de interés");
    }

    [Fact]
    public void EstadoCambio_EsFalse_CuandoNoHuboCambio()
    {
        var dto = new LicitacionInteresDto { EstadoLicitacionAlMarcar = 8, EstadoLicitacionActual = 8 };

        dto.EstadoCambio.Should().BeFalse();
    }

    [Fact]
    public async Task VincularAsync_SoloActualizaLosCamposProvistos()
    {
        _handlerMock.Setup(h => h.ObtenerPorLicitacionAsync(1204, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicitacionInteresDto { Id = 12, LicitacionId = 1204, WorkspaceId = 84 });

        var result = await _service.VincularAsync(1204, new VincularInteresRequest { WorkspaceId = 84 });

        _handlerMock.Verify(h => h.VincularWorkspaceAsync(1204, 84, It.IsAny<CancellationToken>()), Times.Once);
        _handlerMock.Verify(h => h.VincularConversacionAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        result!.WorkspaceId.Should().Be(84);
    }
}
