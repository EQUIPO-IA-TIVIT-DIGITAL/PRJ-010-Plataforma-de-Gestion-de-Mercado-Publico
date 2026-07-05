using Dapper;
using MPM.Core.Data;
using MPM.Modules.Auth.Models;
using System.Threading;
using System.Threading.Tasks;

namespace MPM.Modules.Auth.Data;

public class AuthHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public async Task<TokenValidationResult?> ValidateResetTokenAsync(string token, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        await conn.OpenAsync(ct);

        var result = await conn.QueryFirstOrDefaultAsync<TokenValidationResult>(
            @"SELECT email AS Email, expires_at AS ExpiresAt, used_at AS UsedAt 
              FROM password_reset_tokens 
              WHERE token = @token",
            new { token });

        return result;
    }

    public async Task<long> CountActiveUsersAsync(CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var count = await conn.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM usuarios WHERE deleted_at IS NULL");
        return count;
    }

    public async Task<IEnumerable<UsuarioItemDto>> ListarUsuariosAsync(string? search = null, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var sql = @"SELECT id, email, nombre, tenant_nombre AS TenantNombre
                    FROM usuarios
                    WHERE deleted_at IS NULL
                    AND (@p_search IS NULL OR
                         nombre ILIKE '%' || @p_search || '%' OR
                         email ILIKE '%' || @p_search || '%')
                    ORDER BY nombre
                    LIMIT 50";
        return await conn.QueryAsync<UsuarioItemDto>(sql, new { p_search = search });
    }
}