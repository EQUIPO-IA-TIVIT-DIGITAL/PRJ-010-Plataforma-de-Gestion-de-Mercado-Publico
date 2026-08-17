using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Propuestas.Models;
using MPM.Modules.Propuestas.Services;
using MPM.Shared.Models;

namespace MPM.Modules.Propuestas.Controllers;

[ApiController]
[Route("api/v1/licitaciones/{codigoExterno}/decision")]
[Authorize]
[ServiceFilter(typeof(MPM.Modules.Propuestas.Filters.PropuestasExceptionFilter))]
public sealed class DecisionAvisosController(IDecisionAvisoService avisos) : ControllerBase
{
    [HttpPost("{decisionId:long}/avisar")]
    public async Task<ActionResult<ApiResponse<AvisarResponse>>> Avisar(
        string codigoExterno, long decisionId, [FromBody] AvisarRequest? request,
        CancellationToken ct = default)
    {
        if (HttpContext.Items["TenantContext"] is not TenantContext)
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var result = await avisos.AvisarAsync(codigoExterno, decisionId, request?.Destinatarios, ct);
        return Ok(ApiResponse<AvisarResponse>.Ok(result));
    }
}
