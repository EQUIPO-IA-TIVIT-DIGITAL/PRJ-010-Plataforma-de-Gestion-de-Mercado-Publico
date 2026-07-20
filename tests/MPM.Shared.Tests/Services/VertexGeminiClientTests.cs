using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPM.Shared.Services;
using Xunit;

namespace MPM.Shared.Tests.Services;

// 029-fix-hallazgos-code-review-competidores-alertas (FR-006): VertexGeminiClient centraliza lo
// que antes estaba duplicado (con distinto maxOutputTokens) entre GeminiService (Análisis) y
// CompetidorGeminiService (Competidores) -- estos tests cubren el cliente compartido en sí;
// GeminiServiceTests y CompetidorGeminiServiceTests cubren que cada caller lo usa correctamente.
public class VertexGeminiClientTests
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

    private static VertexGeminiClient BuildClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler), BuildConfig(), FakeTokenProvider(), NullLogger<VertexGeminiClient>.Instance);

    [Fact]
    public async Task GenerarContenidoAsync_ParsesTextAndUsage()
    {
        var responseJson = @"{
            ""candidates"": [{ ""content"": { ""parts"": [{ ""text"": ""{\""ok\"":true}"" }] }, ""finishReason"": ""STOP"" }],
            ""usageMetadata"": { ""promptTokenCount"": 10, ""candidatesTokenCount"": 5, ""totalTokenCount"": 15 }
        }";
        var handler = new StubHandler(responseJson, HttpStatusCode.OK);
        var client = BuildClient(handler);

        var result = await client.GenerarContenidoAsync("gemini-2.5-pro", new { contents = Array.Empty<object>() });

        result.Text.Should().Be("{\"ok\":true}");
        result.FinishReason.Should().Be("STOP");
        result.Usage.PromptTokenCount.Should().Be(10);
        result.Usage.CandidatesTokenCount.Should().Be(5);
        result.Usage.TotalTokenCount.Should().Be(15);
    }

    [Fact]
    public async Task GenerarContenidoAsync_StripsMarkdownCodeFences()
    {
        var responseJson = @"{ ""candidates"": [{ ""content"": { ""parts"": [{ ""text"": ""```json\n{\""a\"":1}\n```"" }] } }] }";
        var handler = new StubHandler(responseJson, HttpStatusCode.OK);
        var client = BuildClient(handler);

        var result = await client.GenerarContenidoAsync("gemini-2.5-pro", new { });

        result.Text.Should().Be("{\"a\":1}");
    }

    [Fact]
    public async Task GenerarContenidoAsync_ThrowsGeminiRespuestaBloqueada_WhenNoCandidates()
    {
        var handler = new StubHandler(@"{ ""candidates"": [] }", HttpStatusCode.OK);
        var client = BuildClient(handler);

        var act = () => client.GenerarContenidoAsync("gemini-2.5-pro", new { });

        await act.Should().ThrowAsync<GeminiRespuestaBloqueadaException>();
    }

    [Fact]
    public async Task GenerarContenidoAsync_ThrowsGeminiRespuestaBloqueada_WhenCandidatesPropertyMissing()
    {
        var handler = new StubHandler(@"{ ""promptFeedback"": { ""blockReason"": ""SAFETY"" } }", HttpStatusCode.OK);
        var client = BuildClient(handler);

        var act = () => client.GenerarContenidoAsync("gemini-2.5-pro", new { });

        await act.Should().ThrowAsync<GeminiRespuestaBloqueadaException>();
    }

    [Fact]
    public async Task GenerarContenidoAsync_BuildsCorrectEndpointAndAuthHeader()
    {
        var handler = new StubHandler(@"{ ""candidates"": [{ ""content"": { ""parts"": [{ ""text"": ""ok"" }] } }] }", HttpStatusCode.OK);
        var client = BuildClient(handler);

        await client.GenerarContenidoAsync("gemini-2.5-pro", new { });

        handler.LastRequestUri.Should().NotBeNull();
        handler.LastRequestUri!.ToString().Should().Be(
            "https://us-central1-aiplatform.googleapis.com/v1/projects/tivit-cu010/locations/us-central1/publishers/google/models/gemini-2.5-pro:generateContent");
        handler.LastAuthHeader.Should().Be($"Bearer {TestToken}");
    }

    [Fact]
    public async Task GenerarContenidoAsync_ThrowsOnHttpError()
    {
        var handler = new StubHandler("""{"error":{"message":"denied"}}""", HttpStatusCode.Forbidden);
        var client = BuildClient(handler);

        var act = () => client.GenerarContenidoAsync("gemini-2.5-pro", new { });

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _status;
        public Uri? LastRequestUri { get; private set; }
        public string? LastAuthHeader { get; private set; }

        public StubHandler(string body, HttpStatusCode status)
        {
            _body = body;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastAuthHeader = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }
}
