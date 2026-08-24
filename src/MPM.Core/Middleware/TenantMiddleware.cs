using System.Diagnostics;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MPM.Shared.Models;
using Serilog.Context;

namespace MPM.Core.Middleware;

public class TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // E-06: sanitización + CorrelationId != TraceId
        var raw = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(raw)) raw = raw.Trim();
        if (raw?.Length > 36) raw = raw.Substring(0, 36);
        if (raw != null && !Regex.IsMatch(raw, @"^[a-zA-Z0-9_-]{8,36}$")) raw = null;

        // fallback a X-Request-Id si X-Correlation-Id no es válido (compatibilidad)
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
        context.Items["X-Correlation-Id"] = correlationId;

        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("correlation_id", correlationId);
        try
        {
            Activity.Current?.AddBaggage("correlationId", correlationId);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "AddBaggage failed");
        }

        // Set headers immediately for testability + OnStarting for pipeline correctness
        // 037-B: W3C traceparent propagado en respuesta para verificacion OTel (si Activity existe).
        var traceparent = Activity.Current?.Id;
        context.Response.Headers["X-Correlation-Id"] = correlationId;
        context.Response.Headers["X-Trace-Id"] = traceId;
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

        var user = context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var tenantContext = new TenantContext
            {
                UserId = user.FindFirst("user_id")?.Value ?? "",
                TenantId = user.FindFirst("tenant_id")?.Value ?? "",
                Username = user.FindFirst("username")?.Value ?? "",
                Roles = user.FindAll(ClaimTypes.Role).Concat(user.FindAll("role")).Select(c => c.Value).Distinct().ToArray(),
                TenantName = user.FindFirst("tenant_name")?.Value ?? ""
            };
            context.Items["TenantContext"] = tenantContext;
        }

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TraceId", traceId))
        {
            await next(context);
        }
    }
}
