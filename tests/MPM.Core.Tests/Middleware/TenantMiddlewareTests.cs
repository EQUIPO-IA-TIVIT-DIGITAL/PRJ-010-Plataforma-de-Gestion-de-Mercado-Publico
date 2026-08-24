using MPM.Core.Middleware;
using MPM.Shared.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Xunit;

namespace MPM.Core.Tests.Middleware;

public class TenantMiddlewareTests
{
    private static DefaultHttpContext CreateHttpContext(ClaimsPrincipal? user = null)
    {
        var context = new DefaultHttpContext();
        if (user != null)
            context.User = user;
        return context;
    }

    private static ClaimsPrincipal CreateAuthenticatedUser(string userId, string tenantId, string username, string[] roles, string tenantName)
    {
        var claims = new List<Claim>
        {
            new("user_id", userId),
            new("tenant_id", tenantId),
            new("username", username),
            new("tenant_name", tenantName)
        };
        claims.AddRange(roles.Select(r => new Claim("role", r)));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task Sets_TenantContext_From_AuthenticatedUser_Claims()
    {
        var userId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid().ToString();
        var user = CreateAuthenticatedUser(userId.ToString(), tenantId.ToString(), "admin@tivit.cl", ["SuperAdmin"], "TIVIT Chile");
        var context = CreateHttpContext(user);
        var nextCalled = false;

        var middleware = new TenantMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, NullLogger<TenantMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        var tenantCtx = context.Items["TenantContext"] as TenantContext;
        tenantCtx.Should().NotBeNull();
        tenantCtx!.UserId.Should().Be(userId);
        tenantCtx.TenantId.Should().Be(tenantId);
        tenantCtx.Username.Should().Be("admin@tivit.cl");
        tenantCtx.Roles.Should().Contain("SuperAdmin");
        tenantCtx.TenantName.Should().Be("TIVIT Chile");
    }

    [Fact]
    public async Task Skips_TenantContext_For_AnonymousUser()
    {
        var context = CreateHttpContext(null);
        var nextCalled = false;

        var middleware = new TenantMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, NullLogger<TenantMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Items.ContainsKey("TenantContext").Should().BeFalse();
    }

    [Fact]
    public async Task Handles_Invalid_Guid_In_Claims_Gracefully()
    {
        var claims = new List<Claim>
        {
            new("user_id", "not-a-guid"),
            new("tenant_id", "also-not-a-guid"),
            new("username", "testuser"),
            new("tenant_name", "Test Tenant"),
            new("role", "User")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);
        var context = CreateHttpContext(user);
        var nextCalled = false;

        var middleware = new TenantMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, NullLogger<TenantMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        var tenantCtx = context.Items["TenantContext"] as TenantContext;
        tenantCtx.Should().NotBeNull();
        tenantCtx!.UserId.Should().Be("not-a-guid");
        tenantCtx.TenantId.Should().Be("also-not-a-guid");
    }
}