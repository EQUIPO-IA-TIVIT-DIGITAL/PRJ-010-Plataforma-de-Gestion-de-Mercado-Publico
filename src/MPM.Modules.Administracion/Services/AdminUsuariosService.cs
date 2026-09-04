using MPM.Modules.Administracion.Data;
using MPM.Modules.Administracion.Models;
using System.Threading;
using System.Threading.Tasks;

namespace MPM.Modules.Administracion.Services;

/// <summary>
/// Orquesta la gestión de usuarios con las reglas de jerarquía
/// (<see cref="AdminRoleRules"/>) aplicadas ANTES de tocar la BD.
/// Lanza <see cref="InvalidOperationException"/> con mensaje legible para la UI
/// cuando el actor no tiene permiso o el dato es inválido.
/// </summary>
public class AdminUsuariosService(AdminUsuariosHandler handler)
{
    private readonly AdminUsuariosHandler _handler = handler;

    public async Task<(IEnumerable<AdminUsuarioItemDto> Items, long Total)> ListarAsync(
        string? search, int pagina, int paginaSize, CancellationToken ct = default)
    {
        var items = await _handler.ListarUsuariosAsync(search, pagina, paginaSize, ct);
        var list = items as AdminUsuarioItemDto[] ?? items.ToArray();
        var total = list.Length > 0 ? list[0].TotalCount : 0;
        return (list, total);
    }

    public async Task<long> CrearAsync(
        string[] actorRoles, CrearUsuarioRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            throw new InvalidOperationException("Ingresa un correo electrónico válido");

        if (string.IsNullOrWhiteSpace(request.Nombre))
            throw new InvalidOperationException("El nombre es requerido");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            throw new InvalidOperationException("La contraseña inicial debe tener al menos 6 caracteres");

        if (!AdminRoleRules.EsRolValido(request.Rol))
            throw new InvalidOperationException("El rol seleccionado no es válido");

        if (!AdminRoleRules.PuedeGestionarRol(actorRoles, request.Rol))
            throw new InvalidOperationException(
                "No tienes permisos para crear usuarios con el rol '" + request.Rol + "'. Solo un SuperAdmin gestiona Admins y SuperAdmins.");

        return await _handler.CrearUsuarioAsync(
            request.Email.Trim(), request.Nombre.Trim(), request.Password, request.Rol,
            request.TenantId, request.TenantNombre, ct);
    }

    public async Task<AdminUsuarioItemDto> ObtenerAsync(long userId, CancellationToken ct = default)
    {
        var usuario = await _handler.ObtenerUsuarioAsync(userId, ct);
        if (usuario == null)
            throw new InvalidOperationException("El usuario no existe");
        return usuario;
    }

    public async Task ActualizarEstadoAsync(
        string[] actorRoles, long actorUserId, long targetUserId, bool activo,
        CancellationToken ct = default)
    {
        if (actorUserId == targetUserId)
            throw new InvalidOperationException("No puedes desactivar tu propia cuenta");

        var target = await ObtenerAsync(targetUserId, ct);
        if (!AdminRoleRules.PuedeGestionarUsuario(actorRoles, target.Roles))
            throw new InvalidOperationException(
                "No tienes permisos para modificar usuarios con rol privilegiado (Admin/SuperAdmin)");

        await _handler.ActualizarEstadoAsync(targetUserId, activo, ct);
    }

    public async Task ActualizarRolAsync(
        string[] actorRoles, long actorUserId, long targetUserId, string nuevoRol,
        CancellationToken ct = default)
    {
        if (actorUserId == targetUserId)
            throw new InvalidOperationException("No puedes cambiar tu propio rol");

        if (!AdminRoleRules.EsRolValido(nuevoRol))
            throw new InvalidOperationException("El rol seleccionado no es válido");

        var target = await ObtenerAsync(targetUserId, ct);
        if (!AdminRoleRules.PuedeGestionarUsuario(actorRoles, target.Roles))
            throw new InvalidOperationException(
                "No tienes permisos para modificar usuarios con rol privilegiado (Admin/SuperAdmin)");

        if (!AdminRoleRules.PuedeGestionarRol(actorRoles, nuevoRol))
            throw new InvalidOperationException(
                "No tienes permisos para asignar el rol '" + nuevoRol + "'. Solo un SuperAdmin gestiona Admins y SuperAdmins.");

        await _handler.ActualizarRolAsync(targetUserId, nuevoRol, ct);
    }

    public async Task SetAccountManagerAsync(
        string[] actorRoles, long targetUserId, bool esAccountManager,
        CancellationToken ct = default)
    {
        var target = await ObtenerAsync(targetUserId, ct);
        if (!AdminRoleRules.PuedeGestionarUsuario(actorRoles, target.Roles))
            throw new InvalidOperationException(
                "No tienes permisos para modificar usuarios con rol privilegiado (Admin/SuperAdmin)");

        await _handler.SetAccountManagerAsync(targetUserId, esAccountManager, ct);
    }
}
