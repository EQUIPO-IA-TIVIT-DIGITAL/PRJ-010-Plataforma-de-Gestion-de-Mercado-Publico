using Dapper;
using FluentAssertions;
using MPM.Core.Data;
using MPM.Modules.Alertas.Data;
using Npgsql;
using Xunit;

namespace MPM.Modules.Alertas.Tests.Data;

/// <summary>Cubre 029-fix-hallazgos-code-review-competidores-alertas FR-004/US4: el UPDATE de
/// <c>ActualizarLicitacionEnCalienteAsync</c> le faltaba el guard <c>deleted_at IS NULL</c> que
/// sí tiene la query equivalente de Licitaciones -- un match de Alertas en curso podía
/// "resucitar" datos de una licitación ya eliminada (soft-delete). Corre contra el Postgres real
/// de docker-compose (localhost:5433), mismo patrón que <c>LicitacionSearchTests</c>.</summary>
public class AlertasHandlerActualizarEnCalienteTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=localhost;Port=5433;Database=mpm;Username=mpm;Password=mpm_password";

    private readonly List<string> _codigosACleanup = new();
    private AlertasHandler _handler = null!;

    static AlertasHandlerActualizarEnCalienteTests()
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public Task InitializeAsync()
    {
        _handler = new AlertasHandler(new DbConnectionFactory(TestConnectionString));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();
        foreach (var codigo in _codigosACleanup)
            await conn.ExecuteAsync("DELETE FROM licitaciones WHERE codigo_externo = @codigo", new { codigo });
    }

    private async Task<string> CrearLicitacionAsync(NpgsqlConnection conn, bool eliminada)
    {
        var codigo = $"TEST-029-US4-{Guid.NewGuid():N}";
        _codigosACleanup.Add(codigo);
        await conn.ExecuteAsync(
            """
            INSERT INTO licitaciones (codigo_externo, nombre, descripcion, codigo_estado, tipo,
                                       organismo, unidad_tecnica, moneda, monto_estimado,
                                       fecha_publicacion, fecha_cierre, link, raw_data, deleted_at)
            VALUES (@codigo, 'Licitación de prueba US4', NULL, 5, 'LE', NULL, NULL, 'CLP', NULL,
                    NOW(), NOW(), 'https://example.test', '{}'::JSONB, @deletedAt)
            """,
            new { codigo, deletedAt = eliminada ? (DateTime?)DateTime.UtcNow : null });
        return codigo;
    }

    [Fact]
    public async Task ActualizarLicitacionEnCalienteAsync_LicitacionEliminada_NoSeActualiza()
    {
        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();
        var codigo = await CrearLicitacionAsync(conn, eliminada: true);

        await _handler.ActualizarLicitacionEnCalienteAsync(
            codigo, organismo: "Organismo Resucitado", unidadTecnica: "UT", montoEstimado: 999m,
            descripcion: "Descripción resucitada", rawData: """{"Comprador":{}}""");

        var organismo = await conn.ExecuteScalarAsync<string?>(
            "SELECT organismo FROM licitaciones WHERE codigo_externo = @codigo", new { codigo });
        var deletedAt = await conn.ExecuteScalarAsync<DateTime?>(
            "SELECT deleted_at FROM licitaciones WHERE codigo_externo = @codigo", new { codigo });

        organismo.Should().BeNull("una licitación eliminada no debe ser modificada por el enriquecimiento en caliente");
        deletedAt.Should().NotBeNull("el guard no debe alterar el propio deleted_at tampoco");
    }

    [Fact]
    public async Task ActualizarLicitacionEnCalienteAsync_LicitacionActiva_SiSeActualiza()
    {
        // No debe regresionar el caso feliz: una licitación NO eliminada sí debe enriquecerse.
        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();
        var codigo = await CrearLicitacionAsync(conn, eliminada: false);

        await _handler.ActualizarLicitacionEnCalienteAsync(
            codigo, organismo: "Organismo Real", unidadTecnica: "UT Real", montoEstimado: 1234m,
            descripcion: "Descripción real", rawData: """{"Comprador":{}}""");

        var organismo = await conn.ExecuteScalarAsync<string?>(
            "SELECT organismo FROM licitaciones WHERE codigo_externo = @codigo", new { codigo });

        organismo.Should().Be("Organismo Real");
    }
}
