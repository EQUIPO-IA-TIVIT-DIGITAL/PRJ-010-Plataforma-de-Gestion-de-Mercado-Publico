using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPM.Modules.Competidores.Services;
using MPM.Shared.Services;
using Xunit;

namespace MPM.Modules.Competidores.Tests.Services;

// 029-fix-hallazgos-code-review-competidores-alertas (FR-003, FR-006): CompetidorGeminiService
// ahora delega en VertexGeminiClient (compartido con GeminiService de Análisis) en vez de armar
// su propia request con un maxOutputTokens desincronizado (8192 vs. los 65536 ya validados).
public class CompetidorGeminiServiceTests
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

    private static CompetidorGeminiService BuildService(StubHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var vertexClient = new VertexGeminiClient(httpClient, BuildConfig(), FakeTokenProvider(), NullLogger<VertexGeminiClient>.Instance);
        return new CompetidorGeminiService(vertexClient, NullLogger<CompetidorGeminiService>.Instance);
    }

    [Fact]
    public async Task AnalizarCompetidorAsync_UsesSameMaxOutputTokensAsAnalisis()
    {
        var handler = new StubHandler(@"{ ""candidates"": [{ ""content"": { ""parts"": [{ ""text"": ""{}"" }] } }] }", HttpStatusCode.OK);
        var service = BuildService(handler);

        await service.AnalizarCompetidorAsync("ENTEL", "oferta 1: $100");

        handler.LastRequestBody.Should().Contain($"\"maxOutputTokens\":{VertexGeminiClient.DefaultMaxOutputTokens}");
        handler.LastRequestBody.Should().NotContain("\"maxOutputTokens\":8192");
    }

    [Fact]
    public async Task AnalizarCompetidorAsync_ReturnsRawJsonText()
    {
        var handler = new StubHandler(
            @"{ ""candidates"": [{ ""content"": { ""parts"": [{ ""text"": ""{\""patrones\"":\""agresivo\""}"" }] } }] }",
            HttpStatusCode.OK);
        var service = BuildService(handler);

        var result = await service.AnalizarCompetidorAsync("ENTEL", "oferta 1: $100");

        result.Should().Be("{\"patrones\":\"agresivo\"}");
    }

    [Fact]
    public async Task AnalizarCompetidorAsync_ThrowsGeminiRespuestaBloqueada_WhenCandidatesEmpty()
    {
        // FR-003: antes de esta spec, esto indexaba candidates[0] sin guard y tiraba una
        // excepción no controlada distinta cada vez; ahora es siempre la misma excepción
        // tipada, capturable explícitamente por CompetidorAnalysisService (US3).
        var handler = new StubHandler(@"{ ""candidates"": [] }", HttpStatusCode.OK);
        var service = BuildService(handler);

        var act = () => service.AnalizarCompetidorAsync("ENTEL", "oferta 1: $100");

        await act.Should().ThrowAsync<GeminiRespuestaBloqueadaException>();
    }

    public class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _status;
        public string? LastRequestBody { get; private set; }

        public StubHandler(string body, HttpStatusCode status)
        {
            _body = body;
            _status = status;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content != null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }
}
