using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Mensajeria.Models;
using MPM.Modules.Mensajeria.Services;
using MPM.Shared.Models;

namespace MPM.Modules.Mensajeria.Controllers;

[ApiController]
[Route("api/v1/presencia")]
[Authorize]
public class PresenciaController(PresenciaService service) : ControllerBase
{
    private readonly PresenciaService _service = service;

    /// <summary>
    /// Obtiene el estado de presencia de una lista de usuarios (comma-separated)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<PresenciaDto>>), 200)]
    public async Task<ActionResult<ApiResponse<List<PresenciaDto>>>> Obtener([FromQuery] string userIds, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userIds))
            return BadRequest(ApiResponse<object>.Fail("userIds es requerido", [new ErrorDetail { Code = "VAL_001", Field = "userIds", Message = "userIds es requerido" }]));

        var userIdList = userIds.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        var result = await _service.ObtenerAsync(userIdList, ct);
        return Ok(ApiResponse<List<PresenciaDto>>.Ok(result));
    }

    /// <summary>
    /// Notifica que el usuario está escribiendo en una conversación
    /// </summary>
    [HttpPost("typing")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<ActionResult<ApiResponse<object>>> NotificarTyping([FromBody] TypingRequest request, CancellationToken ct = default)
    {
        var tenant = HttpContext.Items["TenantContext"] as TenantContext;
        if (tenant == null)
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var estado = request.Escribiendo ? "escribiendo" : "online";
        await _service.ActualizarAsync(tenant.UserId.ToString(), estado, request.ConversacionId, ct);

        return Ok(ApiResponse<object>.Ok(new { result = true }));
    }
}

public class TypingRequest
{
    public long ConversacionId { get; set; }
    public bool Escribiendo { get; set; }
}
