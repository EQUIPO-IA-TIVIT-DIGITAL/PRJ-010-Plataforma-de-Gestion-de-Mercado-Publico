using MPM.Modules.Auth.Data;
using MPM.Modules.Auth.Models;
using FluentAssertions;
using Xunit;

namespace MPM.Modules.Auth.Tests.Data;

public class AuthHandlerTests
{
    [Fact]
    public void TokenValidationResult_Defaults()
    {
        var result = new TokenValidationResult();
        result.Email.Should().BeEmpty();
        result.ExpiresAt.Should().Be(default(DateTime));
        result.UsedAt.Should().BeNull();
    }

    [Fact]
    public void TokenValidationResult_WithValues()
    {
        var expiresAt = DateTime.UtcNow.AddHours(1);
        var usedAt = DateTime.UtcNow;
        var result = new TokenValidationResult
        {
            Email = "admin@tivit.cl",
            ExpiresAt = expiresAt,
            UsedAt = usedAt
        };
        result.Email.Should().Be("admin@tivit.cl");
        result.ExpiresAt.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
        result.UsedAt.Should().BeCloseTo(usedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void TokenValidationResult_ExpiredToken_NotUsed()
    {
        var result = new TokenValidationResult
        {
            Email = "user@test.cl",
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
            UsedAt = null
        };
        result.ExpiresAt.Should().BeBefore(DateTime.UtcNow);
        result.UsedAt.Should().BeNull();
    }

    [Fact]
    public void TokenValidationResult_ValidToken_NotUsed()
    {
        var result = new TokenValidationResult
        {
            Email = "user@test.cl",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            UsedAt = null
        };
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        result.UsedAt.Should().BeNull();
    }

    [Fact]
    public void TokenValidationResult_UsedToken()
    {
        var result = new TokenValidationResult
        {
            Email = "user@test.cl",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            UsedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        result.UsedAt.Should().NotBeNull();
    }
}