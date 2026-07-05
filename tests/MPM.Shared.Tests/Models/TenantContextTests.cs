using MPM.Shared.Models;
using FluentAssertions;
using Xunit;

namespace MPM.Shared.Tests.Models;

public class TenantContextTests
{
    [Fact]
    public void TenantContext_DefaultValues()
    {
        var ctx = new TenantContext();
        ctx.TenantId.Should().Be(string.Empty);
        ctx.UserId.Should().Be(string.Empty);
        ctx.Username.Should().Be(string.Empty);
        ctx.Roles.Should().NotBeNull();
        ctx.TenantName.Should().Be(string.Empty);
    }

    [Fact]
    public void TenantContext_WithValues()
    {
        var tenantId = "guid-tenant-001";
        var userId = "guid-user-001";
        var ctx = new TenantContext
        {
            TenantId = tenantId,
            UserId = userId,
            Username = "admin@tivit.cl",
            Roles = ["SuperAdmin", "User"],
            TenantName = "TIVIT Chile"
        };
        ctx.TenantId.Should().Be(tenantId);
        ctx.UserId.Should().Be(userId);
        ctx.Username.Should().Be("admin@tivit.cl");
        ctx.Roles.Should().Contain("SuperAdmin");
        ctx.Roles.Should().Contain("User");
        ctx.TenantName.Should().Be("TIVIT Chile");
    }
}
