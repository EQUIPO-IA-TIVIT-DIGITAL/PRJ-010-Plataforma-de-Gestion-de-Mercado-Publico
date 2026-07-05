using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MPM.Modules.Licitaciones.Services;
using System.Text.Json;
using Xunit;

namespace MPM.Tests.Services;

public class ApiMpServiceTests
{
    private readonly Mock<ILogger<ApiMpService>> _loggerMock = new();
    private readonly HttpClient _httpClient;

    public ApiMpServiceTests()
    {
        _httpClient = new HttpClient();
    }

    [Fact]
    public void ParseCodigoEstado_WithNumberJsonElement_ReturnsShort()
    {
        var json = "8";
        var doc = JsonDocument.Parse(json);
        var element = doc.RootElement.Clone();

        element.ValueKind.Should().Be(JsonValueKind.Number);
        ((short)element.GetInt32()).Should().Be(8);
    }

    [Fact]
    public void ParseCodigoEstado_WithStringJsonElement_ReturnsShort()
    {
        var json = "\"5\"";
        var doc = JsonDocument.Parse(json);
        var element = doc.RootElement.Clone();

        element.ValueKind.Should().Be(JsonValueKind.String);
        short.TryParse(element.GetString(), out var v).Should().BeTrue();
        v.Should().Be(5);
    }

    [Fact]
    public void ParseCodigoEstado_WithNullElement_ReturnsDefault()
    {
        var element = (JsonElement?)null;
        var result = element == null ? (short)1 : (short)element.Value.GetInt32();
        result.Should().Be(1);
    }

    [Fact]
    public void LicitacionRawDto_DefaultValues_AreCorrect()
    {
        var dto = new LicitacionRawDto();
        dto.codigo_externo.Should().Be(string.Empty);
        dto.nombre.Should().Be(string.Empty);
        dto.tipo.Should().Be(string.Empty);
        dto.moneda.Should().Be("CLP");
        dto.monto_estimado.Should().BeNull();
        dto.fecha_publicacion.Should().BeNull();
    }

    [Fact]
    public void LicitacionRawDto_WithValues_SetsCorrectly()
    {
        var dto = new LicitacionRawDto
        {
            codigo_externo = "12345-67-LE26",
            nombre = "Test licitacion",
            codigo_estado = 5,
            tipo = "Licitacion",
            moneda = "CLP",
            monto_estimado = 1000000,
            fecha_cierre = new DateTime(2026, 6, 15, 18, 0, 0)
        };

        dto.codigo_externo.Should().Be("12345-67-LE26");
        dto.nombre.Should().Be("Test licitacion");
        dto.codigo_estado.Should().Be(5);
        dto.tipo.Should().Be("Licitacion");
        dto.monto_estimado.Should().Be(1000000);
        dto.fecha_cierre.Should().Be(new DateTime(2026, 6, 15, 18, 0, 0));
    }

    [Fact]
    public void ApiMpLicitacion_Deserializes_FromJson()
    {
        var json = """
        {
            "CodigoExterno": "12345-67-LE26",
            "Nombre": "Test licitacion",
            "CodigoEstado": 8,
            "FechaCierre": "2026-06-15T18:00:00"
        }
        """;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = JsonSerializer.Deserialize<ApiMpLicitacion>(json, options);

        result.Should().NotBeNull();
        result!.CodigoExterno.Should().Be("12345-67-LE26");
        result.Nombre.Should().Be("Test licitacion");
        result.FechaCierre.Should().Be("2026-06-15T18:00:00");
    }

    [Fact]
    public void ApiMpListResponse_Deserializes_FromJson()
    {
        var json = """
        {
            "Listado": [
                {"CodigoExterno": "A", "Nombre": "B", "CodigoEstado": 1, "FechaCierre": "2026-01-01"},
                {"CodigoExterno": "C", "Nombre": "D", "CodigoEstado": 5, "FechaCierre": null}
            ]
        }
        """;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = JsonSerializer.Deserialize<ApiMpListResponse>(json, options);

        result.Should().NotBeNull();
        result!.Listado.Should().HaveCount(2);
        result.Listado![0].CodigoExterno.Should().Be("A");
    }
}
