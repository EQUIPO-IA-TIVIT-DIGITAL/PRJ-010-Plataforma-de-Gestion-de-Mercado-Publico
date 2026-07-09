using Dapper;
using MPM.Core.Data;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MPM.Api.Database;

public class DatabaseInitializer(ILogger<DatabaseInitializer> logger, DbConnectionFactory dbFactory)
{
    // Hash arbitrario y fijo del proyecto para pg_advisory_lock — coordina que solo una
    // instancia aplique migraciones a la vez cuando varias arrancan en paralelo (Cloud Run).
    private const long MigrationLockKey = 72197719;

    public async Task InitializeAsync()
    {
        await using var conn = dbFactory.Create();
        conn.Open();

        await conn.ExecuteAsync(
            "SELECT pg_advisory_lock(@key)", new { key = MigrationLockKey });

        try
        {
            await conn.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS _migrations (
                    version VARCHAR(50) PRIMARY KEY,
                    applied_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )
            """);

            var assembly = Assembly.GetExecutingAssembly();
            var scriptNames = assembly.GetManifestResourceNames()
                .Where(n => n.Contains(".Database.Scripts.") && n.EndsWith(".sql"))
                .OrderBy(n => n)
                .ToList();

            foreach (var resourceName in scriptNames)
            {
                var version = ExtractVersion(resourceName);
                if (string.IsNullOrEmpty(version)) continue;

                var alreadyApplied = await conn.QueryFirstOrDefaultAsync<string>(
                    "SELECT version FROM _migrations WHERE version = @v", new { v = version });

                if (alreadyApplied != null)
                {
                    logger.LogInformation("Migration {Version} already applied, skipping.", version);
                    continue;
                }

                logger.LogInformation("Applying migration {Version}...", version);
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null) continue;

                using var reader = new StreamReader(stream);
                var sql = await reader.ReadToEndAsync();

                try
                {
                    await conn.ExecuteAsync(sql);
                    await conn.ExecuteAsync(
                        "INSERT INTO _migrations (version) VALUES (@v)", new { v = version });
                    logger.LogInformation("Migration {Version} applied successfully.", version);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Migration {Version} failed.", version);
                    // No se traga el error: se propaga para que el host aborte el arranque
                    // en vez de quedar "healthy" con un esquema incompleto (QA BUG-001).
                    throw;
                }
            }
        }
        finally
        {
            await conn.ExecuteAsync(
                "SELECT pg_advisory_unlock(@key)", new { key = MigrationLockKey });
        }
    }

    private static string? ExtractVersion(string resourceName)
    {
        var match = Regex.Match(resourceName, @"V(\d{3})__");
        return match.Success ? "V" + match.Groups[1].Value : null;
    }
}
