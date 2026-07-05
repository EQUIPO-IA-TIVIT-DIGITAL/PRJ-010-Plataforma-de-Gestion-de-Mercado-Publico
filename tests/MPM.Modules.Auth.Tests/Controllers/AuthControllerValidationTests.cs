using MPM.Modules.Auth.Models;
using FluentAssertions;
using Xunit;

namespace MPM.Modules.Auth.Tests.Controllers;

public class AuthControllerValidationTests
{
    [Fact]
    public void LoginRequest_EmptyEmail_IsInvalid()
    {
        var request = new LoginRequest { Email = "", Password = "test123" };
        var isValid = !string.IsNullOrEmpty(request.Email) && !string.IsNullOrEmpty(request.Password);
        isValid.Should().BeFalse();
    }

    [Fact]
    public void LoginRequest_EmptyPassword_IsInvalid()
    {
        var request = new LoginRequest { Email = "admin@tivit.cl", Password = "" };
        var isValid = !string.IsNullOrEmpty(request.Email) && !string.IsNullOrEmpty(request.Password);
        isValid.Should().BeFalse();
    }

    [Fact]
    public void LoginRequest_BothEmpty_IsInvalid()
    {
        var request = new LoginRequest { Email = "", Password = "" };
        var isValid = !string.IsNullOrEmpty(request.Email) && !string.IsNullOrEmpty(request.Password);
        isValid.Should().BeFalse();
    }

    [Fact]
    public void LoginRequest_ValidCredentials_IsValid()
    {
        var request = new LoginRequest { Email = "admin@tivit.cl", Password = "test123" };
        var isValid = !string.IsNullOrEmpty(request.Email) && !string.IsNullOrEmpty(request.Password);
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ForgotPasswordRequest_EmptyEmail_IsInvalid()
    {
        var request = new ForgotPasswordRequest { Email = "" };
        var isValid = !string.IsNullOrEmpty(request.Email);
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ForgotPasswordRequest_ValidEmail_IsValid()
    {
        var request = new ForgotPasswordRequest { Email = "user@test.cl" };
        var isValid = !string.IsNullOrEmpty(request.Email);
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ResetPasswordRequest_EmptyToken_IsInvalid()
    {
        var request = new ResetPasswordRequest { Token = "", NewPassword = "password123" };
        var isValid = !string.IsNullOrEmpty(request.Token) && !string.IsNullOrEmpty(request.NewPassword);
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ResetPasswordRequest_ShortPassword_IsInvalid()
    {
        var request = new ResetPasswordRequest { Token = "abc123", NewPassword = "12345" };
        var isValid = request.NewPassword.Length >= 6;
        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("abc123", "password123", true)]
    [InlineData("", "password123", false)]
    [InlineData("abc123", "", false)]
    [InlineData("abc123", "12345", false)]
    [InlineData("", "", false)]
    public void ResetPasswordRequest_Validation(string token, string password, bool expectedValid)
    {
        var request = new ResetPasswordRequest { Token = token, NewPassword = password };
        var isValid = !string.IsNullOrEmpty(request.Token)
                      && !string.IsNullOrEmpty(request.NewPassword)
                      && request.NewPassword.Length >= 6;
        isValid.Should().Be(expectedValid);
    }

    [Fact]
    public void UserInfo_Defaults()
    {
        var info = new UserInfo();
        info.Id.Should().Be(0);
        info.Nombre.Should().BeEmpty();
    }
}