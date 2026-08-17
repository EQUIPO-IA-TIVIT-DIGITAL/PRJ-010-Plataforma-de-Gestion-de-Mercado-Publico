using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Propuestas.Data;
using MPM.Modules.Propuestas.Models;
using MPM.Modules.Propuestas.Services;
using MPM.Shared.Models;

namespace MPM.Modules.Propuestas.Controllers;

[ApiController]
[Route("api/v1/propuestas/catalogos")]
[Authorize]
[ServiceFilter(typeof(MPM.Modules.Propuestas.Filters.PropuestasExceptionFilter))]
public class PropuestasCatalogosController(
    PropuestasCatalogoService catalogoService,
    CensusCertificationSyncService censusSyncService) : ControllerBase
{
    [HttpGet("experiencias")]
    public async Task<ActionResult<ApiResponse<CatalogoPage<ExperienciaCatalogoDto>>>> ListarExperiencias(
        [FromQuery] string? q, [FromQuery] bool activo = true, [FromQuery] int page = 1,
        [FromQuery] int size = 20, CancellationToken ct = default)
        => Ok(ApiResponse<CatalogoPage<ExperienciaCatalogoDto>>.Ok(await catalogoService.ListarExperienciasAsync(q, activo, page, size, ct)));

    [HttpPost("experiencias")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<ExperienciaCatalogoDto>>> CrearExperiencia([FromBody] ExperienciaCatalogoRequest request, CancellationToken ct = default)
        => Created("", ApiResponse<ExperienciaCatalogoDto>.Ok(await catalogoService.CrearExperienciaAsync(request, ct)));

    [HttpPut("experiencias/{id:long}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<ExperienciaCatalogoDto>>> ActualizarExperiencia(long id, [FromBody] ExperienciaCatalogoRequest request, CancellationToken ct = default)
        => Ok(ApiResponse<ExperienciaCatalogoDto>.Ok(await catalogoService.ActualizarExperienciaAsync(id, request, ct)));

    [HttpDelete("experiencias/{id:long}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<object>>> EliminarExperiencia(long id, CancellationToken ct = default)
    {
        await catalogoService.EliminarExperienciaAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { experienciaId = id }));
    }

    [HttpGet("certificaciones")]
    public async Task<ActionResult<ApiResponse<CatalogoPage<CertificacionCatalogoDto>>>> ListarCertificaciones(
        [FromQuery] string? q, [FromQuery] bool activo = true, [FromQuery] bool? conArchivo = null,
        [FromQuery] string? tipo = null, [FromQuery] int page = 1, [FromQuery] int size = 20, CancellationToken ct = default)
        => Ok(ApiResponse<CatalogoPage<CertificacionCatalogoDto>>.Ok(await catalogoService.ListarCertificacionesAsync(q, activo, conArchivo, tipo, page, size, ct)));

    [HttpPost("certificaciones")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<CertificacionCatalogoDto>>> CrearCertificacion([FromBody] CertificacionCatalogoRequest request, CancellationToken ct = default)
        => Created("", ApiResponse<CertificacionCatalogoDto>.Ok(await catalogoService.CrearCertificacionAsync(request, ct)));

    [HttpPut("certificaciones/{id:long}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<CertificacionCatalogoDto>>> ActualizarCertificacion(long id, [FromBody] CertificacionCatalogoRequest request, CancellationToken ct = default)
        => Ok(ApiResponse<CertificacionCatalogoDto>.Ok(await catalogoService.ActualizarCertificacionAsync(id, request, ct)));

    [HttpDelete("certificaciones/{id:long}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<object>>> EliminarCertificacion(long id, CancellationToken ct = default)
    {
        await catalogoService.EliminarCertificacionAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { certificacionId = id }));
    }

    [HttpPost("certificaciones/{id:long}/archivo")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<CertificacionCatalogoDto>>> SubirArchivoCertificacion(
        long id, IFormFile file, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No se proporcionó ningún archivo"));

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && file.ContentType != "application/pdf")
            return BadRequest(ApiResponse<object>.Fail("Solo se permiten archivos en formato PDF"));

        var cert = await catalogoService.ObtenerCertificacionAsync(id, ct);
        if (cert == null)
            return NotFound(ApiResponse<object>.Fail("Certificación no encontrada"));

        var uploadDir = "/app/uploads/certificaciones-empresa";
        if (!Directory.Exists(uploadDir))
        {
            uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "certificaciones-empresa");
            Directory.CreateDirectory(uploadDir);
        }
        else
        {
            Directory.CreateDirectory(uploadDir);
        }

        var cleanFileName = Path.GetFileName(file.FileName).Replace(" ", "_");
        var savedFileName = $"{id}_{cleanFileName}";
        var fullPath = Path.Combine(uploadDir, savedFileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream, ct);
        }

        var relativePath = $"certificaciones-empresa/{savedFileName}";
        var updateRequest = new CertificacionCatalogoRequest
        {
            Nombre = cert.Nombre,
            Institucion = cert.Institucion,
            Vigencia = cert.Vigencia,
            Titular = cert.Titular,
            Tipo = cert.Tipo,
            FileIdCensus = relativePath,
            Activo = cert.Activo,
        };

        var updated = await catalogoService.ActualizarCertificacionAsync(id, updateRequest, ct);
        return Ok(ApiResponse<CertificacionCatalogoDto>.Ok(updated));
    }

    [HttpPost("certificaciones/sincronizar-census")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<CensusSyncResultDto>>> SincronizarCertificaciones(CancellationToken ct = default)
    {
        try
        {
            return Ok(ApiResponse<CensusSyncResultDto>.Ok(await censusSyncService.SincronizarAsync(ct)));
        }
        catch (CensusCertificationSyncService.CensusPayloadTooLargeException ex)
        {
            return BadRequest(ApiResponse<object>.Fail("Payload de Census demasiado grande", [new ErrorDetail { Code = "VAL_008", Message = ex.Message }]));
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, ApiResponse<object>.Fail("Census inalcanzable", [new ErrorDetail { Code = "CEN_002", Message = ex.Message }]));
        }
    }

    [HttpGet("capitulos")]
    public async Task<ActionResult<ApiResponse<CatalogoPage<CapituloCatalogoDto>>>> ListarCapitulos(
        [FromQuery] string? q, [FromQuery] bool activo = true, [FromQuery] int page = 1,
        [FromQuery] int size = 20, CancellationToken ct = default)
        => Ok(ApiResponse<CatalogoPage<CapituloCatalogoDto>>.Ok(await catalogoService.ListarCapitulosAsync(q, activo, page, size, ct)));

    [HttpPost("capitulos")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<CapituloCatalogoDto>>> CrearCapitulo([FromBody] CapituloCatalogoRequest request, CancellationToken ct = default)
        => Created("", ApiResponse<CapituloCatalogoDto>.Ok(await catalogoService.CrearCapituloAsync(request, ct)));

    [HttpPut("capitulos/{id:long}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<CapituloCatalogoDto>>> ActualizarCapitulo(long id, [FromBody] CapituloCatalogoRequest request, CancellationToken ct = default)
        => Ok(ApiResponse<CapituloCatalogoDto>.Ok(await catalogoService.ActualizarCapituloAsync(id, request, ct)));

    [HttpDelete("capitulos/{id:long}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ApiResponse<object>>> EliminarCapitulo(long id, CancellationToken ct = default)
    {
        await catalogoService.EliminarCapituloAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { capituloId = id }));
    }

}
