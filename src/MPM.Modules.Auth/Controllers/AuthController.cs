using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using MPM.Core.Data;
using MPM.Modules.Auth.Data;
using MPM.Modules.Auth.Models;
using MPM.Shared.Models;
using MPM.Shared.Services;
using Dapper;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MPM.Modules.Auth.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    IConfiguration config,
    ILogger<AuthController> logger,
    DbConnectionFactory dbFactory,
    AuthHandler authHandler,
    IEmailService emailService,
    IHostEnvironment env) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            return BadRequest(ApiResponse<object>.Fail("Email y contraseña son requeridos", null));

        await using var conn = dbFactory.Create();
        await conn.OpenAsync();

        var parameters = new DynamicParameters();
        parameters.Add("p_email", request.Email.ToLower().Trim());
        parameters.Add("p_password", request.Password);
        parameters.Add("p_user_id", dbType: DbType.Int64, direction: ParameterDirection.InputOutput);
        parameters.Add("p_nombre", dbType: DbType.String, size: 200, direction: ParameterDirection.InputOutput);
        parameters.Add("p_roles", dbType: DbType.Object, direction: ParameterDirection.InputOutput);
        parameters.Add("p_tenant_id", dbType: DbType.String, size: 100, direction: ParameterDirection.InputOutput);
        parameters.Add("p_tenant_nombre", dbType: DbType.String, size: 200, direction: ParameterDirection.InputOutput);
        parameters.Add("p_error_msg", dbType: DbType.String, direction: ParameterDirection.InputOutput);

        await conn.ExecuteAsync(
            "CALL usp_Auth_ValidarUsuario(@p_email, @p_password, @p_user_id, @p_nombre, @p_roles, @p_tenant_id, @p_tenant_nombre, @p_error_msg)",
            parameters,
            commandType: CommandType.Text);

        var errorMsg = parameters.Get<string?>("p_error_msg");

        if (!string.IsNullOrEmpty(errorMsg))
        {
            logger.LogWarning("Login failed for {Email}: {Error}", request.Email, errorMsg);
            return Unauthorized(ApiResponse<object>.Fail(errorMsg, null));
        }

        var userId = parameters.Get<long>("p_user_id");
        var nombre = parameters.Get<string>("p_nombre") ?? "";
        var rolesArray = parameters.Get<string[]?>("p_roles") ?? [];
        var tenantId = parameters.Get<string?>("p_tenant_id") ?? Guid.NewGuid().ToString();
        var tenantNombre = parameters.Get<string?>("p_tenant_nombre") ?? "TIVIT Chile";

        var jwtSecret = config["JWT:Secret"] ?? "CHANGE-THIS-IN-PRODUCTION-MIN-32-CHARS";
        var jwtIssuer = config["JWT:Issuer"] ?? "TIVIT.MPM";

        var claims = new[]
        {
            new Claim("user_id", userId.ToString()),
            new Claim("tenant_id", tenantId),
            new Claim("username", request.Email.ToLower().Trim()),
            new Claim("tenant_name", tenantNombre),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        }.Concat(rolesArray.Select(r => new Claim(ClaimTypes.Role, r))
                          .Select(r => new Claim("role", r.Value)))
         .ToArray();

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: "MPM.Users",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(ApiResponse<object>.Ok(new
        {
            token = tokenString,
            expiresAt = token.ValidTo,
            user = new
            {
                userId = userId.ToString(),
                nombre = nombre,
                email = request.Email.ToLower().Trim(),
                roles = rolesArray,
                tenantId = tenantId,
                tenantNombre = tenantNombre
            }
        }));
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (string.IsNullOrEmpty(request.Email))
            return BadRequest(ApiResponse<object>.Fail("El email es requerido", null));

        await using var conn = dbFactory.Create();
        await conn.OpenAsync();

        var userExists = await conn.QueryFirstOrDefaultAsync<UserInfo>(
            "SELECT id, nombre FROM usuarios WHERE email = @email AND deleted_at IS NULL",
            new { email = request.Email.ToLower().Trim() });

        var parameters = new DynamicParameters();
        parameters.Add("p_email", request.Email.ToLower().Trim());
        parameters.Add("p_expiration_hours", 1);
        parameters.Add("p_token", dbType: DbType.String, size: 255, direction: ParameterDirection.InputOutput);
        parameters.Add("p_error_msg", dbType: DbType.String, direction: ParameterDirection.InputOutput);

        await conn.ExecuteAsync(
            "CALL usp_Auth_ForgotPassword(@p_email, @p_expiration_hours, @p_token, @p_error_msg)",
            parameters,
            commandType: CommandType.Text);

        var errorMsg = parameters.Get<string?>("p_error_msg");
        var token = parameters.Get<string?>("p_token");

        if (!string.IsNullOrEmpty(errorMsg))
        {
            logger.LogWarning("ForgotPassword error for {Email}: {Error}", request.Email, errorMsg);
            return BadRequest(ApiResponse<object>.Fail(errorMsg, null));
        }

        var resetUrl = $"{config["App:BaseUrl"] ?? "http://localhost:5173"}/reset-password/{token}";

        if (config.GetValue<bool>("App:LogResetToken", false) || !env.IsProduction())
        {
            logger.LogInformation("Password reset link for {Email}: {Url}", request.Email, resetUrl);
        }

        if (userExists != null)
        {
            await emailService.SendPasswordResetEmailAsync(request.Email.ToLower().Trim(), resetUrl, userExists.Nombre);
        }

        return Ok(ApiResponse<object>.Ok(new { message = "Si el email existe, recibirás instrucciones para restablecer tu contraseña" }));
    }

    [HttpGet("validate-reset-token/{token}")]
    public async Task<IActionResult> ValidateResetToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return BadRequest(ApiResponse<object>.Fail("Token requerido", null));

        var tokenRecord = await authHandler.ValidateResetTokenAsync(token);

        if (tokenRecord == null)
            return BadRequest(ApiResponse<object>.Fail("Token inválido", null));

        if (tokenRecord.UsedAt.HasValue)
            return BadRequest(ApiResponse<object>.Fail("Este token ya ha sido utilizado", null));

        if (tokenRecord.ExpiresAt < DateTime.UtcNow)
            return BadRequest(ApiResponse<object>.Fail("El token ha expirado", null));

        return Ok(ApiResponse<object>.Ok(new { valid = true }));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (string.IsNullOrEmpty(request.Token) || string.IsNullOrEmpty(request.NewPassword))
            return BadRequest(ApiResponse<object>.Fail("Token y nueva contraseña son requeridos", null));

        if (request.NewPassword.Length < 6)
            return BadRequest(ApiResponse<object>.Fail("La contraseña debe tener al menos 6 caracteres", null));

        await using var conn = dbFactory.Create();
        await conn.OpenAsync();

        var parameters = new DynamicParameters();
        parameters.Add("p_token", request.Token);
        parameters.Add("p_new_password", request.NewPassword);
        parameters.Add("p_email", dbType: DbType.String, size: 255, direction: ParameterDirection.InputOutput);
        parameters.Add("p_error_msg", dbType: DbType.String, direction: ParameterDirection.InputOutput);

        await conn.ExecuteAsync(
            "CALL usp_Auth_ActualizarPassword(@p_token, @p_new_password, @p_email, @p_error_msg)",
            parameters,
            commandType: CommandType.Text);

        var errorMsg = parameters.Get<string?>("p_error_msg");
        var email = parameters.Get<string?>("p_email");

        if (!string.IsNullOrEmpty(errorMsg))
        {
            logger.LogWarning("ResetPassword error: {Error}", errorMsg);
            return BadRequest(ApiResponse<object>.Fail(errorMsg, null));
        }

        logger.LogInformation("Password reset successful for {Email}", email);

        return Ok(ApiResponse<object>.Ok(new { message = "Contraseña restablecida exitosamente" }));
    }
}