using Microsoft.AspNetCore.Mvc;
using MPM.Core.Data;
using MPM.Modules.Auth.Data;

namespace MPM.Api.Controllers;

[ApiController]
public class HealthController(DbConnectionFactory dbFactory, AuthHandler authHandler) : ControllerBase
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    [HttpGet("/health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

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
        catch (Exception ex)
        {
            return StatusCode(503, new { status = "unhealthy", module = "auth", error = ex.Message, timestamp = DateTime.UtcNow });
        }
    }

    [HttpGet("/health/licitaciones")]
    public async Task<IActionResult> HealthLicitaciones()
    {
        try
        {
            await using var conn = _dbFactory.Create();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM licitaciones WHERE deleted_at IS NULL";
            var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());

            using var cmd2 = conn.CreateCommand();
            cmd2.CommandText = "SELECT MAX(ejecutado_en) FROM sync_log WHERE estado = 'COMPLETADO'";
            var lastSync = await cmd2.ExecuteScalarAsync();

            return Ok(new
            {
                status = "healthy",
                module = "licitaciones",
                totalRecords = count,
                lastSync = lastSync != DBNull.Value ? Convert.ToDateTime(lastSync) : (DateTime?)null,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { status = "unhealthy", module = "licitaciones", error = ex.Message, timestamp = DateTime.UtcNow });
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
        catch (Exception ex)
        {
            return StatusCode(503, new { status = "unhealthy", module = "mensajeria", error = ex.Message });
        }
    }
}