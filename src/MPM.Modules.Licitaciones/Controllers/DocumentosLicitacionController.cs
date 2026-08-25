using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
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
    AdjuntoDescargaService adjuntoDescargaService,
    AdjuntoManualUploadService manualUploadService,
    IConfiguration config) : ControllerBase
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

    /// <summary>Dispara la descarga bajo demanda de los documentos (202 con polling). ADR-015: deprecado, flag Extraccion:ModoDescarga.</summary>
    [HttpPost("descargar")]
    [ProducesResponseType(typeof(ApiResponse<DescargarDocumentosResultDto>), 202)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    [ProducesResponseType(typeof(ApiResponse<object>), 409)]
    [ProducesResponseType(typeof(ApiResponse<object>), 422)]
    public async Task<ActionResult<ApiResponse<DescargarDocumentosResultDto>>> Descargar(
        string codigoExterno, [FromBody] DescargarDocumentosRequest? request, CancellationToken ct = default)
    {
        var modo = config["Extraccion:ModoDescarga"] ?? "manual";
        if (modo.Equals("manual", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(501, ApiResponse<object>.Fail(
                "Descarga automática deshabilitada",
                [new ErrorDetail { Code = "DOC_007", Message = "Descarga automática deshabilitada por ADR-015 (reCAPTCHA Enterprise). Use carga manual: POST /upload-manual" }]));
        }

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
#pragma warning disable CS0618 // AdjuntoDescargaService is obsolete (ADR-015)
            var resultado = await adjuntoDescargaService.IniciarDescargaAsync(
                lic.Id, codigoExterno, tenant.UserId, request?.Forzar ?? false, ct);
#pragma warning restore CS0618

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

    /// <summary>Carga manual de pliegos (ADR-015, 038). Multipart/form-data con hasta 10 archivos, 20MB c/u.</summary>
    [HttpPost("upload-manual")]
    [RequestSizeLimit(210_000_000)] // ~210MB para 10x20MB + overhead
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<ActionResult<ApiResponse<object>>> UploadManual(
        string codigoExterno, [FromForm] List<IFormFile> files, CancellationToken ct = default)
    {
        var tenant = GetTenant();
        if (tenant == null)
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var lic = await licitacionService.ObtenerPorCodigoAsync(codigoExterno, ct);
        if (lic == null)
            return NotFound(ApiResponse<object>.Fail(
                "Licitación no encontrada",
                [new ErrorDetail { Code = "LIC_001", Message = $"Licitación {codigoExterno} no encontrada" }]));

        if (files == null || files.Count == 0)
            return BadRequest(ApiResponse<object>.Fail(
                "Sin archivos",
                [new ErrorDetail { Code = "VAL_001", Message = "Debe enviar al menos 1 archivo en el campo 'files'" }]));

        try
        {
            var result = await manualUploadService.UploadAsync(lic.Id, codigoExterno, files, tenant.UserId, ct);

            // Partial success: si todo fue rechazado, 422
            if (result.Rechazados == result.TotalRecibidos && result.Descargados == 0 && result.Reutilizados == 0)
            {
                return StatusCode(422, ApiResponse<object>.Fail(
                    "Todos los archivos fueron rechazados",
                    result.Errores.Select(e => new ErrorDetail { Code = e.Contains("DOC_009") ? "DOC_009" : e.Contains("DOC_008") ? "DOC_008" : "VAL_001", Message = e }).ToList()));
            }

            return Ok(ApiResponse<object>.Ok(new
            {
                totalRecibidos = result.TotalRecibidos,
                descargados = result.Descargados,
                reutilizados = result.Reutilizados,
                rechazados = result.Rechazados,
                errores = result.Errores,
                conjuntoHash = result.ConjuntoHash,
                mensaje = result.Rechazados > 0
                    ? $"Carga parcial: {result.Descargados + result.Reutilizados} ok, {result.Rechazados} rechazados"
                    : $"Carga completada: {result.Descargados + result.Reutilizados} archivos"
            }));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail("Validación", [new ErrorDetail { Code = "VAL_001", Message = ex.Message }]));
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
