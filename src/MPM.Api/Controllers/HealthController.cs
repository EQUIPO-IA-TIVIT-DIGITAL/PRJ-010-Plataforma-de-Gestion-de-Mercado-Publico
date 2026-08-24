using Microsoft.AspNetCore.Mvc;
using MPM.Core.Data;
using MPM.Modules.Auth.Data;

namespace MPM.Api.Controllers;

[ApiController]
public class HealthController(DbConnectionFactory dbFactory, AuthHandler authHandler) : ControllerBase
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    // 037-A: /health agregado y /health/licitaciones ahora via MapHealthChecks (SELECT 1) - ver Program.cs
    // Se deja solo auth y mensajeria aquí para no duplicar ruta y evitar SELECT COUNT(*) pesado (OBS-R006).
    [HttpGet("/health/auth")]
    public async Task<IActionResult> HealthAuth()
    {
        try
        {
            var totalUsers = await authHandler.CountActiveUsersAsync();
            return Ok(new
            {
                status = "healthy",
                module = "auth",
                totalUsers,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception)
        {
            // No exponer ex.Message (PII / detalles internos)
            return StatusCode(503, new { status = "unhealthy", module = "auth", timestamp = DateTime.UtcNow });
        }
    }

    [HttpGet("/health/mensajeria")]
    public async Task<IActionResult> HealthMensajeria()
    {
        try
        {
            await using var conn = _dbFactory.Create();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM conversaciones LIMIT 1";
            await cmd.ExecuteScalarAsync();
            return Ok(new { status = "healthy", module = "mensajeria" });
        }
        catch (Exception)
        {
            return StatusCode(503, new { status = "unhealthy", module = "mensajeria" });
        }
    }
}