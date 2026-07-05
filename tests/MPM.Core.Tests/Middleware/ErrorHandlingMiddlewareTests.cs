using MPM.Core.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace MPM.Core.Tests.Middleware;

public class ErrorHandlingMiddlewareTests
{
    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static Mock<ILogger<ErrorHandlingMiddleware>> CreateLoggerMock()
    {
        return new Mock<ILogger<ErrorHandlingMiddleware>>();
    }

    [Fact]
    public async Task Passes_Through_SuccessfulRequests()
    {
        var logger = CreateLoggerMock();
        var middleware = new ErrorHandlingMiddleware(_ => Task.CompletedTask, logger.Object);
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Catches_Exception_Returns500()
    {
        var logger = CreateLoggerMock();
        var middleware = new ErrorHandlingMiddleware(_ => throw new InvalidOperationException("Test error"), logger.Object);
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task Catches_Exception_ReturnsJsonBody()
    {
        var logger = CreateLoggerMock();
        var middleware = new ErrorHandlingMiddleware(_ => throw new Exception("boom"), logger.Object);
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.Should().NotBeEmpty();
        
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        json.TryGetProperty("success", out var successProp).Should().BeTrue();
        successProp.GetBoolean().Should().BeFalse();
        json.TryGetProperty("message", out var msgProp).Should().BeTrue();
        msgProp.GetString().Should().Be("Error interno del servidor");
    }

    [Fact]
    public async Task Catches_Exception_Contains_ErrorsArray()
    {
        var logger = CreateLoggerMock();
        var middleware = new ErrorHandlingMiddleware(_ => throw new Exception("boom"), logger.Object);
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        json.TryGetProperty("errors", out var errorsProp).Should().BeTrue();
        errorsProp.GetArrayLength().Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task SetsContentType_ToApplicationJson()
    {
        var logger = CreateLoggerMock();
        var middleware = new ErrorHandlingMiddleware(_ => throw new Exception("boom"), logger.Object);
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.ContentType.Should().Be("application/json");
    }
}