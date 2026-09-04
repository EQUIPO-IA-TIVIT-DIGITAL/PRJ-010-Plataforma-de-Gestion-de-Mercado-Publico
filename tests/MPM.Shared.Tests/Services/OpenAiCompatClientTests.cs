using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MPM.Shared.Services;
using Xunit;

namespace MPM.Shared.Tests.Services;

// 033-migracion-qwen-g4 (US3): OpenAiCompatClient (Qwen vía endpoint OpenAI-compatible).
public class OpenAiCompatClientTests
{
    private static IConfiguration BuildConfig(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value))
            .Build();

    private static OpenAiCompatClient BuildClient(CapturingHandler handler, params (string Key, string? Value)[] values) =>
        new(new HttpClient(handler), BuildConfig(values), NullLogger<OpenAiCompatClient>.Instance);

    private static string OkResponse(string content = "{}", string finishReason = "stop", long? prompt = 10, long? completion = 5) =>
        $$"""
        {
          "id": "chatcmpl-test",
          "model": "qwen3.7-g4",
          "choices": [ { "index": 0, "message": { "role": "assistant", "content": "{{content}}" }, "finish_reason": "{{finishReason}}" } ],
          "usage": { "prompt_tokens": {{prompt ?? 0}}, "completion_tokens": {{completion ?? 0}}, "total_tokens": {{(prompt ?? 0) + (completion ?? 0)}} }
        }
        """;

    [Fact]
    public async Task GenerarContenidoAsync_PosteaAEndpointChatCompletions_ConBearerKey()
    {
        var handler = new CapturingHandler(OkResponse("hola"));
        var client = BuildClient(handler, ("AI:Endpoint", "http://qwen:8000/v1"), ("AI:Model", "qwen3.7-g4"), ("AI:ApiKey", "secret-key"));
        client.ApplySettings("http://qwen:8000/v1", "qwen3.7-g4");

        var result = await client.GenerarContenidoAsync(new LlmRequest(
            Messages: [new LlmMessage("user", [new LlmTextPart("hola")])]));

        handler.LastRequestUri.Should().Be("http://qwen:8000/v1/chat/completions");
        handler.LastAuthHeader.Should().Be("Bearer secret-key");
        result.Text.Should().Be("hola");
        result.FinishReason.Should().Be("stop");
        result.Usage.PromptTokenCount.Should().Be(10);
        result.Usage.CandidatesTokenCount.Should().Be(5);
    }

    [Fact]
    public async Task GenerarContenidoAsync_JsonResponse_AgregaResponseFormat()
    {
        var handler = new CapturingHandler(OkResponse("{}"));
        var client = BuildClient(handler, ("AI:Endpoint", "http://qwen:8000/v1"), ("AI:Model", "qwen3.7-g4"));
        client.ApplySettings("http://qwen:8000/v1", "qwen3.7-g4");

        await client.GenerarContenidoAsync(new LlmRequest(
            Messages: [new LlmMessage("user", [new LlmTextPart("analiza")])],
            JsonResponse: true, MaxOutputTokens: 4096));

        handler.LastRequestBody.Should().Contain("\"model\":\"qwen3.7-g4\"");
        handler.LastRequestBody.Should().Contain("\"max_tokens\":4096");
        handler.LastRequestBody.Should().Contain("\"response_format\":{\"type\":\"json_object\"}");
        handler.LastRequestBody.Should().Contain("\"role\":\"user\"");
    }

    [Fact]
    public async Task GenerarContenidoAsync_PdfPart_SeEnviaComoTextoEstructuradoParaQwen()
    {
        var handler = new CapturingHandler(OkResponse("{}"));
        var client = BuildClient(handler, ("AI:Endpoint", "http://qwen:8000/v1"), ("AI:Model", "qwen3.7-g4"));
        client.ApplySettings("http://qwen:8000/v1", "qwen3.7-g4");

        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        await client.GenerarContenidoAsync(new LlmRequest(
            Messages: [new LlmMessage("user", [new LlmPdfPart(pdfBytes, "doc.pdf", null)])],
            JsonResponse: true));

        handler.LastRequestBody.Should().Contain("\"type\":\"text\"");
    }

    [Fact]
    public async Task GenerarContenidoAsync_SinChoices_LanzaRespuestaBloqueada()
    {
        var handler = new CapturingHandler(@"{ ""choices"": [] }");
        var client = BuildClient(handler, ("AI:Endpoint", "http://qwen:8000/v1"), ("AI:Model", "qwen3.7-g4"));
        client.ApplySettings("http://qwen:8000/v1", "qwen3.7-g4");

        var act = () => client.GenerarContenidoAsync(new LlmRequest(
            Messages: [new LlmMessage("user", [new LlmTextPart("x")])]));

        await act.Should().ThrowAsync<LlmRespuestaBloqueadaException>();
    }

    [Fact]
    public async Task GenerarContenidoAsync_SinEndpoint_LanzaErrorConfiguracion()
    {
        var handler = new CapturingHandler(OkResponse("{}"));
        var client = BuildClient(handler, ("AI:Model", "qwen3.7-g4"));

        var act = () => client.GenerarContenidoAsync(new LlmRequest(
            Messages: [new LlmMessage("user", [new LlmTextPart("x")])]));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*AI:Endpoint*");
    }

    [Fact]
    public async Task GenerarContenidoAsync_ErrorHttp_LanzaHttpRequestException()
    {
        var handler = new CapturingHandler("""{"error":{"message":"model not found"}}""", HttpStatusCode.NotFound);
        var client = BuildClient(handler, ("AI:Endpoint", "http://qwen:8000/v1"), ("AI:Model", "qwen3.7-g4"));
        client.ApplySettings("http://qwen:8000/v1", "qwen3.7-g4");

        var act = () => client.GenerarContenidoAsync(new LlmRequest(
            Messages: [new LlmMessage("user", [new LlmTextPart("x")])]));

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GenerarContenidoAsync_Truncamiento_ExponeFinishReasonLength()
    {
        var handler = new CapturingHandler(OkResponse("parcial", finishReason: "length"));
        var client = BuildClient(handler, ("AI:Endpoint", "http://qwen:8000/v1"), ("AI:Model", "qwen3.7-g4"));
        client.ApplySettings("http://qwen:8000/v1", "qwen3.7-g4");

        var result = await client.GenerarContenidoAsync(new LlmRequest(
            Messages: [new LlmMessage("user", [new LlmTextPart("x")])]));

        result.FinishReason.Should().Be("length");
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _status;
        public string? LastRequestUri { get; private set; }
        public string? LastAuthHeader { get; private set; }
        public string? LastRequestBody { get; private set; }

        public CapturingHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _body = body;
            _status = status;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString();
            LastAuthHeader = request.Headers.Authorization?.ToString();
            if (request.Content != null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }
}
