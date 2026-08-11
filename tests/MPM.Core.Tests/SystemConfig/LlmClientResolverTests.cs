using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPM.Core.Data;
using MPM.Core.SystemConfig;
using MPM.Shared.Services;
using Xunit;

namespace MPM.Core.Tests.SystemConfig;

// 033-migracion-qwen-g4: el resolver elige el ILlmClient registrado por key segÃºn el proveedor activo.
public class LlmClientResolverTests
{
    private sealed class FakeLlmClient(string modelName) : ILlmClient
    {
        public string ModelName { get; } = modelName;
        public Task<LlmResult> GenerarContenidoAsync(LlmRequest request, CancellationToken ct = default)
            => Task.FromResult(new LlmResult("ok", "{}", new LlmUsage()));
    }

    private static LlmClientResolver BuildResolver(
        string provider, params string[] registeredKeys)
    {
        var services = new ServiceCollection();
        foreach (var key in registeredKeys)
            services.AddKeyedScoped<ILlmClient>(key, (_, k) => new FakeLlmClient(k!.ToString()!));

        var data = new Mock<ISystemConfigData>();
        data.Setup(d => d.ObtenerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiProviderRow(provider, "http://qwen:8000/v1", "qwen3.7-g4", 1L, "admin@tivit.cl", DateTime.UtcNow));

        var configService = new SystemConfigService(
            data.Object,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<SystemConfigService>.Instance);

        return new LlmClientResolver(services.BuildServiceProvider(), configService, NullLogger<LlmClientResolver>.Instance);
    }

    [Fact]
    public async Task GetClientAsync_DevuelveClienteGemini_CuandoProveedorActivoEsGemini()
    {
        var resolver = BuildResolver("gemini", "gemini");

        var client = await resolver.GetClientAsync();

        client.Should().NotBeNull();
        client.ModelName.Should().Be("gemini");
    }

    [Fact]
    public async Task GetClientAsync_DevuelveClienteOpenAi_CuandoProveedorActivoEsOpenai()
    {
        var resolver = BuildResolver("openai", "gemini", "openai");

        var client = await resolver.GetClientAsync();

        client.Should().NotBeNull();
        client.ModelName.Should().Be("openai");
    }

    [Fact]
    public async Task GetClientAsync_ProveedorNoRegistrado_LanzaErrorClaro()
    {
        var resolver = BuildResolver("gemini", "openai"); // gemini activo pero solo openai registrado

        var act = () => resolver.GetClientAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*registrado en el sistema*");
    }

    [Fact]
    public async Task GetClientAsync_ProveedorDesconocido_LanzaErrorClaro()
    {
        var resolver = BuildResolver("desconocido", "gemini", "openai");

        var act = () => resolver.GetClientAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

