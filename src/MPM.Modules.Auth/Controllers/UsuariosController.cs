using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Auth.Data;
using MPM.Modules.Auth.Models;
using MPM.Shared.Models;

namespace MPM.Modules.Auth.Controllers;

[ApiController]
[Route("api/v1/usuarios")]
[Authorize]
public class UsuariosController(AuthHandler authHandler) : ControllerBase
{
    private TenantContext? GetTenant()
    {
        return HttpContext.Items["TenantContext"] as TenantContext;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<UsuarioItemDto>>>> ListarUsuarios(
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var usuarios = await authHandler.ListarUsuariosAsync(search, ct);
        return Ok(ApiResponse<IEnumerable<UsuarioItemDto>>.Ok(usuarios));
    }

    [HttpPut("mi-perfil")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    public async Task<ActionResult<ApiResponse<object>>> ActualizarMiPerfil(
        [FromBody] ActualizarPerfilRequest request,
        CancellationToken ct = default)
    {
        var tenant = GetTenant();
        if (tenant == null || string.IsNullOrEmpty(tenant.UserId))
            return Unauthorized(ApiResponse<object>.Fail("No autorizado"));

        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(ApiResponse<object>.Fail("El nombre no puede estar vacío"));

        if (!long.TryParse(tenant.UserId, out var userId))
            return BadRequest(ApiResponse<object>.Fail("Identificador de usuario inválido"));

        var success = await authHandler.ActualizarNombreUsuarioAsync(userId, request.Nombre.Trim(), ct);
        if (!success)
            return BadRequest(ApiResponse<object>.Fail("No se pudo actualizar el perfil"));

        return Ok(ApiResponse<object>.Ok(new { result = true }));
    }

    [HttpPut("mi-password")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    public async Task<ActionResult<ApiResponse<object>>> ActualizarMiPassword(
        [FromBody] ActualizarPasswordRequest request,
        CancellationToken ct = default)
    {
        var tenant = GetTenant();
        if (tenant == null || string.IsNullOrEmpty(tenant.UserId))
            return Unauthorized(ApiResponse<object>.Fail("No autorizado"));

        if (string.IsNullOrEmpty(request.PasswordActual))
            return BadRequest(ApiResponse<object>.Fail("La contraseña actual es requerida"));

        if (string.IsNullOrEmpty(request.NuevaPassword) || request.NuevaPassword.Length < 6)
            return BadRequest(ApiResponse<object>.Fail("La nueva contraseña debe tener al menos 6 caracteres"));

        if (request.NuevaPassword != request.ConfirmarPassword)
            return BadRequest(ApiResponse<object>.Fail("La confirmación de la nueva contraseña no coincide"));

        if (!long.TryParse(tenant.UserId, out var userId))
            return BadRequest(ApiResponse<object>.Fail("Identificador de usuario inválido"));

        var isOldValid = await authHandler.ValidarPasswordAsync(userId, request.PasswordActual, ct);
        if (!isOldValid)
            return BadRequest(ApiResponse<object>.Fail("La contraseña actual es incorrecta"));

        var success = await authHandler.ActualizarPasswordAsync(userId, request.NuevaPassword, ct);
        if (!success)
            return BadRequest(ApiResponse<object>.Fail("No se pudo actualizar la contraseña"));

        return Ok(ApiResponse<object>.Ok(new { result = true }));
    }
}

public class ActualizarPerfilRequest
{
    public string Nombre { get; set; } = string.Empty;
}

public class ActualizarPasswordRequest
{
    public string PasswordActual { get; set; } = string.Empty;
    public string NuevaPassword { get; set; } = string.Empty;
    public string ConfirmarPassword { get; set; } = string.Empty;
}
