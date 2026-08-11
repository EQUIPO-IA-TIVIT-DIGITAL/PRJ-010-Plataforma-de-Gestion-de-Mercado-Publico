using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPM.Core.SystemConfig;
using MPM.Modules.Licitaciones.Models;
using MPM.Modules.Licitaciones.Services;
using MPM.Shared.Services;
using Xunit;

namespace MPM.Modules.Licitaciones.Tests.Services;

public class ConsultaSemanticaServiceTests
{
    private const string TestToken = "fake-adc-token";

    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GOOGLE_CLOUD_PROJECT"] = "tivit-cu010",
                ["Vertex:Region"] = "us-central1",
            })
            .Build();

    private static GoogleAdcTokenProvider FakeTokenProvider()
    {
        var mock = new Mock<GoogleAdcTokenProvider>();
        mock.Setup(m => m.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(TestToken);
        return mock.Object;
    }

    // 033-migracion-qwen-g4: ConsultaSemanticaService resuelve el cliente de IA activo vía
    // LlmClientResolver (mockeado para devolver el VertexGeminiClient real con el mismo
    // HttpClient fake, preservando la cobertura de parseo del request/response).
    private static ConsultaSemanticaService BuildService(string responseBody, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new StubHttpMessageHandler(responseBody, status);
        var httpClient = new HttpClient(handler);
        var vertexClient = new VertexGeminiClient(httpClient, BuildConfig(), FakeTokenProvider(), NullLogger<VertexGeminiClient>.Instance);
        var resolver = new Mock<LlmClientResolver>(null!, null!, NullLogger<LlmClientResolver>.Instance);
        resolver.Setup(r => r.GetClientAsync(It.IsAny<CancellationToken>())).ReturnsAsync(vertexClient);
        return new ConsultaSemanticaService(resolver.Object, NullLogger<ConsultaSemanticaService>.Instance);
    }

    [Fact]
    public async Task InterpretarAsync_ReturnsNull_WhenResolverFails()
    {
        // La búsqueda degrada a texto literal si el proveedor de IA falla (FR-005).
        var handler = new StubHttpMessageHandler("{}", HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var vertexClient = new VertexGeminiClient(httpClient, BuildConfig(), FakeTokenProvider(), NullLogger<VertexGeminiClient>.Instance);
        var resolver = new Mock<LlmClientResolver>(null!, null!, NullLogger<LlmClientResolver>.Instance);
        resolver.Setup(r => r.GetClientAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("proveedor no disponible"));
        var service = new ConsultaSemanticaService(resolver.Object, NullLogger<ConsultaSemanticaService>.Instance);

        var result = await service.InterpretarAsync("ciberseguridad para el sector salud");

        result.Should().BeNull();
    }

    [Fact]
    public async Task InterpretarAsync_ParsesFullInterpretation()
    {
        var geminiText = """{"terminosExpandidos": ["SOC", "seguridad de la información"], "estadoInferido": 8, "montoDesde": 10000000, "montoHasta": null, "fechaDesde": null, "fechaHasta": null, "confianza": "alta"}""";
        var responseJson = WrapAsGeminiResponse(geminiText);
        var service = BuildService(responseJson);

        var result = await service.InterpretarAsync("ciberseguridad adjudicadas mayores a 10 millones");

        result.Should().NotBeNull();
        result!.Confianza.Should().Be(ConfianzaInterpretacion.Alta);
        result.TerminosExpandidos.Should().Contain("SOC");
        result.EstadoInferido.Should().Be((short)8);
        result.MontoDesde.Should().Be(10000000);
        result.MontoHasta.Should().BeNull();
    }

    [Fact]
    public async Task InterpretarAsync_ParsesResponse_WhenWrappedInMarkdownFences()
    {
        var geminiText = "```json\n{\"terminosExpandidos\": [\"nube\"], \"estadoInferido\": null, \"montoDesde\": null, \"montoHasta\": null, \"fechaDesde\": null, \"fechaHasta\": null, \"confianza\": \"alta\"}\n```";
        var responseJson = WrapAsGeminiResponse(geminiText);
        var service = BuildService(responseJson);

        var result = await service.InterpretarAsync("cloud computing");

        result.Should().NotBeNull();
        result!.TerminosExpandidos.Should().Contain("nube");
    }

    [Fact]
    public async Task InterpretarAsync_ReturnsLowConfidence_WhenQueryIsAmbiguous()
    {
        var geminiText = """{"terminosExpandidos": [], "estadoInferido": null, "montoDesde": null, "montoHasta": null, "fechaDesde": null, "fechaHasta": null, "confianza": "baja"}""";
        var responseJson = WrapAsGeminiResponse(geminiText);
        var service = BuildService(responseJson);

        var result = await service.InterpretarAsync("asdf qwerty 1234");

        result.Should().NotBeNull();
        result!.Confianza.Should().Be(ConfianzaInterpretacion.Baja);
    }

    [Fact]
    public async Task InterpretarAsync_ReturnsNull_OnHttpError()
    {
        var service = BuildService("""{"error":{"message":"invalid credentials"}}""", HttpStatusCode.Unauthorized);

        var result = await service.InterpretarAsync("cualquier consulta");

        result.Should().BeNull();
    }

    [Fact]
    public async Task InterpretarAsync_ReturnsNull_OnMalformedJson()
    {
        var responseJson = WrapAsGeminiResponse("esto no es json valido {{{");
        var service = BuildService(responseJson);

        var result = await service.InterpretarAsync("cualquier consulta");

        result.Should().BeNull();
    }

    [Fact]
    public async Task InterpretarAsync_RequestsJsonModeAndSmallOutputBudget()
    {
        // 033-migracion-qwen-g4: el modelo ya no es una constante del módulo (lo resuelve el
        // cliente activo); este test ancla el contrato del prompt: JSON mode + presupuesto 1024.
        var handler = new CapturingHttpMessageHandler(
            (WrapAsGeminiResponse("""{"terminosExpandidos": [], "confianza": "baja"}"""), HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var vertexClient = new VertexGeminiClient(httpClient, BuildConfig(), FakeTokenProvider(), NullLogger<VertexGeminiClient>.Instance);
        var resolver = new Mock<LlmClientResolver>(null!, null!, NullLogger<LlmClientResolver>.Instance);
        resolver.Setup(r => r.GetClientAsync(It.IsAny<CancellationToken>())).ReturnsAsync(vertexClient);
        var service = new ConsultaSemanticaService(resolver.Object, NullLogger<ConsultaSemanticaService>.Instance);

        await service.InterpretarAsync("ciberseguridad");

        handler.LastRequestBody.Should().NotBeNull();
        handler.LastRequestBody.Should().Contain("\"responseMimeType\":\"application/json\"");
        handler.LastRequestBody.Should().Contain("\"maxOutputTokens\":1024");
    }

    private static string WrapAsGeminiResponse(string text)
    {
        var escaped = text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        return $$"""
            {
                "candidates": [{
                    "content": { "parts": [{ "text": "{{escaped}}" }], "role": "model" },
                    "finishReason": "STOP"
                }]
            }
            """;
    }
}

internal class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly (string Body, HttpStatusCode Status) _response;

    public StubHttpMessageHandler(string body, HttpStatusCode status)
    {
        _response = (body, status);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(_response.Status)
        {
            Content = new StringContent(_response.Body, Encoding.UTF8, "application/json")
        });
    }
}

internal class CapturingHttpMessageHandler : HttpMessageHandler
{
    private readonly (string Body, HttpStatusCode Status) _response;
    public Uri? LastRequestUri { get; private set; }
    public string? LastAuthHeader { get; private set; }
    public string? LastRequestBody { get; private set; }

    public CapturingHttpMessageHandler((string Body, HttpStatusCode Status) response)
    {
        _response = response;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        LastAuthHeader = request.Headers.Authorization?.ToString();
        if (request.Content != null)
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(_response.Status)
        {
            Content = new StringContent(_response.Body, Encoding.UTF8, "application/json")
        };
    }
}
