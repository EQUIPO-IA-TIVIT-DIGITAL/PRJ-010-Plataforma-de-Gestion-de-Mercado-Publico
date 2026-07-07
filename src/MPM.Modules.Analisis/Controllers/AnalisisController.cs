using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Analisis.Models;
using MPM.Modules.Analisis.Services;
using MPM.Shared.Models;

namespace MPM.Modules.Analisis.Controllers;

[ApiController]
[Route("api/v1/analisis")]
[Authorize]
public class AnalisisController(AnalisisService analisisService) : ControllerBase
{
    private readonly AnalisisService _analisisService = analisisService;

    private TenantContext? GetTenant()
    {
        return HttpContext.Items["TenantContext"] as TenantContext;
    }

    [HttpPost("workspaces")]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceDetalleDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<ActionResult<ApiResponse<WorkspaceDetalleDto>>> CrearWorkspace(
        [FromBody] CrearWorkspaceRequest request,
        CancellationToken ct = default)
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(ApiResponse<object>.Fail("nombre es requerido", [new ErrorDetail { Code = "VAL_001", Field = "nombre", Message = "nombre es requerido" }]));

        var (workspace, error) = await _analisisService.CrearWorkspaceAsync(request.LicitacionId, request.Nombre, tenant.UserId.ToString(), ct);

