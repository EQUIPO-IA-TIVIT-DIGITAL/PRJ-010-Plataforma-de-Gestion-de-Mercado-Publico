using MPM.Shared.Models;
using Microsoft.AspNetCore.Http;

namespace MPM.Core.Middleware;

public class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var tenantContext = new TenantContext
            {
                UserId = user.FindFirst("user_id")?.Value ?? "",
                TenantId = user.FindFirst("tenant_id")?.Value ?? "",
                Username = user.FindFirst("username")?.Value ?? "",
                Roles = user.FindAll("role").Select(c => c.Value).ToArray(),
                TenantName = user.FindFirst("tenant_name")?.Value ?? ""
            };
            context.Items["TenantContext"] = tenantContext;
        }

        await next(context);
    }
}
