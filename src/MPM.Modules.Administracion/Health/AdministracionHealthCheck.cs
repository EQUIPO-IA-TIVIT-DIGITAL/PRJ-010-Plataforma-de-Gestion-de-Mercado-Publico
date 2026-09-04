using Microsoft.Extensions.Diagnostics.HealthChecks;
using MPM.Core.Data;

namespace MPM.Modules.Administracion.Health;

public class AdministracionHealthCheck(DbConnectionFactory dbFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = dbFactory.Create();
            await conn.OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy("administracion ok");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("administracion db failed", ex);
        }
    }
}
