using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Mensajeria.Models;
using MPM.Modules.Mensajeria.Services;
using MPM.Shared.Models;

namespace MPM.Modules.Mensajeria.Controllers;

[ApiController]
[Route("api/v1/conversaciones")]
[Authorize]
public class ConversacionController(ConversacionService service) : ControllerBase
{
    private readonly ConversacionService _service = service;

    /// <summary>
    /// Lista las conversaciones del usuario autenticado con último mensaje y conteo de no leídos
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<ConversacionResumenDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    public async Task<ActionResult<ApiResponse<PaginatedResult<ConversacionResumenDto>>>> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string sortBy = "updated_at",
        [FromQuery] string sortDir = "desc",
        CancellationToken ct = default)
    {
        var tenant = HttpContext.Items["TenantContext"] as TenantContext;
        if (tenant == null)
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var result = await _service.ListarAsync(tenant.UserId.ToString(), page, pageSize, search, sortBy, sortDir, ct);
        return Ok(ApiResponse<PaginatedResult<ConversacionResumenDto>>.Ok(result));
    }

    /// <summary>
    /// Obtiene el detalle de una conversación con sus participantes
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<ConversacionDetalleDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<ActionResult<ApiResponse<ConversacionDetalleDto>>> Obtener(long id, CancellationToken ct = default)
    {
        var tenant = HttpContext.Items["TenantContext"] as TenantContext;
        if (tenant == null)
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var result = await _service.ObtenerAsync(id, tenant.UserId.ToString(), ct);
        if (result == null)
            return NotFound(ApiResponse<object>.Fail("Conversación no encontrada", [new ErrorDetail { Code = "MSG_001", Message = "Conversación no encontrada" }]));

        return Ok(ApiResponse<ConversacionDetalleDto>.Ok(result));
    }

    /// <summary>
    /// Crea una nueva conversación (directa o grupal)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ConversacionDetalleDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 409)]
    public async Task<ActionResult<ApiResponse<ConversacionDetalleDto>>> Crear([FromBody] CrearConversacionRequest request, CancellationToken ct = default)
    {
        var tenant = HttpContext.Items["TenantContext"] as TenantContext;
        if (tenant == null)
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        if (request.ParticipanteIds == null || request.ParticipanteIds.Count == 0)
            return BadRequest(ApiResponse<object>.Fail("participanteIds es requerido", [new ErrorDetail { Code = "VAL_001", Field = "participanteIds", Message = "participanteIds es requerido" }]));

        if (request.Asunto != null && request.Asunto.Length > 200)
            return BadRequest(ApiResponse<object>.Fail("asunto excede el largo máximo", [new ErrorDetail { Code = "VAL_008", Field = "asunto", Message = "asunto excede el largo máximo de 200 caracteres" }]));

        var (id, error) = await _service.CrearAsync(request.Tipo, request.Asunto, request.LicitacionId, request.ParticipanteIds, tenant.UserId.ToString(), ct);

        if (error != null)
        {
            if (error.StartsWith("MSG_002"))
                return Conflict(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "MSG_002", Message = error }]));
            if (error.StartsWith("MSG_004"))
                return UnprocessableEntity(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "MSG_004", Message = error }]));
            return BadRequest(ApiResponse<object>.Fail(error));
        }

        var result = await _service.ObtenerAsync(id, tenant.UserId.ToString(), ct);
        return CreatedAtAction(nameof(Obtener), new { id }, ApiResponse<ConversacionDetalleDto>.Ok(result!));
    }

    /// <summary>
    /// Actualiza el asunto de una conversación grupal (solo admin)
    /// </summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<ConversacionDetalleDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 403)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<ActionResult<ApiResponse<ConversacionDetalleDto>>> Actualizar(long id, [FromBody] ActualizarConversacionRequest request, CancellationToken ct = default)
    {
        var tenant = HttpContext.Items["TenantContext"] as TenantContext;
        if (tenant == null)
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var error = await _service.ActualizarAsync(id, request.Asunto, tenant.UserId.ToString(), ct);
        if (error != null)
        {
            if (error.StartsWith("MSG_001"))
                return NotFound(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "MSG_001", Message = error }]));
            if (error.StartsWith("AUTH_001"))
                return StatusCode(403, ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "AUTH_001", Message = error }]));
            if (error.StartsWith("MSG_003"))
                return UnprocessableEntity(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "MSG_003", Message = error }]));
            return BadRequest(ApiResponse<object>.Fail(error));
        }

        var result = await _service.ObtenerAsync(id, tenant.UserId.ToString(), ct);
        return Ok(ApiResponse<ConversacionDetalleDto>.Ok(result));
    }

    /// <summary>
    /// Abandona una conversación. Si es el último participante, la conversación se elimina
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<ActionResult<ApiResponse<object>>> Abandonar(long id, CancellationToken ct = default)
    {
        var tenant = HttpContext.Items["TenantContext"] as TenantContext;
        if (tenant == null)
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var error = await _service.AbandonarAsync(id, tenant.UserId.ToString(), ct);
        if (error != null)
        {
            if (error.StartsWith("MSG_001"))
                return NotFound(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "MSG_001", Message = error }]));
            if (error.StartsWith("AUTH_001"))
                return StatusCode(403, ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "AUTH_001", Message = error }]));
            return BadRequest(ApiResponse<object>.Fail(error));
        }

        return Ok(ApiResponse<object>.Ok(new { result = true }));
    }

    /// <summary>
    /// Agrega un participante a una conversación grupal (solo admin)
    /// </summary>
    [HttpPost("{id:long}/participantes")]
    [ProducesResponseType(typeof(ApiResponse<ParticipanteItemDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), 403)]
    [ProducesResponseType(typeof(ApiResponse<object>), 409)]
    public async Task<ActionResult<ApiResponse<ParticipanteItemDto>>> AgregarParticipante(long id, [FromBody] AgregarParticipanteRequest request, CancellationToken ct = default)
    {
        var tenant = HttpContext.Items["TenantContext"] as TenantContext;
        if (tenant == null)
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var error = await _service.AgregarParticipanteAsync(id, request.UserId, request.Rol, tenant.UserId.ToString(), ct);
        if (error != null)
        {
            if (error.StartsWith("MSG_001"))
                return NotFound(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "MSG_001", Message = error }]));
            if (error.StartsWith("AUTH_001"))
                return StatusCode(403, ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "AUTH_001", Message = error }]));
            if (error.StartsWith("MSG_002"))
                return Conflict(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "MSG_002", Message = error }]));
            if (error.StartsWith("MSG_003"))
                return UnprocessableEntity(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "MSG_003", Message = error }]));
            return BadRequest(ApiResponse<object>.Fail(error));
        }

        var participante = new ParticipanteItemDto
        {
            UserId = request.UserId,
            Nombre = request.UserId,
            Rol = request.Rol,
            JoinedAt = DateTime.UtcNow
        };

        return Created($"/api/v1/conversaciones/{id}/participantes", ApiResponse<ParticipanteItemDto>.Ok(participante));
    }

    /// <summary>
    /// Quita un participante de una conversación grupal (solo admin)
    /// </summary>
    [HttpPost("{id:long}/participantes/{userId}/remove")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 403)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<ActionResult<ApiResponse<object>>> QuitarParticipante(long id, string userId, CancellationToken ct = default)
    {
        var tenant = HttpContext.Items["TenantContext"] as TenantContext;
        if (tenant == null)
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var error = await _service.QuitarParticipanteAsync(id, userId, tenant.UserId.ToString(), ct);
        if (error != null)
        {
            if (error.StartsWith("MSG_001"))
                return NotFound(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "MSG_001", Message = error }]));
            if (error.StartsWith("AUTH_001"))
                return StatusCode(403, ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "AUTH_001", Message = error }]));
            if (error.StartsWith("MSG_003"))
                return UnprocessableEntity(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "MSG_003", Message = error }]));
            if (error.StartsWith("MSG_005"))
                return NotFound(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "MSG_005", Message = error }]));
            return BadRequest(ApiResponse<object>.Fail(error));
        }

        return Ok(ApiResponse<object>.Ok(new { result = true }));
    }
}

public class CrearConversacionRequest
{
    public string Tipo { get; set; } = string.Empty;
    public string? Asunto { get; set; }
    public long? LicitacionId { get; set; }
    public List<string> ParticipanteIds { get; set; } = new();
}

public class ActualizarConversacionRequest
{
    public string Asunto { get; set; } = string.Empty;
}

public class AgregarParticipanteRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Rol { get; set; } = "miembro";
}
