using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MPM.Modules.Propuestas.Data;
using MPM.Modules.Propuestas.Services;
using MPM.Shared.Models;

namespace MPM.Modules.Propuestas.Filters;

public sealed class PropuestasExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var (status, code, message) = context.Exception switch
        {
            PropuestasCatalogoService.PropuestasValidationException ex => (400, ex.Code, ex.Message),
            PropuestasHandler.PropuestasDataException ex => (ex.Code switch { "PRO_001" => 404, "PRO_002" => 409, _ => 500 }, ex.Code, ex.Message),
            CensusCertificationSyncService.CensusPayloadTooLargeException ex => (400, "VAL_008", ex.Message),
            PropuestasRecomendacionService.RecomendacionException ex => (ex.Code == "LIC_001" ? 404 : 422, ex.Code, ex.Message),
            HttpRequestException ex => (502, "CEN_002", ex.Message),
            _ => (500, "SYS_001", "Error interno del módulo Propuestas"),
        };

        context.Result = new ObjectResult(ApiResponse<object>.Fail(
            message, [new ErrorDetail { Code = code, Message = message }])) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
