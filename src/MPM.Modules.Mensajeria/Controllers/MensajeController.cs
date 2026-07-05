using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Mensajeria.Models;
using MPM.Modules.Mensajeria.Services;
using MPM.Shared.Models;
using MPM.Shared.Services;

namespace MPM.Modules.Mensajeria.Controllers;

[ApiController]
[Route("api/v1/conversaciones/{conversacionId:long}/mensajes")]
[Authorize]
public class MensajeController(
    MensajeService mensajeService,
    AdjuntoService adjuntoService,
    IStorageService storageService) : ControllerBase
{
    private readonly MensajeService _mensajeService = mensajeService;
    private readonly AdjuntoService _adjuntoService = adjuntoService;
    private readonly IStorageService _storageService = storageService;

    /// <summary>
    /// Lista los mensajes de una conversación (paginado, más recientes primero)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<MensajeDetalleDto>>), 200)]
    public async Task<ActionResult<ApiResponse<PaginatedResult<MensajeDetalleDto>>>> Listar(
        long conversacionId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] long? before = null,
        CancellationToken ct = default)
    {
        var tenant = HttpContext.Items["TenantContext"] as TenantContext;
        if (tenant == null)
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var result = await _mensajeService.ListarAsync(conversacionId, tenant.UserId.ToString(), page, pageSize, before, ct);
        return Ok(ApiResponse<PaginatedResult<MensajeDetalleDto>>.Ok(result));
    }

    /// <summary>
    /// Envía un nuevo mensaje a la conversación
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<MensajeDetalleDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 403)]
    public async Task<ActionResult<ApiResponse<MensajeDetalleDto>>> Enviar(
        long conversacionId,
        [FromBody] EnviarMensajeRequest request,
        CancellationToken ct = default)
    {
        var tenant = HttpContext.Items["TenantContext"] as TenantContext;
        if (tenant == null)
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        if (request.Tipo == "texto" && string.IsNullOrWhiteSpace(request.Contenido))
            return BadRequest(ApiResponse<object>.Fail("contenido es requerido para mensajes de texto", [new ErrorDetail { Code = "VAL_001", Field = "contenido", Message = "contenido es requerido" }]));

        if (request.Contenido != null && request.Contenido.Length > 5000)
            return BadRequest(ApiResponse<object>.Fail("contenido excede el largo máximo", [new ErrorDetail { Code = "VAL_008", Field = "contenido", Message = "contenido excede el largo máximo de 5000 caracteres" }]));

        var (id, error) = await _mensajeService.EnviarAsync(conversacionId, tenant.UserId.ToString(), request.Tipo, request.Contenido, request.ReplyToId, ct);

        if (error != null)
        {
            if (error.StartsWith("MSG_001"))
                return NotFound(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "MSG_001", Message = error }]));
            if (error.StartsWith("AUTH_001"))
                return StatusCode(403, ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "AUTH_001", Message = error }]));
            if (error.StartsWith("VAL_"))
                return BadRequest(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = error.Split(':')[0], Message = error }]));
            return BadRequest(ApiResponse<object>.Fail(error));
        }

        var mensajes = await _mensajeService.ListarAsync(conversacionId, tenant.UserId.ToString(), 1, 1, null, ct);
        var mensaje = mensajes.Items.FirstOrDefault(m => m.Id == id);

        return Created($"/api/v1/conversaciones/{conversacionId}/mensajes/{id}", ApiResponse<MensajeDetalleDto>.Ok(mensaje!));
    }

    /// <summary>
    /// Edita un mensaje (solo el autor, dentro de ventana de 15 minutos)
    /// </summary>
    [HttpPut("{msgId:long}")]
    [ProducesResponseType(typeof(ApiResponse<MensajeDetalleDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 403)]
    [ProducesResponseType(typeof(ApiResponse<object>), 422)]
    public async Task<ActionResult<ApiResponse<MensajeDetalleDto>>> Editar(
        long conversacionId,
        long msgId,
        [FromBody] EditarMensajeRequest request,
        CancellationToken ct = default)
    {
        var tenant = HttpContext.Items["TenantContext"] as TenantContext;
        if (tenant == null)
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var error = await _mensajeService.EditarAsync(msgId, tenant.UserId.ToString(), request.Contenido, ct);
        if (error != null)
        {
            if (error.StartsWith("MSG_006"))
                return NotFound(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "MSG_006", Message = error }]));
            if (error.StartsWith("AUTH_001"))
                return StatusCode(403, ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "AUTH_001", Message = error }]));
            if (error.StartsWith("MSG_003"))
                return UnprocessableEntity(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "MSG_003", Message = error }]));
            if (error.StartsWith("VAL_"))
                return BadRequest(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = error.Split(':')[0], Message = error }]));
            return BadRequest(ApiResponse<object>.Fail(error));
        }

        var mensajes = await _mensajeService.ListarAsync(conversacionId, tenant.UserId.ToString(), 1, 1, null, ct);
        var mensaje = mensajes.Items.FirstOrDefault(m => m.Id == msgId);

        return Ok(ApiResponse<MensajeDetalleDto>.Ok(mensaje));
    }

    /// <summary>
    /// Elimina un mensaje (soft delete, solo autor o admin)
    /// </summary>
    [HttpDelete("{msgId:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 403)]
    public async Task<ActionResult<ApiResponse<object>>> Eliminar(long conversacionId, long msgId, CancellationToken ct = default)
    {
        var tenant = HttpContext.Items["TenantContext"] as TenantContext;
        if (tenant == null)
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var error = await _mensajeService.EliminarAsync(msgId, tenant.UserId.ToString(), ct);
        if (error != null)
        {
            if (error.StartsWith("MSG_006"))
                return NotFound(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "MSG_006", Message = error }]));
            if (error.StartsWith("AUTH_001"))
                return StatusCode(403, ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "AUTH_001", Message = error }]));
            return BadRequest(ApiResponse<object>.Fail(error));
        }

        return Ok(ApiResponse<object>.Ok(new { result = true }));
    }

    /// <summary>
    /// Marca un mensaje como leído por el usuario autenticado
    /// </summary>
    [HttpPost("{msgId:long}/leido")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<ActionResult<ApiResponse<object>>> MarcarLeido(long conversacionId, long msgId, CancellationToken ct = default)
    {
        var tenant = HttpContext.Items["TenantContext"] as TenantContext;
        if (tenant == null)
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        await _mensajeService.MarcarLeidoAsync(msgId, tenant.UserId.ToString(), ct);
        return Ok(ApiResponse<object>.Ok(new { result = true }));
    }

    /// <summary>
    /// Sube un archivo adjunto a un mensaje (máximo 10MB)
    /// </summary>
    [HttpPost("{msgId:long}/adjuntos")]
    [ProducesResponseType(typeof(ApiResponse<AdjuntoDetalleDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<ActionResult<ApiResponse<AdjuntoDetalleDto>>> SubirAdjunto(
        long conversacionId,
        long msgId,
        IFormFile archivo,
        CancellationToken ct = default)
    {
        var tenant = HttpContext.Items["TenantContext"] as TenantContext;
        if (tenant == null)
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        if (archivo == null || archivo.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("archivo es requerido", [new ErrorDetail { Code = "VAL_001", Field = "archivo", Message = "archivo es requerido" }]));

        var maxSize = 10 * 1024 * 1024;
        if (archivo.Length > maxSize)
            return BadRequest(ApiResponse<object>.Fail("archivo excede el tamaño máximo", [new ErrorDetail { Code = "VAL_007", Field = "archivo", Message = "archivo excede el tamaño máximo de 10MB" }]));

        var storagePath = $"mensajeria/{conversacionId}/{msgId}";
        var fileName = $"{Guid.NewGuid()}_{archivo.FileName}";

        using var uploadStream = archivo.OpenReadStream();
        var filePath = await _storageService.UploadAsync(storagePath, fileName, uploadStream, archivo.ContentType, ct);

        var (id, error) = await _adjuntoService.CrearAsync(msgId, archivo.FileName, archivo.ContentType, archivo.Length, filePath, ct);

        if (error != null)
        {
            if (error.StartsWith("MSG_006"))
                return NotFound(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "MSG_006", Message = error }]));
            return BadRequest(ApiResponse<object>.Fail(error));
        }

        var adjunto = await _adjuntoService.ObtenerAsync(id, conversacionId, tenant.UserId.ToString(), ct);
        return Created($"/api/v1/conversaciones/{conversacionId}/mensajes/{msgId}/adjuntos/{id}", ApiResponse<AdjuntoDetalleDto>.Ok(adjunto!));
    }

    /// <summary>
    /// Descarga un archivo adjunto de un mensaje
    /// </summary>
    [HttpGet("{msgId:long}/adjuntos/{attId:long}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DescargarAdjunto(long conversacionId, long msgId, long attId, CancellationToken ct = default)
    {
        var tenant = HttpContext.Items["TenantContext"] as TenantContext;
        if (tenant == null)
            return Unauthorized();

        var adjunto = await _adjuntoService.ObtenerAsync(attId, conversacionId, tenant.UserId.ToString(), ct);
        if (adjunto == null)
            return NotFound();

        var stream = await _storageService.DownloadAsync(adjunto.RutaStorage, ct);
        if (stream == null)
            return NotFound();

        return File(stream, adjunto.MimeType, adjunto.NombreArchivo);
    }
}

public class EnviarMensajeRequest
{
    public string Tipo { get; set; } = "texto";
    public string? Contenido { get; set; }
    public long? ReplyToId { get; set; }
}

public class EditarMensajeRequest
{
    public string Contenido { get; set; } = string.Empty;
}
