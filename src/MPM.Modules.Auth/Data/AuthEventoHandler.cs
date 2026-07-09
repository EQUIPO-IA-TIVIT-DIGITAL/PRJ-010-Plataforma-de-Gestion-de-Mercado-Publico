using Dapper;
using MPM.Core.Data;

namespace MPM.Modules.Auth.Data;

/// <summary>
/// Auditoría de inicios de sesión exitosos (QA BUG-010) — usada para medir adopción, pedido de
/// negocio con deadline propio. Nunca debe bloquear ni fallar el login: el llamador decide si
/// registrar el evento es "best effort" (ver AuthController.Login).
/// </summary>
public class AuthEventoHandler(DbConnectionFactory dbFactory)
{
    public async Task RegistrarAsync(string userId, string tenantId, string email, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        await using var conn = dbFactory.Create();
        await conn.ExecuteAsync(
            "SELECT usp_Auth_RegistrarEvento(@p_user_id, @p_tenant_id, @p_email, @p_ip_address, @p_user_agent)",
            new { p_user_id = userId, p_tenant_id = tenantId, p_email = email, p_ip_address = ipAddress, p_user_agent = userAgent });
    }
}
