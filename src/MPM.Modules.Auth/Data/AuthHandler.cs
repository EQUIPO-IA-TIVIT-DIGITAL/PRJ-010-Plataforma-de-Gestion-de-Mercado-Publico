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

    public async Task<bool> ActualizarNombreUsuarioAsync(long userId, string nuevoNombre, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var sql = "UPDATE usuarios SET nombre = @nombre, updated_at = CURRENT_TIMESTAMP WHERE id = @id AND deleted_at IS NULL";
        var rows = await conn.ExecuteAsync(sql, new { nombre = nuevoNombre, id = userId });
        return rows > 0;
    }

    public async Task<bool> ValidarPasswordAsync(long userId, string passwordActual, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var sql = "SELECT (password_hash = crypt(@passwordActual, password_hash)) AS IsValid FROM usuarios WHERE id = @id AND deleted_at IS NULL";
        var isValid = await conn.QueryFirstOrDefaultAsync<bool>(sql, new { passwordActual, id = userId });
        return isValid;
    }

    public async Task<bool> ActualizarPasswordAsync(long userId, string nuevaPassword, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var sql = "UPDATE usuarios SET password_hash = crypt(@nuevaPassword, gen_salt('bf', 11)), updated_at = CURRENT_TIMESTAMP WHERE id = @id AND deleted_at IS NULL";
        var rows = await conn.ExecuteAsync(sql, new { nuevaPassword, id = userId });
        return rows > 0;
    }
}