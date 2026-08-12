namespace MPM.Modules.Administracion.Models;

/// <summary>Fila de la lista administrable de usuarios (paginada).</summary>
public class AdminUsuarioItemDto
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string[] Roles { get; set; } = [];
    public bool Activo { get; set; }
    public DateTime? UltimoLogin { get; set; }
    public string? TenantNombre { get; set; }
    public bool EsAccountManager { get; set; }
    public long TotalCount { get; set; }
}

public class CrearUsuarioRequest
{
    public string Email { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string? TenantNombre { get; set; }
}

public class ActualizarEstadoRequest
{
    public bool Activo { get; set; }
}

public class ActualizarRolRequest
{
    public string Rol { get; set; } = string.Empty;
}

public class SetAccountManagerRequest
{
    public bool EsAccountManager { get; set; }
}

/// <summary>Fila de log unificado (cualquiera de los 5 orígenes).</summary>
public class AdminLogItemDto
{
    public long Id { get; set; }
    public string Tipo { get; set; } = string.Empty;   // auth | sync | scraper | extraccion | ai_provider
    public DateTime Fecha { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public string? Extra { get; set; }                // JSONB como texto, listo para la UI
}
