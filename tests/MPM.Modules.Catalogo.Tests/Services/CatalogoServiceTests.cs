using MPM.Modules.Catalogo.Data;
using MPM.Modules.Catalogo.Models;
using MPM.Modules.Catalogo.Services;
using Moq;
using FluentAssertions;
using Xunit;

namespace MPM.Modules.Catalogo.Tests.Services;

public class CatalogoServiceTests
{
    private readonly Mock<ICatalogoHandler> _handlerMock;
    private readonly CatalogoService _service;

    public CatalogoServiceTests()
    {
        _handlerMock = new Mock<ICatalogoHandler>(MockBehavior.Strict);
        _service = new CatalogoService(_handlerMock.Object);
    }

    [Fact]
    public async Task GetEstadosAsync_CallsHandler()
    {
        var expected = new List<EstadoItemDto>
        {
            new() { Codigo = 5, Nombre = "Adjudicada" },
            new() { Codigo = 8, Nombre = "Desierta" }
        };
        _handlerMock.Setup(h => h.GetEstadosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetEstadosAsync();

        result.Should().HaveCount(2);
        result[0].Codigo.Should().Be(5);
        _handlerMock.Verify(h => h.GetEstadosAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTiposLicitacionAsync_CallsHandler()
    {
        var expected = new List<TipoLicitacionItemDto>
        {
            new() { Codigo = 1, Nombre = "Licitación Pública", Slug = "publica" }
        };
        _handlerMock.Setup(h => h.GetTiposLicitacionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetTiposLicitacionAsync();

        result.Should().HaveCount(1);
        _handlerMock.Verify(h => h.GetTiposLicitacionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMonedasAsync_CallsHandler()
    {
        var expected = new List<MonedaItemDto>
        {
            new() { Codigo = 1, Nombre = "Peso Chileno", Simbolo = "$", CodigoIso = "CLP" }
        };
        _handlerMock.Setup(h => h.GetMonedasAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetMonedasAsync();

        result.Should().HaveCount(1);
        _handlerMock.Verify(h => h.GetMonedasAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_CallsHandler()
    {
        var expected = new CatalogosResponseDto
        {
            EstadosLicitacion = [new EstadoItemDto { Codigo = 5, Nombre = "Adjudicada" }],
            TiposLicitacion = [],
            Monedas = []
        };
        _handlerMock.Setup(h => h.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetAllAsync();

        result.Should().NotBeNull();
        result.EstadosLicitacion.Should().HaveCount(1);
        _handlerMock.Verify(h => h.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}