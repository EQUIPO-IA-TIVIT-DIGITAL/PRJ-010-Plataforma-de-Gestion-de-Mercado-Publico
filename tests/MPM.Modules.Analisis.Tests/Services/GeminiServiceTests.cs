using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MPM.Modules.Analisis.Services;
using Xunit;

namespace MPM.Modules.Analisis.Tests.Services;

public class GeminiServiceTests
{
    private const string TestApiKey = "test-api-key";

    [Fact]
    public void ModelName_ShouldBeGemini25Pro()
    {
        GeminiService.ModelName.Should().Be("gemini-2.5-pro");
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
        var httpClient = new HttpClient(handler);
        var service = new GeminiService(httpClient, NullLogger<GeminiService>.Instance);

        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 dummy content");
        var result = await service.AnalyzePdfAsync(pdfBytes, "test.pdf", TestApiKey);

        result.Text.Should().Contain("foo");
        result.Usage.PromptTokenCount.Should().Be(100);
        result.Usage.CandidatesTokenCount.Should().Be(50);
        result.Usage.TotalTokenCount.Should().Be(150);
        result.RawResponse.Should().Contain("candidates");
    }

    [Fact]
    public async Task AnalyzePdfAsync_BuildsRequestToPublicGeminiEndpoint()
    {
        var handler = new CapturingHttpMessageHandler(
            (@"{ ""candidates"": [{ ""content"": { ""parts"": [{ ""text"": ""ok"" }] } }] }", HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var service = new GeminiService(httpClient, NullLogger<GeminiService>.Instance);

        await service.AnalyzePdfAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 }, "doc.pdf", "key-123");

        handler.LastRequestUri.Should().NotBeNull();
        handler.LastRequestUri!.Host.Should().Be("generativelanguage.googleapis.com");
        handler.LastRequestUri.AbsolutePath.Should().Contain("gemini-2.5-pro");
        handler.LastRequestUri.AbsolutePath.Should().EndWith(":generateContent");
        handler.LastRequestUri.Query.Should().Contain("key=key-123");
    }

    [Fact]
    public async Task AnalyzePdfAsync_ThrowsOnHttpError()
    {
        var handler = new StubHttpMessageHandler("""{"error":{"message":"invalid key"}}""", HttpStatusCode.Unauthorized);
        var httpClient = new HttpClient(handler);
        var service = new GeminiService(httpClient, NullLogger<GeminiService>.Instance);

        var act = () => service.AnalyzePdfAsync(new byte[] { 1, 2, 3 }, "x.pdf", "bad");
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task AnalyzePdfAsync_HandlesEmptyCandidates()
    {
        var responseJson = @"{ ""candidates"": [] }";
        var handler = new StubHttpMessageHandler(responseJson, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var service = new GeminiService(httpClient, NullLogger<GeminiService>.Instance);

        var result = await service.AnalyzePdfAsync(new byte[] { 1, 2, 3 }, "x.pdf", "k");

        result.Text.Should().BeEmpty();
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
        var httpClient = new HttpClient(handler);
        var service = new GeminiService(httpClient, NullLogger<GeminiService>.Instance);

        var result = await service.ChatAsync(
            "Pregunta?",
            """{"licitacion": "test"}""",
            new List<ChatHistoryItem>(),
            "key");

        result.Text.Should().Be("La respuesta es X");
        result.FinishReason.Should().Be("STOP");
    }

    [Fact]
    public async Task ChatAsync_BuildsRequestWithRoleUser()
    {
        var handler = new CapturingHttpMessageHandler(
            (@"{ ""candidates"": [{ ""content"": { ""parts"": [{ ""text"": ""ok"" }] } }] }", HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var service = new GeminiService(httpClient, NullLogger<GeminiService>.Instance);

        await service.ChatAsync("Mi pregunta", "{}", new List<ChatHistoryItem>(), "k");

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

    public CapturingHttpMessageHandler((string Body, HttpStatusCode Status) response)
    {
        _response = response;
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
