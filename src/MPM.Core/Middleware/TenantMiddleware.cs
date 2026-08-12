using MPM.Shared.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace MPM.Core.Middleware;

public class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
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
