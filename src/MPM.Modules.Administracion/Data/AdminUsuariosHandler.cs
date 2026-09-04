using Dapper;
using MPM.Core.Data;
using MPM.Modules.Administracion.Models;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace MPM.Modules.Administracion.Data;

/// <summary>
/// Acceso a datos de administración de usuarios. Toda mutación pasa por los
/// stored procedures usp_Admin_* (V131); la jerarquía de roles se valida en
/// <see cref="AdminUsuariosService"/> antes de llegar acá.
/// </summary>
public class AdminUsuariosHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public async Task<IEnumerable<AdminUsuarioItemDto>> ListarUsuariosAsync(
        string? search, int pagina, int paginaSize, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        return await conn.QueryAsync<AdminUsuarioItemDto>(
            sql: "SELECT * FROM usp_Admin_ListarUsuarios(@p_search, @p_pagina, @p_pagina_size)",
            param: new { p_search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(), p_pagina = pagina, p_pagina_size = paginaSize },
            commandType: CommandType.Text);
    }

    public async Task<long> CrearUsuarioAsync(
        string email, string nombre, string password, string rol,
        string? tenantId, string? tenantNombre, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        await conn.OpenAsync(ct);

        var p = new DynamicParameters();
        p.Add("p_email", email);
        p.Add("p_nombre", nombre);
        p.Add("p_password", password);
        p.Add("p_rol", rol);
        p.Add("p_tenant_id", tenantId);
        p.Add("p_tenant_nombre", tenantNombre);
        p.Add("p_user_id", 0, dbType: DbType.Int64, direction: ParameterDirection.InputOutput);
        p.Add("p_error_msg", null, dbType: DbType.String, direction: ParameterDirection.InputOutput, size: 4000);

        await conn.ExecuteAsync("CALL usp_Admin_CrearUsuario(@p_email, @p_nombre, @p_password, @p_rol, @p_tenant_id, @p_tenant_nombre, @p_user_id, @p_error_msg)", p);

        var error = p.Get<string?>("p_error_msg");
        if (!string.IsNullOrEmpty(error))
            throw new InvalidOperationException(error);

        return p.Get<long>("p_user_id");
    }

    public async Task<AdminUsuarioItemDto?> ObtenerUsuarioAsync(long userId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        return await conn.QueryFirstOrDefaultAsync<AdminUsuarioItemDto>(
            sql: @"SELECT id, email, nombre, roles, activo, ultimo_login AS UltimoLogin, tenant_nombre AS TenantNombre
                   FROM usuarios WHERE id = @id AND deleted_at IS NULL",
            param: new { id = userId });
    }

    public async Task ActualizarEstadoAsync(long userId, bool activo, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        await conn.OpenAsync(ct);

        var p = new DynamicParameters();
        p.Add("p_user_id", userId);
        p.Add("p_activo", activo);
        p.Add("p_error_msg", null, dbType: DbType.String, direction: ParameterDirection.InputOutput, size: 4000);

        await conn.ExecuteAsync("CALL usp_Admin_ActualizarEstado(@p_user_id, @p_activo, @p_error_msg)", p);

        var error = p.Get<string?>("p_error_msg");
        if (!string.IsNullOrEmpty(error))
            throw new InvalidOperationException(error);
    }

    public async Task ActualizarRolAsync(long userId, string rol, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        await conn.OpenAsync(ct);

        var p = new DynamicParameters();
        p.Add("p_user_id", userId);
        p.Add("p_rol", rol);
        p.Add("p_error_msg", null, dbType: DbType.String, direction: ParameterDirection.InputOutput, size: 4000);

        await conn.ExecuteAsync("CALL usp_Admin_ActualizarRol(@p_user_id, @p_rol, @p_error_msg)", p);

        var error = p.Get<string?>("p_error_msg");
        if (!string.IsNullOrEmpty(error))
            throw new InvalidOperationException(error);
    }

    public async Task SetAccountManagerAsync(long userId, bool esAccountManager, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        await conn.OpenAsync(ct);

        var p = new DynamicParameters();
        p.Add("p_usuario_id", userId);
        p.Add("p_es_account_manager", esAccountManager);
        p.Add("p_error_msg", null, dbType: DbType.String, direction: ParameterDirection.InputOutput, size: 4000);

        await conn.ExecuteAsync("CALL usp_Admin_SetAccountManager(@p_usuario_id, @p_es_account_manager, @p_error_msg)", p);

        var error = p.Get<string?>("p_error_msg");
        if (!string.IsNullOrEmpty(error))
            throw new InvalidOperationException(error);
    }
}
