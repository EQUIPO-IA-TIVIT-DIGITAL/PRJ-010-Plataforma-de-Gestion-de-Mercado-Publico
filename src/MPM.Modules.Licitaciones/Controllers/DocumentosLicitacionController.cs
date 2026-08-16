using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Licitaciones.Models;
using MPM.Modules.Licitaciones.Services;
using MPM.Shared.Models;

namespace MPM.Modules.Licitaciones.Controllers;

/// <summary>
/// Documentos de licitación — descarga bajo demanda + cache por hash (036-flujo-comercial-ofertas,
/// spec docs/api-first/licitaciones-documentos.md).
/// </summary>
[ApiController]
[Route("api/v1/licitaciones/{codigoExterno}/documentos")]
[Authorize]
public class DocumentosLicitacionController(
    LicitacionService licitacionService,
    AdjuntoDescargaService adjuntoDescargaService) : ControllerBase
{
    private TenantContext? GetTenant() => HttpContext.Items["TenantContext"] as TenantContext;

    /// <summary>Estado de los documentos guardados de la licitación (para cache y polling).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<EstadoDocumentosDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<ActionResult<ApiResponse<EstadoDocumentosDto>>> Estado(
        string codigoExterno, CancellationToken ct = default)
    {
        var lic = await licitacionService.ObtenerPorCodigoAsync(codigoExterno, ct);
        if (lic == null)
            return NotFound(ApiResponse<object>.Fail(
                "Licitación no encontrada",
                [new ErrorDetail { Code = "LIC_001", Message = $"Licitación {codigoExterno} no encontrada" }]));

        var estado = await adjuntoDescargaService.ObtenerEstadoAsync(lic.Id, ct);
        return Ok(ApiResponse<EstadoDocumentosDto>.Ok(estado));
    }

    /// <summary>Dispara la descarga bajo demanda de los documentos (202 con polling).</summary>
    [HttpPost("descargar")]
    [ProducesResponseType(typeof(ApiResponse<DescargarDocumentosResultDto>), 202)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    [ProducesResponseType(typeof(ApiResponse<object>), 409)]
    [ProducesResponseType(typeof(ApiResponse<object>), 422)]
    public async Task<ActionResult<ApiResponse<DescargarDocumentosResultDto>>> Descargar(
        string codigoExterno, [FromBody] DescargarDocumentosRequest? request, CancellationToken ct = default)
    {
        var tenant = GetTenant();
        if (tenant == null)
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var lic = await licitacionService.ObtenerPorCodigoAsync(codigoExterno, ct);
        if (lic == null)
            return NotFound(ApiResponse<object>.Fail(
                "Licitación no encontrada",
                [new ErrorDetail { Code = "LIC_001", Message = $"Licitación {codigoExterno} no encontrada" }]));

        try
        {
            var resultado = await adjuntoDescargaService.IniciarDescargaAsync(
                lic.Id, codigoExterno, tenant.UserId, request?.Forzar ?? false, ct);

            if (resultado.EstadoConjunto == "error")
                return StatusCode(422, ApiResponse<object>.Fail(
                    "La descarga de documentos falló",
                    [new ErrorDetail { Code = "DOC_005", Message = resultado.DescargaError ?? "Error desconocido" }]));

            return StatusCode(202, ApiResponse<DescargarDocumentosResultDto>.Ok(resultado));
        }
        catch (AdjuntoDescargaService.DescargaEnCursoException)
        {
            return Conflict(ApiResponse<object>.Fail(
                "Extracción ya en curso",
                [new ErrorDetail { Code = "DOC_006", Message = "Ya hay una extracción de documentos en curso para esta licitación" }]));
        }
    }

    /// <summary>Descarga binaria de un documento guardado (patrón MensajeController).</summary>
    [HttpGet("{documentoId}/archivo")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> Archivo(
        string codigoExterno, long documentoId, CancellationToken ct = default)
    {
        var lic = await licitacionService.ObtenerPorCodigoAsync(codigoExterno, ct);
        if (lic == null)
            return NotFound(ApiResponse<object>.Fail(
                "Licitación no encontrada",
                [new ErrorDetail { Code = "LIC_001", Message = $"Licitación {codigoExterno} no encontrada" }]));

        var archivo = await adjuntoDescargaService.ObtenerArchivoAsync(lic.Id, documentoId, ct);
        if (archivo == null)
            return NotFound(ApiResponse<object>.Fail(
                "Documento no encontrado",
                [new ErrorDetail { Code = "DOC_004", Message = $"Documento {documentoId} no encontrado" }]));

        return File(archivo.Stream, archivo.MimeType, archivo.NombreArchivo);
    }
}
