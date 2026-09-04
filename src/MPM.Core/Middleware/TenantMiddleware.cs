using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MPM.Shared.Models;
using Serilog.Context;

namespace MPM.Core.Middleware;

public class TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Este middleware debe correr DESPUÉS de UseAuthentication (ver Program.cs orden)
        // para que context.User ya esté poblado. En el pipeline normal CorrelationId ya lo
        // resolvió CorrelationIdMiddleware antes; si falta (orden distinto), se resuelve acá
        // con la misma lógica centralizada para no romper el contrato de correlación.
        var correlationId = context.Items["CorrelationId"] as string;
        var traceId = context.Items["TraceId"] as string;
        if (string.IsNullOrEmpty(correlationId) || string.IsNullOrEmpty(traceId))
        {
            (correlationId, traceId) = CorrelationIdMiddleware.Resolve(context);
            context.Items["CorrelationId"] = correlationId;
            context.Items["TraceId"] = traceId;
            context.Response.Headers["X-Correlation-Id"] = correlationId;
            context.Response.Headers["X-Trace-Id"] = traceId;
        }
        var spanId = Activity.Current?.SpanId.ToString() ?? "";

        var user = context.User;
        TenantContext? tenantContext = null;
        if (user?.Identity?.IsAuthenticated == true)
        {
            tenantContext = new TenantContext
            {
                UserId = user.FindFirst("user_id")?.Value ?? "",
                TenantId = user.FindFirst("tenant_id")?.Value ?? "",
                Username = user.FindFirst("username")?.Value ?? "",
                Roles = user.FindAll(ClaimTypes.Role).Concat(user.FindAll("role")).Select(c => c.Value).Distinct().ToArray(),
                TenantName = user.FindFirst("tenant_name")?.Value ?? ""
            };
            // Solo fijar si tiene UserId válido
            if (!string.IsNullOrWhiteSpace(tenantContext.UserId))
            {
                context.Items["TenantContext"] = tenantContext;
                Activity.Current?.SetTag("user.id", tenantContext.UserId);
                if (!string.IsNullOrWhiteSpace(tenantContext.Username))
                    Activity.Current?.SetTag("user.name", tenantContext.Username);
            }
        }

        var userIdForLog = tenantContext?.UserId ?? user?.FindFirst("user_id")?.Value ?? "";
        using (LogContext.PushProperty("UserId", userIdForLog))
        using (LogContext.PushProperty("SpanId", spanId))
        {
            await next(context);
        }
    }
}
