using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MPM.Tests.Integration;

public class MensajeriaApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public MensajeriaApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> GetAuthTokenAsync()
    {
        var payload = JsonContent.Create(new { email = "admin@tivit.cl", password = "test123" });
        var response = await _client.PostAsync("/api/v1/auth/login", payload);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("data").GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task ListarConversaciones_SinAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/conversaciones");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListarConversaciones_ConAuth_ReturnsSuccess()
    {
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/v1/conversaciones?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("success").GetBoolean().Should().BeTrue();
        json.GetProperty("data").GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task CrearConversacion_SinParticipantes_ReturnsBadRequest()
    {
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var payload = JsonContent.Create(new { tipo = "directo", asunto = (string?)null, licitacionId = (long?)null, participanteIds = new string[] { } });
        var response = await _client.PostAsync("/api/v1/conversaciones", payload);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CrearConversacion_Directa_ReturnsCreated()
    {
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var payload = JsonContent.Create(new { tipo = "directo", asunto = (string?)null, licitacionId = (long?)null, participanteIds = new[] { $"user-{uniqueSuffix}" } });
        var response = await _client.PostAsync("/api/v1/conversaciones", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("success").GetBoolean().Should().BeTrue();
        json.GetProperty("data").GetProperty("id").GetInt64().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ObtenerConversacion_NoExiste_ReturnsNotFound()
    {
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/v1/conversaciones/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EnviarMensaje_SinAuth_ReturnsUnauthorized()
    {
        var payload = JsonContent.Create(new { tipo = "texto", contenido = "Hola", replyToId = (long?)null });
        var response = await _client.PostAsync("/api/v1/conversaciones/1/mensajes", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EnviarMensaje_TextoVacio_ReturnsBadRequest()
    {
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var payload = JsonContent.Create(new { tipo = "texto", contenido = "", replyToId = (long?)null });
        var response = await _client.PostAsync("/api/v1/conversaciones/1/mensajes", payload);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ObtenerPresencia_SinUserIds_ReturnsBadRequest()
    {
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/v1/presencia");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ObtenerPresencia_ConUserIds_ReturnsSuccess()
    {
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/v1/presencia?userIds=user-1,user-2");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("success").GetBoolean().Should().BeTrue();
        json.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task NotificarTyping_ReturnsSuccess()
    {
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var payload = JsonContent.Create(new { conversacionId = 1L, escribiendo = true });
        var response = await _client.PostAsync("/api/v1/presencia/typing", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
