namespace MPM.Modules.Administracion.Services;

/// <summary>
/// Reglas puras de la jerarquía de roles de administración — sin I/O, testables.
///
/// Jerarquía: SuperAdmin > Admin > Analista/Usuario.
/// - Cualquier autenticado puede operar la plataforma (Analista y Usuario son
///   hoy equivalentes en permisos; la diferenciación es organizacional).
/// - Admin: gestiona Analista/Usuario y ve logs/salud del sistema.
/// - SuperAdmin: además gestiona Admins y SuperAdmins y la configuración de IA.
/// </summary>
public static class AdminRoleRules
{
    /// <summary>Roles administrables desde el módulo.</summary>
    public static readonly string[] AllRoles = ["SuperAdmin", "Admin", "Analista", "Usuario"];

    /// <summary>Roles privilegiados: solo un SuperAdmin puede crearlos o asignarlos.</summary>
    public static readonly string[] PrivilegedRoles = ["SuperAdmin", "Admin"];

    /// <summary>Roles que un Admin puede gestionar (crear, asignar, modificar).</summary>
    public static readonly string[] AdminManagedRoles = ["Analista", "Usuario"];

    public static bool EsRolValido(string? rol)
        => !string.IsNullOrWhiteSpace(rol) && AllRoles.Contains(rol);

    /// <summary>¿El actor puede crear o asignar el rol objetivo?</summary>
    public static bool PuedeGestionarRol(string[] actorRoles, string rolObjetivo)
    {
        if (!EsRolValido(rolObjetivo)) return false;
        if (actorRoles.Contains("SuperAdmin")) return true;
        return actorRoles.Contains("Admin") && !PrivilegedRoles.Contains(rolObjetivo);
    }

    /// <summary>¿El actor puede gestionar (modificar estado/rol/flags de) un usuario con los roles dados?</summary>
    public static bool PuedeGestionarUsuario(string[] actorRoles, string[] rolesObjetivo)
    {
        if (actorRoles.Contains("SuperAdmin")) return true;
        if (!actorRoles.Contains("Admin")) return false;
        return !rolesObjetivo.Any(r => PrivilegedRoles.Contains(r));
    }
}
