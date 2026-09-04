using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MPM.Core.Middleware;
using System.Diagnostics;
using Xunit;

namespace MPM.Core.Tests.Middleware;

public class CorrelationIdMiddlewareTests
{
    private static CorrelationIdMiddleware CreateMiddleware(RequestDelegate? next = null)
        => new(next ?? (_ => Task.CompletedTask));

    [Fact]
    public async Task ValidCorrelationId_IsPreservedAndEchoed()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "abc12345-XYZ_01";
        string? capturedCorrelation = null;
        var middleware = CreateMiddleware(ctx =>
        {
            capturedCorrelation = ctx.Items["CorrelationId"] as string;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        capturedCorrelation.Should().Be("abc12345-XYZ_01");
        context.Items["CorrelationId"].Should().Be("abc12345-XYZ_01");
        // Response header echoed
        context.Response.Headers.ContainsKey("X-Correlation-Id").Should().BeTrue();
        context.Response.Headers["X-Correlation-Id"].ToString().Should().Be("abc12345-XYZ_01");
    }

    [Fact]
    public async Task ValidCorrelationId_SeparateTraceId_WhenActivityPresent()
    {
        var activity = new Activity("test").Start();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "valid-id-12345";
        string? traceId = null;
        var middleware = CreateMiddleware(ctx =>
        {
            traceId = ctx.Items["TraceId"] as string;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);
        activity.Stop();

        traceId.Should().NotBeNull();
        traceId.Should().Be(activity.TraceId.ToString());
        context.Items["CorrelationId"].Should().Be("valid-id-12345");
        context.Items["TraceId"].Should().NotBe(context.Items["CorrelationId"]);
    }

    [Fact]
    public async Task InvalidCorrelationId_FallsBackToTraceOrGuid()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "bad id with spaces; DROP TABLE";
        string? captured = null;
        var middleware = CreateMiddleware(ctx =>
        {
            captured = ctx.Items["CorrelationId"] as string;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        captured.Should().NotBe("bad id with spaces; DROP TABLE");
        captured.Should().NotBeNull();
        // debe ser guid o traceId, nunca contiene espacios ni ;
        captured.Should().NotContain(" ");
        captured.Should().NotContain(";");
    }

    [Fact]
    public async Task OverLongCorrelationId_TruncatedTo36()
    {
        var longId = new string('A', 50); // 50 > 36
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = longId;
        string? captured = null;
        var middleware = CreateMiddleware(ctx =>
        {
            captured = ctx.Items["CorrelationId"] as string;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        captured.Should().NotBeNull();
        captured!.Length.Should().BeLessOrEqualTo(36);
        captured.Should().Be(new string('A', 36));
    }

    [Fact]
    public async Task ShortCorrelationId_Rejected_RequiresMin8Chars()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "abc"; // <8
        string? captured = null;
        var middleware = CreateMiddleware(ctx =>
        {
            captured = ctx.Items["CorrelationId"] as string;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        captured.Should().NotBe("abc");
        captured!.Length.Should().BeGreaterOrEqualTo(8);
    }

    [Fact]
    public async Task EmptyHeader_GeneratesNewCorrelationId()
    {
        var context = new DefaultHttpContext();
        // no header
        string? captured = null;
        var middleware = CreateMiddleware(ctx =>
        {
            captured = ctx.Items["CorrelationId"] as string;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        captured.Should().NotBeNullOrWhiteSpace();
        captured!.Length.Should().BeGreaterOrEqualTo(8);
    }

    [Fact]
    public async Task XCorrelationId_TakesPrecedenceOver_XRequestId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "corr-12345678";
        context.Request.Headers["X-Request-Id"] = "req-87654321";
        string? captured = null;
        var middleware = CreateMiddleware(ctx =>
        {
            captured = ctx.Items["CorrelationId"] as string;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        captured.Should().Be("corr-12345678");
    }

    [Fact]
    public async Task XRequestId_UsedAsFallback_WhenXCorrelationIdInvalid()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "bad id!";
        context.Request.Headers["X-Request-Id"] = "fallback-123";
        string? captured = null;
        var middleware = CreateMiddleware(ctx =>
        {
            captured = ctx.Items["CorrelationId"] as string;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        captured.Should().Be("fallback-123");
    }

    [Fact]
    public async Task Sanitization_TrimsWhitespace()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "  valid-trim-123  ";
        string? captured = null;
        var middleware = CreateMiddleware(ctx =>
        {
            captured = ctx.Items["CorrelationId"] as string;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        captured.Should().Be("valid-trim-123");
    }

    [Fact]
    public async Task CorrelationId_DoesNotCollapse_WithTraceId_WhenNoActivity()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "my-corr-id-999";
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        var corr = context.Items["CorrelationId"] as string;
        var trace = context.Items["TraceId"] as string;
        corr.Should().Be("my-corr-id-999");
        // sin Activity, TraceId == CorrelationId (fallback)
        trace.Should().Be(corr);
    }
}
