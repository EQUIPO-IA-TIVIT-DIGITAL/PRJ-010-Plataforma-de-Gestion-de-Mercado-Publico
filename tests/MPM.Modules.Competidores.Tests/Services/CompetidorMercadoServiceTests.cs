using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPM.Core.Data;
using MPM.Modules.Competidores.Data;
using MPM.Modules.Competidores.Models;
using MPM.Modules.Competidores.Services;
using Xunit;

namespace MPM.Modules.Competidores.Tests.Services;

/// <summary>spec 031 (US4): get-or-generate de actividad de mercado -- cache-hit no dispara
/// scraping, cache-miss encola pero no reencola si ya está "generando" (evita duplicar costo).
/// No monta un proceso Node real; solo verifica el contrato de EncolarAsync/ObtenerCacheAsync.</summary>
public class CompetidorMercadoServiceTests
{
    private readonly Mock<CompetidoresActividadMercadoHandler> _handlerMock;
    private readonly CompetidorMercadoService _service;

    public CompetidorMercadoServiceTests()
    {
        var dbFactory = new DbConnectionFactory("Host=localhost;Database=unused");
        _handlerMock = new Mock<CompetidoresActividadMercadoHandler>(dbFactory);
        _service = new CompetidorMercadoService(
            NullLogger<CompetidorMercadoService>.Instance,
            new ConfigurationBuilder().Build(),
            _handlerMock.Object);
    }

    private static readonly ActividadMercadoRequest Request = new(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 31));

    [Fact]
    public async Task ObtenerOGenerarAsync_CacheListo_DevuelveCacheado_NoEncola()
    {
        _handlerMock.Setup(h => h.ObtenerCacheAsync("Telefónica", 1, Request.FechaDesde, Request.FechaHasta, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActividadMercadoCacheRow("listo", 58, 145_000_000_000m, "{\"licitaciones\":[]}", DateTime.UtcNow));

        var result = await _service.ObtenerOGenerarAsync("Telefónica", Request);

        result.Estado.Should().Be("listo");
        result.CantidadLicitaciones.Should().Be(58);
        _handlerMock.Verify(h => h.EncolarAsync(It.IsAny<string>(), It.IsAny<short?>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ObtenerOGenerarAsync_CacheGenerando_DevuelveGenerando_NoReencola()
    {
        _handlerMock.Setup(h => h.ObtenerCacheAsync("Telefónica", 1, Request.FechaDesde, Request.FechaHasta, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActividadMercadoCacheRow("generando", null, null, null, null));

        var result = await _service.ObtenerOGenerarAsync("Telefónica", Request);

        result.Estado.Should().Be("generando");
        _handlerMock.Verify(h => h.EncolarAsync(It.IsAny<string>(), It.IsAny<short?>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never,
            "si ya está 'generando' no debe volver a encolar ni disparar un segundo proceso de scraping (evita duplicar costo)");
    }

    [Fact]
    public async Task ObtenerOGenerarAsync_SinCache_Encola()
    {
        _handlerMock.Setup(h => h.ObtenerCacheAsync("Telefónica", 1, Request.FechaDesde, Request.FechaHasta, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActividadMercadoCacheRow?)null);
        _handlerMock.Setup(h => h.ObtenerPalabrasClaveAreaAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "cloud", "nube" });

        var result = await _service.ObtenerOGenerarAsync("Telefónica", Request);

        result.Estado.Should().Be("generando");
        _handlerMock.Verify(h => h.EncolarAsync("Telefónica", 1, Request.FechaDesde, Request.FechaHasta, It.IsAny<CancellationToken>()), Times.Once);
    }
}
