namespace MPM.Modules.Auth.Models;

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class TokenValidationResult
{
    public string Email { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}

public class UserInfo
{
    public long Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public class UsuarioItemDto
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? TenantNombre { get; set; }
}