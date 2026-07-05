using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MPM.Tests.Integration;

public class NotificacionesApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public NotificacionesApiTests(CustomWebApplicationFactory factory)
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

    private async Task AuthenticateAsync()
    {
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task Eliminar_SinAuth_ReturnsUnauthorized()
    {
        var response = await _client.DeleteAsync("/api/v1/notificaciones/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EliminarTodas_SinAuth_ReturnsUnauthorized()
    {
        var response = await _client.DeleteAsync("/api/v1/notificaciones");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Eliminar_NotificacionInexistente_ReturnsNotFound()
    {
        await AuthenticateAsync();

        var response = await _client.DeleteAsync("/api/v1/notificaciones/999999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EliminarTodas_ConAuth_ReturnsOkConCantidad()
    {
        await AuthenticateAsync();

        var response = await _client.DeleteAsync("/api/v1/notificaciones");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("success").GetBoolean().Should().BeTrue();
        json.GetProperty("data").GetProperty("eliminadas").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Listar_TrasEliminarTodas_NoIncluyeEliminadas()
    {
        await AuthenticateAsync();

        await _client.DeleteAsync("/api/v1/notificaciones");

        var response = await _client.GetAsync("/api/v1/notificaciones?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("data").GetProperty("totalRecords").GetInt32().Should().Be(0);
    }
}
