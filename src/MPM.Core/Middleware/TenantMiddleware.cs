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
        // 037-C OBS-R001/R007: enrich Activity con SpanId y tags para Serilog/OTel
        var spanId = Activity.Current?.SpanId.ToString();
        var currentTraceId = Activity.Current?.TraceId.ToString() ?? traceId;

        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("correlation_id", correlationId);
        if (!string.IsNullOrEmpty(spanId))
            Activity.Current?.SetTag("spanId", spanId);
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
        TenantContext? tenantContextEarly = null;
        if (user?.Identity?.IsAuthenticated == true)
        {
            tenantContextEarly = new TenantContext
            {
                UserId = user.FindFirst("user_id")?.Value ?? "",
                TenantId = user.FindFirst("tenant_id")?.Value ?? "",
                Username = user.FindFirst("username")?.Value ?? "",
                Roles = user.FindAll(ClaimTypes.Role).Concat(user.FindAll("role")).Select(c => c.Value).Distinct().ToArray(),
                TenantName = user.FindFirst("tenant_name")?.Value ?? ""
            };
            context.Items["TenantContext"] = tenantContextEarly;
            // 037-C OBS-R001: tag user.id en Activity cuando autenticado (temprano, si auth ya corrió)
            if (!string.IsNullOrWhiteSpace(tenantContextEarly.UserId))
                Activity.Current?.SetTag("user.id", tenantContextEarly.UserId);
            if (!string.IsNullOrWhiteSpace(tenantContextEarly.Username))
                Activity.Current?.SetTag("user.name", tenantContextEarly.Username);
        }

        // 037-C gap OBS-R007: LogContext con SpanId y UserId para Serilog JSON (TraceId/SpanId/UserId/Module)
        var userIdForLog = tenantContextEarly?.UserId ?? "";
        var spanIdForLog = spanId ?? "";

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TraceId", currentTraceId))
        using (LogContext.PushProperty("SpanId", spanIdForLog))
        using (LogContext.PushProperty("UserId", userIdForLog))
        {
            await next(context);

            // Post-next: si la autenticación ocurrió después del middleware (orden Program.cs),
            // enriquecer Activity con user.id de forma tardía (OBS-R001)
            var lateUser = context.User;
            if (lateUser?.Identity?.IsAuthenticated == true)
            {
                var lateUserId = lateUser.FindFirst("user_id")?.Value ?? lateUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrWhiteSpace(lateUserId))
                    Activity.Current?.SetTag("user.id", lateUserId);
                var lateSpanId = Activity.Current?.SpanId.ToString();
                if (!string.IsNullOrEmpty(lateSpanId))
                    Activity.Current?.SetTag("spanId", lateSpanId);
            }
        }
    }
}
