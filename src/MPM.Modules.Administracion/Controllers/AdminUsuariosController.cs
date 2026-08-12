using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MPM.Modules.Administracion.Models;
using MPM.Modules.Administracion.Services;
using MPM.Shared.Models;
using System.Security.Claims;

namespace MPM.Modules.Administracion.Controllers;

/// <summary>
/// Gestión de usuarios del sistema — el "centro de administración" pensado para
/// que un Admin (con o sin conocimientos técnicos) dé de alta usuarios, asigne
/// roles, active/desactive cuentas y marque account managers de gobierno.
///
/// Jerarquía (AdminRoleRules):
/// - Admin y SuperAdmin: crean Analista/Usuario, cambian estado/rol/flags.
/// - Solo SuperAdmin: crea o gestiona Admins y SuperAdmins.
/// - Nadie puede desactivarse o cambiarse el rol a sí mismo.
/// </summary>
[ApiController]
[Route("api/v1/admin/usuarios")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminUsuariosController(AdminUsuariosService service, ILogger<AdminUsuariosController> logger)
    : ControllerBase
{
    private TenantContext? GetTenant() => HttpContext.Items["TenantContext"] as TenantContext;

    // Roles del actor leídos del principal autenticado (ClaimTypes.Role) — el mismo
    // mecanismo que usa [Authorize(Roles)], sin depender del TenantContext.
    private (string[] roles, string? userId) Actor()
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        var userId = User.FindFirst("user_id")?.Value;
        return (roles, userId);
    }

    /// <summary>Lista paginada de usuarios con búsqueda por nombre/email.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<AdminUsuarioItemDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    [ProducesResponseType(typeof(ApiResponse<object>), 403)]
    public async Task<ActionResult<ApiResponse<IEnumerable<AdminUsuarioItemDto>>>> Listar(
        [FromQuery] string? search = null,
        [FromQuery] int pagina = 1,
        [FromQuery] int paginaSize = 20,
        CancellationToken ct = default)
    {
        var (items, total) = await service.ListarAsync(search, Math.Max(1, pagina), Math.Clamp(paginaSize, 1, 100), ct);
        var list = items.ToList();
        return Ok(ApiResponse<IEnumerable<AdminUsuarioItemDto>>.Ok(list));
    }

    /// <summary>Crea un usuario con contraseña temporal y rol inicial.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AdminUsuarioItemDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    [ProducesResponseType(typeof(ApiResponse<object>), 403)]
    public async Task<ActionResult<ApiResponse<AdminUsuarioItemDto>>> Crear(
        [FromBody] CrearUsuarioRequest request, CancellationToken ct = default)
    {
        var (roles, _) = Actor();
        try
        {
            var id = await service.CrearAsync(roles, request, ct);
            var usuario = await service.ObtenerAsync(id, ct);
            logger.LogInformation("Usuario {Email} creado con rol {Rol} por {Actor}",
                request.Email, request.Rol, string.Join(',', roles));
            return Ok(ApiResponse<AdminUsuarioItemDto>.Ok(usuario));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Activa o desactiva un usuario (no permite auto-desactivarse).</summary>
    [HttpPut("{id:long}/estado")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    [ProducesResponseType(typeof(ApiResponse<object>), 403)]
    public async Task<ActionResult<ApiResponse<object>>> ActualizarEstado(
        long id, [FromBody] ActualizarEstadoRequest request, CancellationToken ct = default)
    {
        var (roles, actorUserId) = Actor();
        if (!long.TryParse(actorUserId, out var actorId))
            return Unauthorized(ApiResponse<object>.Fail("No autorizado"));

        try
        {
            await service.ActualizarEstadoAsync(roles, actorId, id, request.Activo, ct);
            return Ok(ApiResponse<object>.Ok(new { result = true }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Cambia el rol de un usuario (jerarquía: solo SuperAdmin toca roles privilegiados).</summary>
    [HttpPut("{id:long}/rol")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    [ProducesResponseType(typeof(ApiResponse<object>), 403)]
    public async Task<ActionResult<ApiResponse<object>>> ActualizarRol(
        long id, [FromBody] ActualizarRolRequest request, CancellationToken ct = default)
    {
        var (roles, actorUserId) = Actor();
        if (!long.TryParse(actorUserId, out var actorId))
            return Unauthorized(ApiResponse<object>.Fail("No autorizado"));

        try
        {
            await service.ActualizarRolAsync(roles, actorId, id, request.Rol, ct);
            return Ok(ApiResponse<object>.Ok(new { result = true }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Marca/desmarca a un usuario como account manager de gobierno (destino de alertas).</summary>
    [HttpPut("{id:long}/account-manager")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    [ProducesResponseType(typeof(ApiResponse<object>), 403)]
    public async Task<ActionResult<ApiResponse<object>>> SetAccountManager(
        long id, [FromBody] SetAccountManagerRequest request, CancellationToken ct = default)
    {
        var (roles, _) = Actor();
        try
        {
            await service.SetAccountManagerAsync(roles, id, request.EsAccountManager, ct);
            return Ok(ApiResponse<object>.Ok(new { result = true }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
