using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Administracion.Data;
using MPM.Modules.Administracion.Models;
using MPM.Shared.Models;

namespace MPM.Modules.Administracion.Controllers;

/// <summary>
/// Logs/auditoría del sistema para Admin/SuperAdmin. Lee los 5 orígenes
/// normalizados por usp_Admin_ListarLogs (V132): inicios de sesión,
/// sincronizaciones, corridas del scraper, extracción de documentos e
/// historial del proveedor de IA. Filtrable por tipo, rango de fechas y estado.
/// </summary>
[ApiController]
[Route("api/v1/admin/logs")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminLogsController(AdminLogsHandler handler) : ControllerBase
{
    private static readonly string[] TiposValidos = ["auth", "sync", "scraper", "extraccion", "ai_provider"];

    /// <summary>Lista los logs más recientes, opcionalmente filtrados.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<AdminLogItemDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    [ProducesResponseType(typeof(ApiResponse<object>), 403)]
    public async Task<ActionResult<ApiResponse<IEnumerable<AdminLogItemDto>>>> Listar(
        [FromQuery] string? tipo = null,
        [FromQuery] DateTime? desde = null,
        [FromQuery] DateTime? hasta = null,
        [FromQuery] string? estado = null,
        [FromQuery] int limite = 100,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(tipo) && !TiposValidos.Contains(tipo.Trim().ToLowerInvariant()))
            return BadRequest(ApiResponse<object>.Fail(
                "Tipo inválido. Valores válidos: " + string.Join(", ", TiposValidos)));

        if (desde.HasValue && hasta.HasValue && desde > hasta)
            return BadRequest(ApiResponse<object>.Fail("La fecha 'desde' no puede ser posterior a 'hasta'"));

        var logs = await handler.ListarLogsAsync(tipo, desde, hasta, estado, Math.Clamp(limite, 1, 500), ct);
        return Ok(ApiResponse<IEnumerable<AdminLogItemDto>>.Ok(logs));
    }
}
