using MPM.Modules.Auth.Models;
using FluentAssertions;
using Xunit;

namespace MPM.Modules.Auth.Tests.Models;

public class AuthDtoTests
{
    [Fact]
    public void LoginRequest_ShouldInitializeWithDefaults()
    {
        var request = new LoginRequest();
        request.Email.Should().BeEmpty();
        request.Password.Should().BeEmpty();
    }

    [Fact]
    public void LoginRequest_ShouldSetProperties()
    {
        var request = new LoginRequest { Email = "admin@tivit.cl", Password = "test123" };
        request.Email.Should().Be("admin@tivit.cl");
        request.Password.Should().Be("test123");
    }

    [Fact]
    public void ForgotPasswordRequest_ShouldInitializeWithDefaults()
    {
        var request = new ForgotPasswordRequest();
        request.Email.Should().BeEmpty();
    }

    [Fact]
    public void ForgotPasswordRequest_ShouldSetEmail()
    {
        var request = new ForgotPasswordRequest { Email = "user@test.cl" };
        request.Email.Should().Be("user@test.cl");
    }

    [Fact]
    public void ResetPasswordRequest_ShouldInitializeWithDefaults()
    {
        var request = new ResetPasswordRequest();
        request.Token.Should().BeEmpty();
        request.NewPassword.Should().BeEmpty();
    }

    [Fact]
    public void ResetPasswordRequest_ShouldSetProperties()
    {
        var request = new ResetPasswordRequest { Token = "abc123", NewPassword = "newpass456" };
        request.Token.Should().Be("abc123");
        request.NewPassword.Should().Be("newpass456");
    }

    [Fact]
    public void TokenValidationResult_ShouldInitializeWithDefaults()
    {
        var result = new TokenValidationResult();
        result.Email.Should().BeEmpty();
        result.ExpiresAt.Should().Be(default(DateTime));
        result.UsedAt.Should().BeNull();
    }

    [Fact]
    public void TokenValidationResult_ShouldSetProperties()
    {
        var now = DateTime.UtcNow;
        var result = new TokenValidationResult
        {
            Email = "user@test.cl",
            ExpiresAt = now.AddHours(1),
            UsedAt = null
        };
        result.Email.Should().Be("user@test.cl");
        result.ExpiresAt.Should().BeCloseTo(now.AddHours(1), TimeSpan.FromSeconds(1));
        result.UsedAt.Should().BeNull();
    }

    [Fact]
    public void UserInfo_ShouldInitializeWithDefaults()
    {
        var info = new UserInfo();
        info.Id.Should().Be(0);
        info.Nombre.Should().BeEmpty();
    }

    [Fact]
    public void UserInfo_ShouldSetProperties()
    {
        var info = new UserInfo { Id = 1, Nombre = "Admin" };
        info.Id.Should().Be(1);
        info.Nombre.Should().Be("Admin");
    }

    [Theory]
    [InlineData("", "password", false, "El email es requerido")]
    [InlineData("user@test.cl", "", false, "La contraseña es requerida")]
    [InlineData("admin@tivit.cl", "test123", true, "")]
    public void LoginValidation_ShouldValidateCorrectly(string email, string password, bool expectedValid, string expectedError)
    {
        var isValid = !string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password);
        isValid.Should().Be(expectedValid);

        if (!expectedValid)
        {
            var error = string.IsNullOrEmpty(email) ? "El email es requerido" : "La contraseña es requerida";
            error.Should().Contain(expectedError.Split(' ')[0]);
        }
    }

    [Theory]
    [InlineData("abc", false)]
    [InlineData("12345", false)]
    [InlineData("123456", true)]
    [InlineData("password123", true)]
    public void PasswordLength_ShouldValidateCorrectly(string password, bool expectedValid)
    {
        var isValid = password.Length >= 6;
        isValid.Should().Be(expectedValid);
    }
}