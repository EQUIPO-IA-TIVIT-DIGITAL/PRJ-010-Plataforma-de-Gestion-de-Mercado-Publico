using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPM.Shared.Services;
using Xunit;

namespace MPM.Shared.Tests.Services;

// 033-migracion-qwen-g4: contrato de traducción LlmRequest (neutral) → body Gemini (Vertex AI).
public class LlmClientContractTests
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

    private static VertexGeminiClient BuildClient(CapturingHandler handler) =>
        new(new HttpClient(handler), BuildConfig(), FakeTokenProvider(), NullLogger<VertexGeminiClient>.Instance);

    private static string OkResponse() => @"{ ""candidates"": [{ ""content"": { ""parts"": [{ ""text"": ""ok"" }] } }] }";

    [Fact]
    public async Task LlmRequest_JsonResponse_AgregaResponseMimeType()
    {
        var handler = new CapturingHandler(OkResponse());
        var client = BuildClient(handler);

        await client.GenerarContenidoAsync(new LlmRequest(
            Messages: [new LlmMessage("user", [new LlmTextPart("prompt")])],
            JsonResponse: true,
            MaxOutputTokens: 1024), CancellationToken.None);

        handler.LastRequestBody.Should().Contain("\"responseMimeType\":\"application/json\"");
        handler.LastRequestBody.Should().Contain("\"maxOutputTokens\":1024");
    }

    [Fact]
    public async Task LlmRequest_SinJsonResponse_NoAgregaResponseMimeType()
    {
        var handler = new CapturingHandler(OkResponse());
        var client = BuildClient(handler);

        await client.GenerarContenidoAsync(new LlmRequest(
            Messages: [new LlmMessage("user", [new LlmTextPart("hola")])],
            JsonResponse: false), CancellationToken.None);

        handler.LastRequestBody.Should().NotContain("responseMimeType");
    }

    [Fact]
    public async Task LlmRequest_PdfConGcsUri_UsaFileData()
    {
        var handler = new CapturingHandler(OkResponse());
        var client = BuildClient(handler);

        await client.GenerarContenidoAsync(new LlmRequest(
            Messages: [new LlmMessage("user",
            [
                new LlmPdfPart([1, 2, 3], "doc.pdf", "gs://tivit-cu010-mpm-adjuntos/analisis/1/doc.pdf"),
                new LlmTextPart("analiza")
            ])],
            JsonResponse: true), CancellationToken.None);

        handler.LastRequestBody.Should().Contain("fileUri");
        handler.LastRequestBody.Should().Contain("gs://tivit-cu010-mpm-adjuntos/analisis/1/doc.pdf");
        handler.LastRequestBody.Should().NotContain("inlineData");
    }

    [Fact]
    public async Task LlmRequest_PdfSinGcsUri_UsaInlineDataBase64()
    {
        var handler = new CapturingHandler(OkResponse());
        var client = BuildClient(handler);

        await client.GenerarContenidoAsync(new LlmRequest(
            Messages: [new LlmMessage("user", [new LlmPdfPart([0x25, 0x50, 0x44, 0x46], "doc.pdf", null)])],
            JsonResponse: true), CancellationToken.None);

        handler.LastRequestBody.Should().Contain("inlineData");
        handler.LastRequestBody.Should().Contain(Convert.ToBase64String([0x25, 0x50, 0x44, 0x46]));
        handler.LastRequestBody.Should().NotContain("fileUri");
    }

    [Fact]
    public async Task LlmRequest_HistorialConAssistant_SeTraduceARolModel()
    {
        var handler = new CapturingHandler(OkResponse());
        var client = BuildClient(handler);

        await client.GenerarContenidoAsync(new LlmRequest(
            Messages:
            [
                new LlmMessage("user", [new LlmTextPart("pregunta")]),
                new LlmMessage("assistant", [new LlmTextPart("respuesta")]),
            ],
            SystemInstruction: "sé breve"), CancellationToken.None);

        handler.LastRequestBody.Should().Contain("\"role\":\"user\"");
        handler.LastRequestBody.Should().Contain("\"role\":\"model\"");
        handler.LastRequestBody.Should().Contain("systemInstruction");
        // El serializador escapa no-ASCII (\u00E9 = é); se valida el texto desescapado.
        var body = System.Text.RegularExpressions.Regex.Unescape(handler.LastRequestBody!);
        body.Should().Contain("sé breve");
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly string _body;
        public string? LastRequestBody { get; private set; }

        public CapturingHandler(string body) => _body = body;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content != null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }
}