        if (error != null)
        {
            if (error.StartsWith("VAL_"))
                return BadRequest(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = error.Split(':')[0], Message = error }]));
            return NotFound(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = error.Split(':')[0], Message = error }]));
        }

        return Created($"/api/v1/analisis/workspaces/{workspace!.Id}", ApiResponse<WorkspaceDetalleDto>.Ok(workspace));
    }

    [HttpGet("workspaces")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<WorkspaceItemDto>>), 200)]
    public async Task<ActionResult<ApiResponse<PaginatedResult<WorkspaceItemDto>>>> ListarWorkspaces(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? estado = null,
        CancellationToken ct = default)
    {
        var (result, error) = await _analisisService.ListarWorkspacesAsync(page, pageSize, search, estado, ct);
        if (error != null)
            return BadRequest(ApiResponse<object>.Fail(error));

        return Ok(ApiResponse<PaginatedResult<WorkspaceItemDto>>.Ok(result!));
    }

    [HttpGet("workspaces/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceDetalleDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<ActionResult<ApiResponse<WorkspaceDetalleDto>>> ObtenerWorkspace(
        long id,
        CancellationToken ct = default)
    {
        var (workspace, error) = await _analisisService.ObtenerWorkspaceAsync(id, ct);
        if (error != null)
            return NotFound(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "ANA_001", Message = error }]));

        return Ok(ApiResponse<WorkspaceDetalleDto>.Ok(workspace!));
    }

    [HttpDelete("workspaces/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 422)]
    public async Task<ActionResult<ApiResponse<object>>> EliminarWorkspace(long id, CancellationToken ct = default)
    {
        var (success, error) = await _analisisService.EliminarWorkspaceAsync(id, ct);
        if (!success)
        {
            if (error?.StartsWith("ANA_005") == true)
                return UnprocessableEntity(ApiResponse<object>.Fail(error!, [new ErrorDetail { Code = "ANA_005", Message = error! }]));
            return NotFound(ApiResponse<object>.Fail(error!, [new ErrorDetail { Code = "ANA_001", Message = error! }]));
        }

        return Ok(ApiResponse<object>.Ok(new { result = true }));
    }

    [HttpPost("workspaces/{id:long}/documentos")]
    [ProducesResponseType(typeof(ApiResponse<DocumentoDetalleDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<ActionResult<ApiResponse<DocumentoDetalleDto>>> SubirDocumento(
        long id,
        IFormFile archivo,
        CancellationToken ct = default)
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized();

        if (archivo == null || archivo.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("archivo es requerido", [new ErrorDetail { Code = "VAL_003", Field = "archivo", Message = "archivo es requerido" }]));

        if (archivo.ContentType != "application/pdf")
            return BadRequest(ApiResponse<object>.Fail("Solo se permiten archivos PDF", [new ErrorDetail { Code = "VAL_004", Field = "archivo", Message = "Solo se permiten archivos PDF" }]));

        var maxSize = 10 * 1024 * 1024;
        if (archivo.Length > maxSize)
            return BadRequest(ApiResponse<object>.Fail("archivo excede el tamaño máximo", [new ErrorDetail { Code = "VAL_005", Field = "archivo", Message = "archivo excede el tamaño máximo de 10MB" }]));

        using var stream = archivo.OpenReadStream();
        var (documento, error) = await _analisisService.SubirDocumentoAsync(id, archivo.FileName, archivo.ContentType, archivo.Length, stream, ct);

        if (error != null)
        {
            if (error.StartsWith("ANA_001"))
                return NotFound(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "ANA_001", Message = error }]));
            return BadRequest(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = error.Split(':')[0], Message = error }]));
        }

        return Created($"/api/v1/analisis/workspaces/{id}/documentos/{documento!.Id}", ApiResponse<DocumentoDetalleDto>.Ok(documento));
    }

    [HttpGet("workspaces/{id:long}/documentos")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<DocumentoItemDto>>), 200)]
    public async Task<ActionResult<ApiResponse<IEnumerable<DocumentoItemDto>>>> ListarDocumentos(long id, CancellationToken ct = default)
    {
        var (documentos, error) = await _analisisService.ListarDocumentosAsync(id, ct);
        if (error != null)
            return NotFound(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "ANA_001", Message = error }]));

        return Ok(ApiResponse<IEnumerable<DocumentoItemDto>>.Ok(documentos));
    }

    [HttpPost("workspaces/{id:long}/analizar")]
    [ProducesResponseType(typeof(ApiResponse<AnalisisResumenDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 422)]
    public async Task<ActionResult<ApiResponse<AnalisisResumenDto>>> Analizar(
        long id,
        [FromBody] AnalizarRequest? request = null,
        CancellationToken ct = default)
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized();

        var (resultado, error) = await _analisisService.AnalizarAsync(id, request?.DocumentoId, ct);

        if (error != null)
        {
            if (error.StartsWith("ANA_002") || error.StartsWith("ANA_003"))
                return UnprocessableEntity(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = error.Split(':')[0], Message = error }]));
            if (error.StartsWith("GEM_"))
                return StatusCode(502, ApiResponse<object>.Fail(error, [new ErrorDetail { Code = error.Split(':')[0], Message = error }]));
            return NotFound(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = error.Split(':')[0], Message = error }]));
        }

        return Ok(ApiResponse<AnalisisResumenDto>.Ok(resultado!));
    }

    [HttpGet("workspaces/{id:long}/dashboard")]
    [ProducesResponseType(typeof(ApiResponse<ResultadoDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<ActionResult<ApiResponse<ResultadoDto>>> ObtenerDashboard(long id, CancellationToken ct = default)
    {
        var (resultado, error) = await _analisisService.ObtenerDashboardAsync(id, ct);
        if (error != null)
            return NotFound(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "ANA_004", Message = error }]));

        return Ok(ApiResponse<ResultadoDto>.Ok(resultado!));
    }

    [HttpPost("workspaces/{id:long}/chat")]
    [ProducesResponseType(typeof(ApiResponse<ChatResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<ActionResult<ApiResponse<ChatResponseDto>>> Chat(
        long id,
        [FromBody] ChatRequest request,
        CancellationToken ct = default)
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Mensaje))
            return BadRequest(ApiResponse<object>.Fail("mensaje es requerido", [new ErrorDetail { Code = "VAL_001", Field = "mensaje", Message = "mensaje es requerido" }]));

        var (response, error) = await _analisisService.ChatAsync(id, request.Mensaje, ct);

        if (error != null)
        {
            if (error.StartsWith("ANA_004"))
                return NotFound(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = "ANA_004", Message = error }]));
            return BadRequest(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = error.Split(':')[0], Message = error }]));
        }

        return Ok(ApiResponse<ChatResponseDto>.Ok(response!));
    }

    [HttpGet("ejecutivo")]
    [ProducesResponseType(typeof(ApiResponse<DashboardEjecutivoDto>), 200)]
    public async Task<ActionResult<ApiResponse<DashboardEjecutivoDto>>> GetDashboardEjecutivo(
        [FromQuery] int? anio = null,
        CancellationToken ct = default)
    {
        var (dashboard, error) = await _analisisService.GetDashboardEjecutivoAsync(anio, ct);
        if (error != null)
            return BadRequest(ApiResponse<object>.Fail(error));

        return Ok(ApiResponse<DashboardEjecutivoDto>.Ok(dashboard!));
    }

    [HttpGet("workspaces/{id:long}/chat")]
    [ProducesResponseType(typeof(ApiResponse<ChatHistorialDto>), 200)]
    public async Task<ActionResult<ApiResponse<ChatHistorialDto>>> ObtenerChatHistorial(long id, CancellationToken ct = default)
    {
        var (historial, error) = await _analisisService.ObtenerChatHistorialAsync(id, ct);
        if (error != null)
            return NotFound(ApiResponse<object>.Fail(error, [new ErrorDetail { Code = error.Split(':')[0], Message = error }]));

        return Ok(ApiResponse<ChatHistorialDto>.Ok(historial!));
    }
}
