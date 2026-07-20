using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Competidores.Models;
using MPM.Modules.Competidores.Services;
using MPM.Shared.Models;

namespace MPM.Modules.Competidores.Controllers;

[ApiController]
[Route("api/v1/competidores")]
[Authorize]
public class CompetidoresController(CompetidorAnalysisService service) : ControllerBase
{
    private TenantContext? GetTenant() => HttpContext.Items["TenantContext"] as TenantContext;

    /// <summary>Lista los nombres de competidores ya recolectados (excluye TIVIT) para poblar el buscador con un dropdown en vez de texto libre.</summary>
    [HttpGet("lista")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<string>>), 200)]
    public async Task<ActionResult<ApiResponse<IEnumerable<string>>>> ListarCompetidores()
    {
        var nombres = await service.ListarCompetidoresAsync();
        return Ok(ApiResponse<IEnumerable<string>>.Ok(nombres));
    }

    /// <summary>Busca las ofertas de un competidor por nombre -- 100% datos ya recolectados, nunca dispara IA (FR-001/FR-002).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<OfertaDto>>), 200)]
    public async Task<ActionResult<ApiResponse<IEnumerable<OfertaDto>>>> BuscarPorNombre([FromQuery] string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return BadRequest(ApiResponse<object>.Fail("El parámetro 'nombre' es requerido"));

        var ofertas = await service.BuscarOfertasAsync(nombre);
        return Ok(ApiResponse<IEnumerable<OfertaDto>>.Ok(ofertas));
    }

    /// <summary>Endpoint interno usado por el scraper (tools/scraper-mp/modulos/cuadroOfertas.js) para persistir ofertas extraídas.</summary>
    [HttpPost("ofertas")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<ActionResult<ApiResponse<object>>> GuardarOfertas([FromBody] GuardarOfertasRequest request)
    {
        if (request.Ofertas.Count == 0)
            return Ok(ApiResponse<object>.Ok(new { guardadas = 0 }));

        await service.GuardarOfertasAsync(request.LicitacionId, request.Ofertas);
        return Ok(ApiResponse<object>.Ok(new { guardadas = request.Ofertas.Count }));
    }

    /// <summary>
    /// FR-003/FR-004/FR-005/FR-006: primero devuelve caché si existe; si no, y `confirmar=false`,
    /// solo informa cuántas licitaciones entrarían (sin gastar IA); solo genera el análisis real
    /// con Gemini cuando el usuario manda `confirmar=true` explícitamente.
    /// </summary>
    [HttpPost("analisis")]
    [ProducesResponseType(typeof(ApiResponse<AnalisisCompetidorResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 422)]
    public async Task<ActionResult<ApiResponse<AnalisisCompetidorResponse>>> AnalizarCompetidor([FromBody] AnalizarCompetidorRequest request)
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        if (string.IsNullOrWhiteSpace(request.NombreCompetidor))
            return BadRequest(ApiResponse<object>.Fail("nombreCompetidor es requerido"));

        if (request.FechaHasta < request.FechaDesde)
            return BadRequest(ApiResponse<object>.Fail("fechaHasta no puede ser anterior a fechaDesde"));

        var (resultado, errorCode) = await service.ObtenerOGenerarAnalisisAsync(request, tenant.UserId);

        // 029-fix-hallazgos-code-review-competidores-alertas (FR-003/US3): antes, una respuesta
        // de Gemini sin candidates (contenido bloqueado por el filtro de seguridad) llegaba sin
        // capturar hasta ErrorHandlingMiddleware y salía como 500 genérico. Ahora es un 422
        // explícito que el frontend puede distinguir (ver contracts/competidores-analisis-api.md).
        if (errorCode == "gemini_contenido_bloqueado")
        {
            return UnprocessableEntity(ApiResponse<object>.Fail(
                "No fue posible generar el análisis para este competidor: el contenido fue bloqueado por el filtro de seguridad de Gemini. Puedes reintentar o contactar soporte si persiste.",
                [new ErrorDetail { Code = errorCode }]));
        }

        return Ok(ApiResponse<AnalisisCompetidorResponse>.Ok(resultado!));
    }
}
