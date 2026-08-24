using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace MPM.Core.Middleware;

public class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var raw = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(raw)) raw = raw.Trim();
        if (raw?.Length > 36) raw = raw.Substring(0, 36);
        if (raw != null && !Regex.IsMatch(raw, @"^[a-zA-Z0-9_-]{8,36}$")) raw = null;

        if (raw == null)
        {
            var fallback = context.Request.Headers["X-Request-Id"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(fallback)) fallback = fallback.Trim();
            if (fallback?.Length > 36) fallback = fallback.Substring(0, 36);
            if (fallback != null && Regex.IsMatch(fallback, @"^[a-zA-Z0-9_-]{8,36}$")) raw = fallback;
        }

        var correlationId = raw ?? Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier ?? Guid.NewGuid().ToString("N");
        var traceId = Activity.Current?.TraceId.ToString() ?? correlationId;

        context.Items["CorrelationId"] = correlationId;
        context.Items["TraceId"] = traceId;

        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("correlation_id", correlationId);
        var spanId = Activity.Current?.SpanId.ToString();
        if (!string.IsNullOrEmpty(spanId))
            Activity.Current?.SetTag("spanId", spanId);

        context.Response.Headers["X-Correlation-Id"] = correlationId;
        context.Response.Headers["X-Trace-Id"] = traceId;
        var traceparent = Activity.Current?.Id;
        if (!string.IsNullOrEmpty(traceparent))
            context.Response.Headers["traceparent"] = traceparent;
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey("X-Correlation-Id"))
                context.Response.Headers["X-Correlation-Id"] = correlationId;
            if (!context.Response.Headers.ContainsKey("X-Trace-Id"))
                context.Response.Headers["X-Trace-Id"] = traceId;
            var tp = Activity.Current?.Id;
            if (!string.IsNullOrEmpty(tp) && !context.Response.Headers.ContainsKey("traceparent"))
                context.Response.Headers["traceparent"] = tp;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TraceId", traceId))
        using (LogContext.PushProperty("SpanId", spanId ?? ""))
        {
            await next(context);
        }
    }
}
