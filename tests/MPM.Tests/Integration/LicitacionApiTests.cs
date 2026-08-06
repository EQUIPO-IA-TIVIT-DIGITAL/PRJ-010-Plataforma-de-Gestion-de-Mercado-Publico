using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MPM.Tests.Integration;

public class LicitacionApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public LicitacionApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Listar_ReturnsSuccessAndPaginatedResult()
    {
        var response = await _client.GetAsync("/api/v1/licitaciones?pageSize=3");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("success").GetBoolean().Should().BeTrue();

        var data = json.GetProperty("data");
        data.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
        data.GetProperty("items").GetArrayLength().Should().BeLessOrEqualTo(3);
        data.GetProperty("page").GetInt32().Should().Be(1);
        data.GetProperty("pageSize").GetInt32().Should().Be(3);
        data.GetProperty("totalRecords").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Listar_WithSearch_FiltersResults()
    {
        var response = await _client.GetAsync("/api/v1/licitaciones?search=LIC&pageSize=3");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("success").GetBoolean().Should().BeTrue();
        var items = json.GetProperty("data").GetProperty("items");
        items.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task Listar_WithEstadoFilter_FiltersByEstado()
    {
        var response = await _client.GetAsync("/api/v1/licitaciones?estado=5&pageSize=3");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = json.GetProperty("data").GetProperty("items");

        foreach (var item in items.EnumerateArray())
        {
            item.GetProperty("estado").GetProperty("codigo").GetInt32().Should().Be(5);
        }
    }

    [Fact]
    public async Task Listar_Pagination_ReturnsCorrectPage()
    {
        var page1 = await _client.GetAsync("/api/v1/licitaciones?page=1&pageSize=2");
        var page2 = await _client.GetAsync("/api/v1/licitaciones?page=2&pageSize=2");

        page1.StatusCode.Should().Be(HttpStatusCode.OK);
        page2.StatusCode.Should().Be(HttpStatusCode.OK);

        var json1 = await page1.Content.ReadFromJsonAsync<JsonElement>();
        var json2 = await page2.Content.ReadFromJsonAsync<JsonElement>();

        var items1 = json1.GetProperty("data").GetProperty("items");
        var items2 = json2.GetProperty("data").GetProperty("items");

        if (items1.GetArrayLength() > 0 && items2.GetArrayLength() > 0)
        {
            var firstCodigo1 = items1[0].GetProperty("codigoExterno").GetString();
            var firstCodigo2 = items2[0].GetProperty("codigoExterno").GetString();
            firstCodigo1.Should().NotBe(firstCodigo2);
        }
        else
        {
            json1.GetProperty("success").GetBoolean().Should().BeTrue();
            json2.GetProperty("success").GetBoolean().Should().BeTrue();
        }
    }

    [Fact]
    public async Task Buscar_ReturnsResults()
    {
        var response = await _client.GetAsync("/api/v1/licitaciones/buscar?q=LIC&limit=5");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = json.GetProperty("data");
        data.ValueKind.Should().Be(JsonValueKind.Array);

        if (data.GetArrayLength() > 0)
        {
            var first = data[0];
            first.TryGetProperty("codigoExterno", out _).Should().BeTrue();
            first.TryGetProperty("nombre", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Buscar_ShortQuery_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/v1/licitaciones/buscar?q=LI");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ObtenerPorCodigo_Existing_ReturnsDetail()
    {
        var listResponse = await _client.GetAsync("/api/v1/licitaciones?pageSize=1");
        var listJson = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var items = listJson.GetProperty("data").GetProperty("items");

        if (items.GetArrayLength() == 0)
            return; // skip if no data

        var firstCodigo = items[0].GetProperty("codigoExterno").GetString();

        var response = await _client.GetAsync($"/api/v1/licitaciones/{firstCodigo}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("success").GetBoolean().Should().BeTrue();
        json.GetProperty("data").GetProperty("codigoExterno").GetString().Should().Be(firstCodigo);
        json.GetProperty("data").TryGetProperty("estado", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ObtenerPorCodigo_NonExisting_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/v1/licitaciones/NOEXISTE-999-XX99");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Catalogos_ReturnsEstados()
    {
        var response = await _client.GetAsync("/api/v1/catalogos/estados-licitacion");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("success").GetBoolean().Should().BeTrue();
        // Solo 5 estados son reales en Mercado Público (5=Publicada, 6=Cerrada, 7=Desierta,
        // 8=Adjudicada, 15=Revocada, ver V086__Fix_estados_licitacion_codigos_reales.sql) --
        // el umbral de 8 databa de antes de esa limpieza del catálogo.
        json.GetProperty("data").GetArrayLength().Should().BeGreaterOrEqualTo(5);
    }

    [Fact]
    public async Task Auth_Login_WithValidCredentials_ReturnsToken()
    {
        var payload = JsonContent.Create(new { email = "admin@tivit.cl", password = "test123" });
        var response = await _client.PostAsync("/api/v1/auth/login", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("success").GetBoolean().Should().BeTrue();
        json.GetProperty("data").GetProperty("token").GetString().Should().NotBeNullOrEmpty();
        json.GetProperty("data").GetProperty("user").GetProperty("email").GetString().Should().Be("admin@tivit.cl");
    }

    [Fact]
    public async Task Auth_Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        var payload = JsonContent.Create(new { email = "wrong@test.cl", password = "bad" });
        var response = await _client.PostAsync("/api/v1/auth/login", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
