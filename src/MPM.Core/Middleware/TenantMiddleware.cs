using MPM.Shared.Models;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Security.Claims;

namespace MPM.Core.Middleware;

public class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // 037-A: propagar X-Correlation-Id / TraceId
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? context.Request.Headers["X-Request-Id"].FirstOrDefault()
            ?? Activity.Current?.TraceId.ToString()
            ?? context.TraceIdentifier
            ?? Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(correlationId))
            correlationId = Guid.NewGuid().ToString("N");

        // Normalizar: si viene con prefijo traceparent, extraer traceId (32 hex)
        // pero respetar valor cliente si es un id válido.
        context.Items["CorrelationId"] = correlationId;
        context.Items["TraceId"] = correlationId;
        context.Items["X-Correlation-Id"] = correlationId;

        // Enriquecer Activity actual para Serilog WithTraceId
        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("correlation_id", correlationId);
        try { Activity.Current?.AddBaggage("correlationId", correlationId); } catch { }

        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey("X-Correlation-Id"))
                context.Response.Headers["X-Correlation-Id"] = correlationId;
            // También exponer TraceId para debug (no PII)
            if (!context.Response.Headers.ContainsKey("X-Trace-Id"))
                context.Response.Headers["X-Trace-Id"] = Activity.Current?.TraceId.ToString() ?? correlationId;
            return Task.CompletedTask;
        });

        var user = context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            // OJO: el claim JWT corto "role" se re-mapea a ClaimTypes.Role al deserializar
            // (MapInboundClaims default en JwtSecurityTokenHandler) — FindAll("role") nunca
            // encuentra nada. Se lee ClaimTypes.Role para que TenantContext.Roles funcione.
            var tenantContext = new TenantContext
            {
                UserId = user.FindFirst("user_id")?.Value ?? "",
                TenantId = user.FindFirst("tenant_id")?.Value ?? "",
                Username = user.FindFirst("username")?.Value ?? "",
                Roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray(),
                TenantName = user.FindFirst("tenant_name")?.Value ?? ""
            };
            context.Items["TenantContext"] = tenantContext;
        }

        await next(context);
    }
}
