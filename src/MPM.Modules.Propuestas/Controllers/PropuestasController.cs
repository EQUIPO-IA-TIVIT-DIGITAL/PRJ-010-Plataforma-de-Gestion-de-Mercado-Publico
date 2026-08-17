using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Propuestas.Models;
using MPM.Modules.Propuestas.Services;
using MPM.Shared.Models;

namespace MPM.Modules.Propuestas.Controllers;

[ApiController]
[Route("api/v1/licitaciones/{codigoExterno}/propuestas")]
[Authorize]
[ServiceFilter(typeof(MPM.Modules.Propuestas.Filters.PropuestasExceptionFilter))]
public sealed class PropuestasController(IPropuestaService service) : ControllerBase
{
    [HttpPost("generar")]
    public async Task<ActionResult<ApiResponse<GenerarPropuestaResponse>>> Generar(
        string codigoExterno, [FromBody] GenerarPropuestaRequest? request, CancellationToken ct = default)
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));
        var generadoPor = string.IsNullOrWhiteSpace(tenant.Username) ? tenant.UserId : tenant.Username;
        var result = await service.GenerarAsync(codigoExterno, request ?? new GenerarPropuestaRequest(), generadoPor, ct);
        return Ok(ApiResponse<GenerarPropuestaResponse>.Ok(result));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<CatalogoPage<PropuestaHistorialDto>>>> Historial(
        string codigoExterno, [FromQuery] int page = 1, [FromQuery] int size = 20,
        [FromQuery] string? estado = null, CancellationToken ct = default)
    {
        if (GetTenant() == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));
        return Ok(ApiResponse<CatalogoPage<PropuestaHistorialDto>>.Ok(
            await service.ListarAsync(codigoExterno, estado, page, size, ct)));
    }

    [HttpGet("{propuestaId:long}/archivo")]
    public async Task<IActionResult> Archivo(string codigoExterno, long propuestaId, CancellationToken ct = default)
    {
        if (GetTenant() == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));
        var (_, proposal) = await service.ObtenerArchivoAsync(codigoExterno, propuestaId, ct);
        var stream = await service.DownloadStoredAsync(proposal.RutaArchivo!, ct);
        if (stream == null)
            return NotFound(ApiResponse<object>.Fail("Propuesta no encontrada o sin archivo", [
                new ErrorDetail { Code = "PRO_001", Message = "El archivo no está disponible" }
            ]));

        var safeCode = string.Concat(codigoExterno.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
        return File(stream,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            $"Propuesta_{safeCode}_v{proposal.Version}.docx");
    }

    [HttpPatch("{propuestaId:long}/estado")]
    public async Task<ActionResult<ApiResponse<PropuestaHistorialDto>>> ActualizarEstado(
        string codigoExterno, long propuestaId, [FromBody] PropuestaEstadoRequest request,
        CancellationToken ct = default)
    {
        if (GetTenant() == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));
        return Ok(ApiResponse<PropuestaHistorialDto>.Ok(
            await service.ActualizarEstadoAsync(codigoExterno, propuestaId, request.Estado, ct)));
    }

    private TenantContext? GetTenant() => HttpContext.Items["TenantContext"] as TenantContext;
}
