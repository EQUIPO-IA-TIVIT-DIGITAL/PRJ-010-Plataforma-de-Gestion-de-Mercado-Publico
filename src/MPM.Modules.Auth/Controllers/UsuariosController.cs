using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Auth.Data;
using MPM.Modules.Auth.Models;
using MPM.Shared.Models;

namespace MPM.Modules.Auth.Controllers;

[ApiController]
[Route("api/v1/usuarios")]
public class UsuariosController(AuthHandler authHandler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<UsuarioItemDto>>>> ListarUsuarios(
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var usuarios = await authHandler.ListarUsuariosAsync(search, ct);
        return Ok(ApiResponse<IEnumerable<UsuarioItemDto>>.Ok(usuarios));
    }
}
