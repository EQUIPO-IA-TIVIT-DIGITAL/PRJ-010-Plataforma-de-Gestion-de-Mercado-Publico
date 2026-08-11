using Dapper;
using MPM.Core.Data;

namespace MPM.Core.Data;

/// <summary>Fila activa de la tabla system_ai_provider.</summary>
public sealed record AiProviderRow(
    string Provider,
    string? Endpoint,
    string Model,
    long UpdatedByUserId,
    string UpdatedByUsername,
    DateTime UpdatedAt);

/// <summary>
/// Acceso a la configuración persistida del proveedor de IA (033-migracion-qwen-g4).
/// Solo vía stored procedures (constitución II) — sin ORM.
/// </summary>
public interface ISystemConfigData
{
    Task<AiProviderRow?> ObtenerAsync(CancellationToken ct = default);

    Task ActualizarAsync(
        string provider, string? endpoint, string model,
        long updatedByUserId, string updatedByUsername,
        CancellationToken ct = default);
}

public class SystemConfigData(DbConnectionFactory connectionFactory) : ISystemConfigData
{
    public async Task<AiProviderRow?> ObtenerAsync(CancellationToken ct = default)
    {
        using var conn = connectionFactory.Create();
        await conn.OpenAsync(ct);

        var row = await conn.QueryFirstOrDefaultAsync<(string Provider, string? Endpoint, string Model,
            long UpdatedByUserId, string UpdatedByUsername, DateTime UpdatedAt)?>(
            "SELECT * FROM usp_SystemConfig_ObtenerAiProvider()");

        if (row == null) return null;
        var r = row.Value;
        return new AiProviderRow(r.Provider, r.Endpoint, r.Model, r.UpdatedByUserId, r.UpdatedByUsername, r.UpdatedAt);
    }

    public async Task ActualizarAsync(
        string provider, string? endpoint, string model,
        long updatedByUserId, string updatedByUsername,
        CancellationToken ct = default)
    {
        using var conn = connectionFactory.Create();
        await conn.OpenAsync(ct);

        await conn.ExecuteAsync(
            "SELECT usp_SystemConfig_ActualizarAiProvider(@p_provider, @p_endpoint, @p_model, @p_updated_by_user_id, @p_updated_by_username)",
            new
            {
                p_provider = provider,
                p_endpoint = endpoint,
                p_model = model,
                p_updated_by_user_id = updatedByUserId,
                p_updated_by_username = updatedByUsername
            });
    }
}
