using FluentAssertions;
using Npgsql;
using Xunit;

namespace MPM.Modules.Licitaciones.Tests.Data;

/// <summary>Cubre QA BUG-008: la búsqueda de <c>usp_Licitaciones_Listar</c> usaba ILIKE con
/// comodín inicial (no aprovecha ningún índice). Corre contra el Postgres real de
/// docker-compose (localhost:5433) — requiere V153 aplicada: desde V153 el match es una
/// expresión inline con <c>unaccent_immutable</c> (ya no la columna <c>search_vector</c>),
/// y el test refleja el predicado exacto del SP para que el planner use el índice de expresión.</summary>
public class LicitacionSearchTests
{
    private const string TestConnectionString =
        "Host=localhost;Port=5433;Database=mpm;Username=mpm;Password=mpm_password";

    [Fact]
    public async Task UspLicitacionesListar_UsaElIndiceDeSearchVector_NoSeqScan()
    {
        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();

        // Se compara el plan de la condición equivalente que usa el proc (no se puede EXPLAIN
        // dentro de una función plpgsql directamente; el planner trata la función como caja
        // negra en el EXPLAIN del SELECT externo). Desde V153 el SP matchea la expresión
        // inline con unaccent_immutable, que es la que cubre idx_licitaciones_search_vector.
        await using var cmd = new NpgsqlCommand(
            "EXPLAIN SELECT l.id FROM licitaciones l WHERE l.deleted_at IS NULL AND " +
            "(setweight(to_tsvector('spanish', unaccent_immutable(coalesce(l.nombre,''))), 'A') || " +
            "setweight(to_tsvector('spanish', unaccent_immutable(coalesce(l.descripcion,''))), 'B') || " +
            "setweight(to_tsvector('spanish', unaccent_immutable(coalesce(l.codigo_externo,''))), 'C')) " +
            "@@ websearch_to_tsquery('spanish', unaccent_immutable(@search))", conn);
        cmd.Parameters.AddWithValue("search", "construccion");
        await using var reader = await cmd.ExecuteReaderAsync();

        var planLines = new List<string>();
        while (await reader.ReadAsync()) planLines.Add(reader.GetString(0));
        var plan = string.Join("\n", planLines);

        plan.Should().Contain("Bitmap Index Scan on idx_licitaciones_search_vector",
            "la búsqueda por texto debe usar el índice GIN de search_vector, no un Seq Scan completo");
        plan.Should().NotContain("Seq Scan on licitaciones");
    }

    [Fact]
    public async Task UspLicitacionesListar_BuscaPorNombre_DevuelveResultados()
    {
        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();

        // Toma un término real de una licitación existente para evitar depender de datos fijos.
        var codigo = await conn.ExecuteScalarStringAsync("SELECT codigo_externo FROM licitaciones WHERE deleted_at IS NULL LIMIT 1");
        if (codigo == null) return; // base de test vacía — nada que validar

        await using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM usp_Licitaciones_Listar(1, 20, @search, NULL, NULL, NULL, NULL, NULL, 'fecha_publicacion', 'desc')", conn);
        cmd.Parameters.AddWithValue("search", codigo);
        var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);

        count.Should().BeGreaterThan(0, $"buscar por el código exacto '{codigo}' debe encontrar al menos esa licitación");
    }
}

internal static class NpgsqlConnectionExtensions
{
    public static async Task<string?> ExecuteScalarStringAsync(this NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }
}
