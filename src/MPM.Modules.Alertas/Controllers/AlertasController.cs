using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Alertas.Models;
using MPM.Modules.Alertas.Services;
using MPM.Shared.Models;

namespace MPM.Modules.Alertas.Controllers;

[ApiController]
[Route("api/v1/alertas")]
[Authorize]
public class AlertasController(AlertasService service, AlertasMatchingService matchingService) : ControllerBase
{
    private TenantContext? GetTenant() => HttpContext.Items["TenantContext"] as TenantContext;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ReglaAlertaDto>>), 200)]
    public async Task<ActionResult<ApiResponse<List<ReglaAlertaDto>>>> Listar()
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var reglas = await service.ListarAsync(tenant.UserId);
        return Ok(ApiResponse<List<ReglaAlertaDto>>.Ok(reglas));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ReglaAlertaDto>), 201)]
    public async Task<ActionResult<ApiResponse<ReglaAlertaDto>>> Crear([FromBody] CrearReglaRequest request)
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        if (string.IsNullOrWhiteSpace(request.Keyword) || request.Keyword.Trim().Length < 2)
            return BadRequest(ApiResponse<object>.Fail("ALT_002: keyword debe tener al menos 2 caracteres"));

        var regla = await service.CrearAsync(tenant.UserId, request);
        return CreatedAtAction(nameof(Listar), ApiResponse<ReglaAlertaDto>.Ok(regla));
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<ActionResult<ApiResponse<object>>> Editar(long id, [FromBody] CrearReglaRequest request)
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var error = await service.EditarAsync(id, tenant.UserId, request);
        if (error != null) return NotFound(ApiResponse<object>.Fail(error));

        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPatch("{id:long}/toggle")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<ActionResult<ApiResponse<object>>> Toggle(long id)
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var (activa, error) = await service.ToggleAsync(id, tenant.UserId);
        if (error != null) return NotFound(ApiResponse<object>.Fail(error));

        return Ok(ApiResponse<object>.Ok(new { activa }));
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<ActionResult<ApiResponse<object>>> Eliminar(long id)
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var error = await service.EliminarAsync(id, tenant.UserId);
        if (error != null) return NotFound(ApiResponse<object>.Fail(error));

        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("mi-telegram")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<ActionResult<ApiResponse<object>>> GuardarMiTelegram([FromBody] GuardarTelegramChatIdRequest request)
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        if (string.IsNullOrWhiteSpace(request.TelegramChatId))
            return BadRequest(ApiResponse<object>.Fail("ALT_003: telegramChatId es requerido"));

        await service.GuardarMiTelegramAsync(tenant.UserId, request.TelegramChatId.Trim());
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    /// <summary>024-inteligencia-competencia-alertas / US3: canal de alertas por correo, adicional a Telegram.</summary>
    [HttpPost("mi-email")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<ActionResult<ApiResponse<object>>> GuardarMiEmail([FromBody] GuardarEmailAlertasRequest request)
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        if (string.IsNullOrWhiteSpace(request.EmailAlertas) || !request.EmailAlertas.Contains('@'))
            return BadRequest(ApiResponse<object>.Fail("ALT_005: emailAlertas es requerido y debe ser un correo válido"));

        await service.GuardarMiEmailAsync(tenant.UserId, request.EmailAlertas.Trim());
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    /// <summary>
    /// Genera un link de un solo clic para conectar Telegram sin copiar/pegar el chat_id
    /// a mano (requiere que el webhook de Telegram esté configurado — ver TelegramWebhookController).
    /// </summary>
    [HttpPost("mi-telegram/link")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<ActionResult<ApiResponse<object>>> GenerarLinkTelegram()
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var (_, url) = await service.GenerarLinkTelegramAsync(tenant.UserId);
        return Ok(ApiResponse<object>.Ok(new { url }));
    }

    [HttpGet("{id:long}/historial")]
    [ProducesResponseType(typeof(ApiResponse<HistorialAlertasDto>), 200)]
    public async Task<ActionResult<ApiResponse<HistorialAlertasDto>>> Historial(long id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var historial = await service.HistorialAsync(id, tenant.UserId, page, pageSize);
        return Ok(ApiResponse<HistorialAlertasDto>.Ok(historial));
    }

    [HttpPost("{id:long}/probar")]
    [ProducesResponseType(typeof(ApiResponse<ProbarAlertaResponse>), 200)]
    public async Task<ActionResult<ApiResponse<ProbarAlertaResponse>>> Probar(long id, [FromBody] ProbarAlertaRequest request)
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var licitacion = new LicitacionParaMatching(
            request.LicitacionId, request.CodigoExterno, request.Nombre, request.Descripcion,
            request.Monto, request.TipoLicitacion, request.Organismo, request.FechaCierre, request.Link);

        var resultado = await matchingService.ProbarAsync(id, tenant.UserId, licitacion);
        return Ok(ApiResponse<ProbarAlertaResponse>.Ok(resultado));
    }
}
