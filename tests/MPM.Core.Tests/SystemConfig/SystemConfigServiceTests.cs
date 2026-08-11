using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPM.Core.Data;
using MPM.Core.SystemConfig;
using Xunit;

namespace MPM.Core.Tests.SystemConfig;

// 033-migracion-qwen-g4: precedencia BD > env > default + fallback + invalidaciÃ³n de cache.
public class SystemConfigServiceTests
{
    private static IConfiguration BuildConfig(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value))
            .Build();

    private static SystemConfigService BuildService(
        ISystemConfigData data, IConfiguration config,
        MemoryCache? cache = null)
    {
        return new SystemConfigService(data, config, cache ?? new MemoryCache(new MemoryCacheOptions()), NullLogger<SystemConfigService>.Instance);
    }

    [Fact]
    public async Task ObtenerActivoAsync_UsaBD_CuandoExisteFila()
    {
        var data = new Mock<ISystemConfigData>();
        data.Setup(d => d.ObtenerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiProviderRow("openai", "http://qwen:8000/v1", "qwen3.7-g4", 1L, "admin@tivit.cl", DateTime.UtcNow));

        var service = BuildService(data.Object, BuildConfig(("AI:Provider", "gemini")));

        var settings = await service.ObtenerActivoAsync();

        settings.Provider.Should().Be("openai");
        settings.Model.Should().Be("qwen3.7-g4");
        settings.Endpoint.Should().Be("http://qwen:8000/v1");
        settings.ResolvedFrom.Should().Be("database");
    }

    [Fact]
    public async Task ObtenerActivoAsync_UsaEnv_CuandoBDNoTieneFila()
    {
        var data = new Mock<ISystemConfigData>();
        data.Setup(d => d.ObtenerAsync(It.IsAny<CancellationToken>())).ReturnsAsync((AiProviderRow?)null);

        var service = BuildService(data.Object, BuildConfig(
            ("AI:Provider", "openai"),
            ("AI:Endpoint", "http://localhost:8000/v1"),
            ("AI:Model", "qwen3.7-g4")));

        var settings = await service.ObtenerActivoAsync();

        settings.Provider.Should().Be("openai");
        settings.Model.Should().Be("qwen3.7-g4");
        settings.ResolvedFrom.Should().Be("environment");
    }

    [Fact]
    public async Task ObtenerActivoAsync_UsaDefaultGemini_CuandoNoHayNada()
    {
        var data = new Mock<ISystemConfigData>();
        data.Setup(d => d.ObtenerAsync(It.IsAny<CancellationToken>())).ReturnsAsync((AiProviderRow?)null);

        var service = BuildService(data.Object, BuildConfig());

        var settings = await service.ObtenerActivoAsync();

        settings.Provider.Should().Be("gemini");
        settings.Model.Should().Be("gemini-2.5-pro");
        settings.ResolvedFrom.Should().Be("environment");
    }

    [Fact]
    public async Task ObtenerActivoAsync_FallaABD_UsaEnvComoFallback()
    {
        var data = new Mock<ISystemConfigData>();
        data.Setup(d => d.ObtenerAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("BD caida"));

        var service = BuildService(data.Object, BuildConfig(("AI:Provider", "gemini")));

        var settings = await service.ObtenerActivoAsync();

        settings.Provider.Should().Be("gemini");
        settings.ResolvedFrom.Should().Be("environment");
    }

    [Fact]
    public async Task ObtenerActivoAsync_CacheaResultado_DentroDelTtl()
    {
        var data = new Mock<ISystemConfigData>();
        data.Setup(d => d.ObtenerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiProviderRow("gemini", null, "gemini-2.5-pro", 1L, "admin@tivit.cl", DateTime.UtcNow));

        var service = BuildService(data.Object, BuildConfig(("AI:Provider", "openai")));

        _ = await service.ObtenerActivoAsync();
        _ = await service.ObtenerActivoAsync();
        _ = await service.ObtenerActivoAsync();

        // Solo una consulta a BD dentro del TTL de 30s.
        data.Verify(d => d.ObtenerAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActualizarAsync_PersisteEInvalidaCache()
    {
        var data = new Mock<ISystemConfigData>();
        data.Setup(d => d.ObtenerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiProviderRow("gemini", null, "gemini-2.5-pro", 1L, "admin@tivit.cl", DateTime.UtcNow));
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = BuildService(data.Object, BuildConfig(("AI:Provider", "gemini")), cache);

        _ = await service.ObtenerActivoAsync(); // carga y cachea gemini

        var actualizado = await service.ActualizarAsync("openai", "http://qwen:8000/v1", "qwen3.7-g4", 1L, "admin@tivit.cl");

        actualizado.ResolvedFrom.Should().Be("database");
        actualizado.Provider.Should().Be("openai");

        // La cache fue invalidada: la siguiente lectura vuelve a consultar BD y ve el nuevo valor.
        data.Setup(d => d.ObtenerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiProviderRow("openai", "http://qwen:8000/v1", "qwen3.7-g4", 1L, "admin@tivit.cl", DateTime.UtcNow));
        var releido = await service.ObtenerActivoAsync();
        releido.Provider.Should().Be("openai");

        data.Verify(d => d.ActualizarAsync("openai", "http://qwen:8000/v1", "qwen3.7-g4",
            It.IsAny<long>(), "admin@tivit.cl", It.IsAny<CancellationToken>()), Times.Once);
    }
}

