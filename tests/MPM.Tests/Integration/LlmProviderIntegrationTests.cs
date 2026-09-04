using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MPM.Core.SystemConfig;
using MPM.Shared.Services;
using Npgsql;
using Xunit;

namespace MPM.Tests.Integration;

// 033-migracion-qwen-g4 (T022, US1): el wiring completo de la API resuelve el proveedor activo
// por request. Con la tabla system_ai_provider vacÃ­a (reciÃ©n migrada) y sin AI:* en entorno,
// la precedencia BD > env > default entrega gemini/gemini-2.5-pro â€” sin cambios de contrato.
// Requiere DB local (CustomWebApplicationFactory â†’ localhost:5433), igual que los demÃ¡s
// tests de integraciÃ³n del proyecto.
public class LlmProviderIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    private const string TestConnectionString =
        "Host=localhost;Port=5433;Database=mpm;Username=mpm;Password=mpm_password";

    public LlmProviderIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task LimpiarProveedorAsync()
    {
        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM system_ai_provider", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Resolver_ConTablaVacia_YEnvGemini_DevuelveClienteGemini()
    {
        // Estado previo no garantizado en BD viva compartida (otro test puede haber dejado
        // una fila) → se parte de tabla vacía explícitamente.
        await LimpiarProveedorAsync();

        using var scope = _factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<LlmClientResolver>();

        var client = await resolver.GetClientAsync();

        client.Should().BeOfType<VertexGeminiClient>();
        client.ModelName.Should().Be("gemini-2.5-pro");
    }

    [Fact]
    public async Task Resolver_ProveedorActivoEnBD_DevuelveElClienteCorrespondiente()
    {
        // Escribe openai en la tabla (camino del switch del super admin) y verifica que el
        // resolver lo levanta por request. Se limpia en finally para no contaminar otros tests.
        using var scope = _factory.Services.CreateScope();
        var configService = scope.ServiceProvider.GetRequiredService<SystemConfigService>();
        var resolver = scope.ServiceProvider.GetRequiredService<LlmClientResolver>();

        try
        {
            await configService.ActualizarAsync(
                "openai", "http://localhost:8000/v1", "qwen3.7-g4",
                updatedByUserId: 1L, updatedByUsername: "test@tivit.cl");

            var settings = await configService.ObtenerActivoAsync();
            settings.Provider.Should().Be("openai");
            settings.ResolvedFrom.Should().Be("database");

            // Desde 033-migracion-qwen-g4 openai sí tiene cliente (OpenAiCompatClient) y el
            // resolver le aplica endpoint+modelo de la BD.
            var client = await resolver.GetClientAsync();
            client.Should().BeOfType<OpenAiCompatClient>();
            client.ModelName.Should().Be("qwen3.7-g4");
        }
        finally
        {
            await LimpiarProveedorAsync();
        }
    }
}

