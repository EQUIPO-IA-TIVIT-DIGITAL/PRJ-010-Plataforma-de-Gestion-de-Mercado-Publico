using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Propuestas.Models;
using MPM.Modules.Propuestas.Services;
using MPM.Shared.Models;

namespace MPM.Modules.Propuestas.Controllers;

[ApiController]
[Route("api/v1/propuestas/recomendaciones")]
[Authorize]
[ServiceFilter(typeof(MPM.Modules.Propuestas.Filters.PropuestasExceptionFilter))]
public class PropuestasRecomendacionesController(PropuestasRecomendacionService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<RecomendacionResponseDto>>> Recomendar([FromBody] RecomendacionRequest request, CancellationToken ct = default)
    {
        try
        {
            return Ok(ApiResponse<RecomendacionResponseDto>.Ok(await service.RecomendarAsync(request, ct)));
        }
        catch (PropuestasRecomendacionService.RecomendacionException ex)
        {
            var status = ex.Code == "LIC_001" ? 404 : 422;
            return StatusCode(status, ApiResponse<object>.Fail(ex.Message, [new ErrorDetail { Code = ex.Code, Message = ex.Message }]));
        }
    }
}
