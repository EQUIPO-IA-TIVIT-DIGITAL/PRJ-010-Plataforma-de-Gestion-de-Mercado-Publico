using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace MPM.Tests.Integration;

public class PropuestasApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public PropuestasApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> GetAuthTokenAsync(string email = "admin@tivit.cl", string password = "test123")
    {
        var payload = JsonContent.Create(new { email, password });
        var response = await _client.PostAsync("/api/v1/auth/login", payload);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("data").GetProperty("token").GetString()!;
    }

    private async Task AuthenticateAsync(string email = "admin@tivit.cl", string password = "test123")
    {
        var token = await GetAuthTokenAsync(email, password);
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task Catalogos_SinAuth_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var resCapitulos = await _client.GetAsync("/api/v1/propuestas/catalogos/capitulos");
        var resCertificaciones = await _client.GetAsync("/api/v1/propuestas/catalogos/certificaciones");
        var resExperiencias = await _client.GetAsync("/api/v1/propuestas/catalogos/experiencias");

        resCapitulos.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        resCertificaciones.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        resExperiencias.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Catalogos_ConAuth_ReturnsOk()
    {
        await AuthenticateAsync();

        var resCapitulos = await _client.GetAsync("/api/v1/propuestas/catalogos/capitulos?page=1&size=10");
        resCapitulos.StatusCode.Should().Be(HttpStatusCode.OK);

        var jsonCap = await resCapitulos.Content.ReadFromJsonAsync<JsonElement>();
        jsonCap.GetProperty("success").GetBoolean().Should().BeTrue();
        jsonCap.GetProperty("data").GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task Recomendaciones_SinAuth_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var payload = JsonContent.Create(new { codigoExterno = "LIC-TEST" });
        var response = await _client.PostAsync("/api/v1/propuestas/recomendaciones", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GenerarPropuesta_SinAuth_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var payload = JsonContent.Create(new { capitulosIds = new[] { 1, 2 } });
        var response = await _client.PostAsync("/api/v1/licitaciones/LIC-TEST/propuestas/generar", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GenerarPropuesta_LicitacionInexistente_ReturnsNotFoundOrError()
    {
        await AuthenticateAsync();

        var payload = JsonContent.Create(new { capitulosIds = new[] { 1, 2 } });
        var response = await _client.PostAsync("/api/v1/licitaciones/LICITACION-INEXISTENTE-99999/propuestas/generar", payload);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.UnprocessableEntity);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("success").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task AvisarDecision_SinAuth_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var payload = JsonContent.Create(new { destinatarios = new[] { "persona@tivit.com" } });
        var response = await _client.PostAsync("/api/v1/licitaciones/LIC-TEST/decision/1/avisar", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AvisarDecision_DestinatariosVacios_ReturnsUnprocessableEntity()
    {
        await AuthenticateAsync();

        var payload = JsonContent.Create(new { destinatarios = Array.Empty<string>() });
        var response = await _client.PostAsync("/api/v1/licitaciones/LIC-TEST/decision/1/avisar", payload);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("success").GetBoolean().Should().BeFalse();
        GetErrorCode(json).Should().Be("PRO_007");
    }

    [Fact]
    public async Task AvisarDecision_EmailInvalido_ReturnsUnprocessableEntity()
    {
        await AuthenticateAsync();

        var payload = JsonContent.Create(new { destinatarios = new[] { "no-es-un-email-valido" } });
        var response = await _client.PostAsync("/api/v1/licitaciones/LIC-TEST/decision/1/avisar", payload);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("success").GetBoolean().Should().BeFalse();
        GetErrorCode(json).Should().Be("PRO_007");
    }

    private static string? GetErrorCode(JsonElement json)
    {
        // El API devuelve errores de validación como arreglo `errors`; se acepta también
        // el objeto singular `error` (misma tolerancia que el E2E de propuestas).
        if (json.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0
            && errors[0].TryGetProperty("code", out var codeFromArray))
            return codeFromArray.GetString();
        if (json.TryGetProperty("error", out var error) && error.TryGetProperty("code", out var codeFromObject))
            return codeFromObject.GetString();
        return null;
    }

    [Fact]
    public async Task HistorialPropuestas_SinAuth_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/v1/licitaciones/LIC-TEST/propuestas");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DescargarArchivo_SinAuth_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/v1/licitaciones/LIC-TEST/propuestas/1/archivo");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ActualizarEstado_SinAuth_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var payload = JsonContent.Create(new { estado = "enviada" });
        var response = await _client.PatchAsync("/api/v1/licitaciones/LIC-TEST/propuestas/1/estado", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExportarDrive_SinAuth_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsync("/api/v1/licitaciones/LIC-TEST/propuestas/1/exportar-drive", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
