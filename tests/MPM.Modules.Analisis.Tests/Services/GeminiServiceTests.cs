using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPM.Core.SystemConfig;
using MPM.Modules.Analisis.Services;
using MPM.Shared.Services;
using Xunit;

namespace MPM.Modules.Analisis.Tests.Services;

public class GeminiServiceTests
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

    // 033-migracion-qwen-g4: GeminiService ya no recibe VertexGeminiClient directo -- recibe
    // LlmClientResolver (que se mockea para devolver el VertexGeminiClient real con el mismo
    // HttpClient fake, preservando la cobertura de armado/parseo del request).
    private static GeminiService BuildService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var vertexClient = new VertexGeminiClient(httpClient, BuildConfig(), FakeTokenProvider(), NullLogger<VertexGeminiClient>.Instance);
        var resolver = new Mock<LlmClientResolver>(null!, null!, NullLogger<LlmClientResolver>.Instance);
        resolver.Setup(r => r.GetClientAsync(It.IsAny<CancellationToken>())).ReturnsAsync(vertexClient);
        return new GeminiService(resolver.Object, NullLogger<GeminiService>.Instance);
    }

    [Fact]
    public void ModelName_ShouldBeGemini25Pro()
    {
        GeminiService.ModelName.Should().Be("gemini-2.5-pro");
    }

    [Fact]
    public async Task GetModelNameAsync_ReturnsActiveClientModel()
    {
        var handler = new StubHttpMessageHandler("""{ "candidates": [{ "content": { "parts": [{ "text": "ok" }] } }] }""", HttpStatusCode.OK);
        var service = BuildService(handler);

        var model = await service.GetModelNameAsync();

        model.Should().Be("gemini-2.5-pro");
    }

    [Fact]
    public async Task AnalyzeDocumentosAsync_RecordsModelNameUsed()
    {
        var handler = new StubHttpMessageHandler("""{ "candidates": [{ "content": { "parts": [{ "text": "{}" }] } }] }""", HttpStatusCode.OK);
        var service = BuildService(handler);

        var result = await service.AnalyzePdfAsync(new byte[] { 1, 2, 3 }, "doc.pdf", gcsUri: null);

        result.ModelName.Should().Be("gemini-2.5-pro");
    }

    [Fact]
    public async Task AnalyzePdfAsync_ReturnsParsedText()
    {
        var responseJson = @"{
            ""candidates"": [{
                ""content"": {
                    ""parts"": [{ ""text"": ""{ \""foo\"": 1 }"" }],
                    ""role"": ""model""
                },
                ""finishReason"": ""STOP"",
                ""index"": 0
            }],
            ""usageMetadata"": {
                ""promptTokenCount"": 100,
                ""candidatesTokenCount"": 50,
                ""totalTokenCount"": 150
            }
        }";

        var handler = new StubHttpMessageHandler(responseJson, HttpStatusCode.OK);
        var service = BuildService(handler);

        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 dummy content");
        var result = await service.AnalyzePdfAsync(pdfBytes, "test.pdf", gcsUri: null);

        result.Text.Should().Contain("foo");
        result.Usage.PromptTokenCount.Should().Be(100);
        result.Usage.CandidatesTokenCount.Should().Be(50);
        result.Usage.TotalTokenCount.Should().Be(150);
        result.RawResponse.Should().Contain("candidates");
    }

    [Fact]
    public async Task AnalyzePdfAsync_BuildsRequestToVertexAiEndpointWithBearerToken()
    {
        var handler = new CapturingHttpMessageHandler(
            (@"{ ""candidates"": [{ ""content"": { ""parts"": [{ ""text"": ""ok"" }] } }] }", HttpStatusCode.OK));
        var service = BuildService(handler);

        await service.AnalyzePdfAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 }, "doc.pdf", gcsUri: null);

        handler.LastRequestUri.Should().NotBeNull();
        handler.LastRequestUri!.Host.Should().Be("us-central1-aiplatform.googleapis.com");
        handler.LastRequestUri.AbsolutePath.Should().Contain("projects/tivit-cu010/locations/us-central1");
        handler.LastRequestUri.AbsolutePath.Should().Contain("gemini-2.5-pro");
        handler.LastRequestUri.AbsolutePath.Should().EndWith(":generateContent");
        handler.LastRequestUri.Query.Should().NotContain("key=");
        handler.LastAuthHeader.Should().Be($"Bearer {TestToken}");
    }

    [Fact]
    public async Task AnalyzePdfAsync_UsesFileDataWithGcsUri_WhenProvided()
    {
        var handler = new CapturingHttpMessageHandler(
            (@"{ ""candidates"": [{ ""content"": { ""parts"": [{ ""text"": ""ok"" }] } }] }", HttpStatusCode.OK));
        var service = BuildService(handler);

        await service.AnalyzePdfAsync(new byte[] { 1, 2, 3 }, "doc.pdf", gcsUri: "gs://tivit-cu010-mpm-adjuntos/analisis/1/doc.pdf");

        handler.LastRequestBody.Should().Contain("fileUri");
        handler.LastRequestBody.Should().Contain("gs://tivit-cu010-mpm-adjuntos/analisis/1/doc.pdf");
        handler.LastRequestBody.Should().NotContain("inlineData");
    }

    [Fact]
    public async Task AnalyzePdfAsync_UsesInlineData_WhenGcsUriIsNull()
    {
        var handler = new CapturingHttpMessageHandler(
            (@"{ ""candidates"": [{ ""content"": { ""parts"": [{ ""text"": ""ok"" }] } }] }", HttpStatusCode.OK));
        var service = BuildService(handler);

        await service.AnalyzePdfAsync(new byte[] { 1, 2, 3 }, "doc.pdf", gcsUri: null);

        handler.LastRequestBody.Should().Contain("inlineData");
        handler.LastRequestBody.Should().NotContain("fileUri");
    }

    [Fact]
    public async Task AnalyzePdfAsync_UsesSharedMaxOutputTokens()
    {
        var handler = new CapturingHttpMessageHandler(
            (@"{ ""candidates"": [{ ""content"": { ""parts"": [{ ""text"": ""ok"" }] } }] }", HttpStatusCode.OK));
        var service = BuildService(handler);

        await service.AnalyzePdfAsync(new byte[] { 1, 2, 3 }, "doc.pdf", gcsUri: null);

        // Ancla el valor validado en producción (VertexGeminiClient.DefaultMaxOutputTokens) --
        // si alguien vuelve a hardcodear un número distinto acá o en CompetidorGeminiService,
        // este test y su equivalente en CompetidorGeminiServiceTests deben fallar juntos.
        handler.LastRequestBody.Should().Contain($"\"maxOutputTokens\":{VertexGeminiClient.DefaultMaxOutputTokens}");
    }

    [Fact]
    public async Task AnalyzePdfAsync_ThrowsOnHttpError()
    {
        var handler = new StubHttpMessageHandler("""{"error":{"message":"invalid credentials"}}""", HttpStatusCode.Unauthorized);
        var service = BuildService(handler);

        var act = () => service.AnalyzePdfAsync(new byte[] { 1, 2, 3 }, "x.pdf", gcsUri: null);
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task AnalyzePdfAsync_ThrowsGeminiRespuestaBloqueada_WhenCandidatesEmpty()
    {
        // 029-fix-hallazgos-code-review-competidores-alertas (FR-003): antes de esta spec, un
        // candidates vacío (ej. contenido bloqueado por el filtro de seguridad) hacía que
        // GeminiService devolviera Text vacío silenciosamente. VertexGeminiClient ahora lanza
        // una excepción tipada -- AnalisisBackgroundService ya tiene un catch(Exception) que
        // envuelve todo el análisis, así que este cambio no rompe el flujo existente, solo lo
        // hace explícito.
        var responseJson = @"{ ""candidates"": [] }";
        var handler = new StubHttpMessageHandler(responseJson, HttpStatusCode.OK);
        var service = BuildService(handler);

        var act = () => service.AnalyzePdfAsync(new byte[] { 1, 2, 3 }, "x.pdf", gcsUri: null);
        await act.Should().ThrowAsync<GeminiRespuestaBloqueadaException>();
    }

    [Fact]
    public async Task ChatAsync_ReturnsParsedText()
    {
        var responseJson = @"{
            ""candidates"": [{
                ""content"": { ""parts"": [{ ""text"": ""La respuesta es X"" }], ""role"": ""model"" },
                ""finishReason"": ""STOP""
            }]
        }";

        var handler = new StubHttpMessageHandler(responseJson, HttpStatusCode.OK);
        var service = BuildService(handler);

        var result = await service.ChatAsync(
            "Pregunta?",
            """{"licitacion": "test"}""",
            new List<ChatHistoryItem>());

        result.Text.Should().Be("La respuesta es X");
        result.FinishReason.Should().Be("STOP");
    }

    [Fact]
    public async Task ChatAsync_BuildsRequestWithRoleUser()
    {
        var handler = new CapturingHttpMessageHandler(
            (@"{ ""candidates"": [{ ""content"": { ""parts"": [{ ""text"": ""ok"" }] } }] }", HttpStatusCode.OK));
        var service = BuildService(handler);

        await service.ChatAsync("Mi pregunta", "{}", new List<ChatHistoryItem>());

        handler.LastRequestBody.Should().NotBeNullOrEmpty();
        handler.LastRequestBody.Should().Contain("\"role\":\"user\"");
        handler.LastRequestBody.Should().Contain("Mi pregunta");
    }
}

internal class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly (string Body, HttpStatusCode Status) _response;
    public Uri? LastRequestUri { get; private set; }
    public string? LastRequestBody { get; private set; }

    public StubHttpMessageHandler(string body, HttpStatusCode status)
    {
        _response = (body, status);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        if (request.Content != null)
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(_response.Status)
        {
            Content = new StringContent(_response.Body, Encoding.UTF8, "application/json")
        };
    }
}

internal class CapturingHttpMessageHandler : HttpMessageHandler
{
    private readonly (string Body, HttpStatusCode Status) _response;
    public Uri? LastRequestUri { get; private set; }
    public string? LastRequestBody { get; private set; }
    public string? LastAuthHeader { get; private set; }

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
