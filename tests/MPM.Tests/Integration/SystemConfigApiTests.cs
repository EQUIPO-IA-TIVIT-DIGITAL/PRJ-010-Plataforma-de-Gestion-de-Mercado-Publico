using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MPM.Tests.Integration;

// 033-migracion-qwen-g4 (US4): switch del super admin — GET/PUT /api/system/ai-provider.
// Requiere DB local (CustomWebApplicationFactory → localhost:5433), igual que los demás
// tests de integración del proyecto.
public class SystemConfigApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public SystemConfigApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> LoginAsync(string email)
    {
        var payload = JsonContent.Create(new { email, password = "test123" });
        var response = await _client.PostAsync("/api/v1/auth/login", payload);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("data").GetProperty("token").GetString()!;
    }

    private async Task AuthenticateAsync(string email = "admin@tivit.cl")
    {
        var token = await LoginAsync(email);
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task Obtener_SinAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/system/ai-provider");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Obtener_SinRolSuperAdmin_ReturnsForbidden()
    {
        // analista@tivit.cl tiene rol Analista (seed V042) — no SuperAdmin.
        await AuthenticateAsync("analista@tivit.cl");

        var response = await _client.GetAsync("/api/system/ai-provider");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Actualizar_SinRolSuperAdmin_ReturnsForbidden()
    {
        await AuthenticateAsync("analista@tivit.cl");

        var response = await _client.PutAsJsonAsync("/api/system/ai-provider",
            new { provider = "openai", endpoint = "http://qwen:8000/v1", model = "qwen3.7-g4" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Obtener_ConSuperAdmin_ReturnsEstadoActual()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync("/api/system/ai-provider");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("success").GetBoolean().Should().BeTrue();
        var data = json.GetProperty("data");
        data.GetProperty("provider").GetString().Should().Be("gemini");
        data.GetProperty("resolvedFrom").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Actualizar_ConSuperAdmin_CambiaProveedorYPersiste()
    {
        await AuthenticateAsync();

        // gcloud primero (restaurar estado conocido)
        var reset = await _client.PutAsJsonAsync("/api/system/ai-provider",
            new { provider = "gemini", model = "gemini-2.5-pro" });
        reset.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.PutAsJsonAsync("/api/system/ai-provider",
            new { provider = "openai", endpoint = "http://qwen.tivit.internal/v1", model = "qwen3.7-g4" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = json.GetProperty("data");
        data.GetProperty("provider").GetString().Should().Be("openai");
        data.GetProperty("model").GetString().Should().Be("qwen3.7-g4");
        data.GetProperty("endpoint").GetString().Should().Be("http://qwen.tivit.internal/v1");
        data.GetProperty("resolvedFrom").GetString().Should().Be("database");
        data.GetProperty("updatedByUsername").GetString().Should().Be("admin@tivit.cl");

        // El GET lo refleja (persistido, no solo en memoria)
        var getResponse = await _client.GetAsync("/api/system/ai-provider");
        var getJson = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        getJson.GetProperty("data").GetProperty("provider").GetString().Should().Be("openai");

        // Restaurar gcloud para no dejar el ambiente de tests en openai
        var restore = await _client.PutAsJsonAsync("/api/system/ai-provider",
            new { provider = "gemini", model = "gemini-2.5-pro" });
        restore.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Actualizar_ProviderInvalido_ReturnsBadRequest()
    {
        await AuthenticateAsync();

        var response = await _client.PutAsJsonAsync("/api/system/ai-provider",
            new { provider = "claude", model = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("message").GetString()!.Should().Contain("INVALID_PROVIDER");
    }

    [Fact]
    public async Task Actualizar_OpenaiSinEndpoint_ReturnsBadRequest()
    {
        await AuthenticateAsync();

        var response = await _client.PutAsJsonAsync("/api/system/ai-provider",
            new { provider = "openai", model = "qwen3.7-g4" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("message").GetString()!.Should().Contain("INVALID_ENDPOINT");
    }
}
