using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Censo.Models;
using MPM.Modules.Censo.Services;
using MPM.Shared.Models;

namespace MPM.Modules.Censo.Controllers;

/// <summary>
/// Catálogo de types/tecnologías de Census (autocompletar en la UI): listado local con
/// refresco lazy si está vacío, y refresh manual desde census/knowledge.
/// </summary>
[ApiController]
[Route("api/v1/censo")]
[Authorize]
public class CensoCatalogoController(CensoCatalogoService catalogoService) : ControllerBase
{
    /// <summary>Listado del catálogo (filtros q/grupo/categoria aplicados en servicio).</summary>
    [HttpGet("catalogo")]
    [ProducesResponseType(typeof(ApiResponse<CensoCatalogoListadoDto>), 200)]
    public async Task<ActionResult<ApiResponse<CensoCatalogoListadoDto>>> Listar(
        [FromQuery] string? q, [FromQuery] string? grupo, [FromQuery] string? categoria,
        CancellationToken ct = default)
    {
        if (q != null && q.Trim().Length == 1)
            return BadRequest(ApiResponse<object>.Fail(
                "Filtro inválido",
                [new ErrorDetail { Code = "VAL_001", Field = "q", Message = "El filtro q requiere al menos 2 caracteres" }]));

        var listado = await catalogoService.ListarAsync(q, grupo, categoria, ct);
        return Ok(ApiResponse<CensoCatalogoListadoDto>.Ok(listado));
    }

    /// <summary>Refresca el catálogo desde census/knowledge (limpia y reinserta).
    /// QA (2026-08-16): requiere rol Admin/SuperAdmin — cualquier JWT podía truncar y
    /// reinsertar el catálogo (hallazgo O-2 del control-agent).</summary>
    [HttpPost("catalogo/refrescar")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<CensoRefrescoResultDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 502)]
    public async Task<ActionResult<ApiResponse<CensoRefrescoResultDto>>> Refrescar(CancellationToken ct = default)
    {
        try
        {
            var resultado = await catalogoService.RefrescarAsync(ct);
            return Ok(ApiResponse<CensoRefrescoResultDto>.Ok(resultado));
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, ApiResponse<object>.Fail(
                "Census inalcanzable",
                [new ErrorDetail { Code = "CEN_002", Message = ex.Message }]));
        }
    }

    /// <summary>Descarga o visualiza el archivo PDF de la certificación (corporativa o desde Census).</summary>
    [HttpGet("certificaciones/archivo/{*fileId}")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    [ProducesResponseType(typeof(ApiResponse<object>), 502)]
    public async Task<IActionResult> DescargarArchivoCertificacion(
        string fileId, [FromServices] CensusClient censusClient, CancellationToken ct = default)
    {
        try
        {
            var cleanFileId = Uri.UnescapeDataString(fileId ?? "").Trim().Replace('\\', '/');

            // 1. Archivo local / corporativo
            var localPath = Path.Combine("/app/uploads", cleanFileId);
            if (System.IO.File.Exists(localPath))
            {
                var localBytes = await System.IO.File.ReadAllBytesAsync(localPath, ct);
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{Path.GetFileName(localPath)}\"";
                return File(localBytes, "application/pdf");
            }

            var localFallback = Path.Combine(Directory.GetCurrentDirectory(), "uploads", cleanFileId);
            if (System.IO.File.Exists(localFallback))
            {
                var localBytes = await System.IO.File.ReadAllBytesAsync(localFallback, ct);
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{Path.GetFileName(localFallback)}\"";
                return File(localBytes, "application/pdf");
            }

            // 2. Consulta a Census
            var bytes = await censusClient.DownloadCertificationFileAsync(cleanFileId, ct);
            if (bytes == null || bytes.Length == 0)
                return NotFound(ApiResponse<object>.Fail("Archivo de certificación no encontrado"));

            Response.Headers["Content-Disposition"] = $"inline; filename=\"Certificacion_{cleanFileId}.pdf\"";
            return File(bytes, "application/pdf");
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, ApiResponse<object>.Fail(
                "Census inalcanzable",
                [new ErrorDetail { Code = "CEN_002", Message = ex.Message }]));
        }
    }
}
