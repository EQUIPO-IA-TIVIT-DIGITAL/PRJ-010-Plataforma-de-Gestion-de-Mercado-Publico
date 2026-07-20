using Dapper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MPM.Core.Data;
using MPM.Modules.Licitaciones.Data;
using MPM.Modules.Licitaciones.Services;
using Npgsql;
using Xunit;

namespace MPM.Modules.Licitaciones.Tests.Services;

/// <summary>Cubre 029-fix-hallazgos-code-review-competidores-alertas FR-010/US6 (QA BUG-003):
/// re-derivación determinística del tipo real de licitaciones del import histórico masivo a
/// partir del sufijo de codigo_externo, reusando ApiMpService.ParseTipoDesdeCodigo (misma lógica
/// que ya usa el path de sync normal).</summary>
public class ParseTipoDesdeCodigoTests
{
    [Theory]
    [InlineData("2153-41-LP26", "LP")]
    [InlineData("14-13-B226", "B")]
    [InlineData("869591-6-LR26", "LR")]
    [InlineData("622-11-I226", "I")]
    [InlineData("548874-36-I226", "I")]
    public void ParseTipoDesdeCodigo_DerivaElTipoRealDelSufijo(string codigoExterno, string tipoEsperado)
    {
        ApiMpService.ParseTipoDesdeCodigo(codigoExterno).Should().Be(tipoEsperado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sin-guiones")]
    [InlineData("123-45-678")]
    public void ParseTipoDesdeCodigo_SinSufijoReconocible_DevuelveGenerico(string? codigoExterno)
    {
        ApiMpService.ParseTipoDesdeCodigo(codigoExterno).Should().Be("Licitacion");
    }
}

/// <summary>Corre contra el Postgres real de docker-compose (localhost:5433), mismo patrón que
/// <see cref="LicitacionSearchTests"/> -- ejercita el job de backfill de punta a punta (SP real +
/// UPDATE real), no solo la función de derivación en aislamiento.</summary>
public class ImportBackfillServiceTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=localhost;Port=5433;Database=mpm;Username=mpm;Password=mpm_password";

    private readonly List<string> _codigosACleanup = new();
    private LicitacionHandler _handler = null!;
    private ImportBackfillService _service = null!;

    static ImportBackfillServiceTests()
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public Task InitializeAsync()
    {
        var dbFactory = new DbConnectionFactory(TestConnectionString);
        _handler = new LicitacionHandler(dbFactory);

        // LicitacionService no se ejercita en estos tests (solo BackfillTipoPorSufijoAsync, que
        // no lo usa) -- se pasa null! igual que LicitacionServiceTests hace con dependencias no
        // usadas por el método bajo prueba.
        _service = new ImportBackfillService(_handler, null!, NullLogger<ImportBackfillService>.Instance);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();
        foreach (var codigo in _codigosACleanup)
            await conn.ExecuteAsync("DELETE FROM licitaciones WHERE codigo_externo = @codigo", new { codigo });
    }

    private async Task<string> CrearLicitacionConTipoGenericoAsync(NpgsqlConnection conn, string sufijo)
    {
        // ParseTipoDesdeCodigo espera exactamente 3 segmentos separados por '-' (mismo formato
        // real: "622-11-I226") -- la unicidad va en el 2do segmento, no rompe el parseo del 3ro.
        var codigo = $"999{Random.Shared.Next(100000, 999999)}-{Random.Shared.Next(1, 9999)}-{sufijo}";
        _codigosACleanup.Add(codigo);
        await conn.ExecuteAsync(
            """
            INSERT INTO licitaciones (codigo_externo, nombre, descripcion, codigo_estado, tipo,
                                       organismo, unidad_tecnica, moneda, monto_estimado,
                                       fecha_publicacion, fecha_cierre, link, raw_data)
            VALUES (@codigo, 'Licitación de prueba US6', 'Descripción real', 5, 'Licitacion',
                    'Organismo real', NULL, 'CLP', NULL, NOW(), NOW(), 'https://example.test', '{}'::JSONB)
            """,
            new { codigo });
        return codigo;
    }

    [Fact]
    public async Task BackfillTipoPorSufijoAsync_ActualizaTipoGenerico_ConElSufijoReal()
    {
        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();
        var codigo = await CrearLicitacionConTipoGenericoAsync(conn, "LR26");

        var resultado = await _service.BackfillTipoPorSufijoAsync(limite: 10000);

        resultado.Candidatos.Should().BeGreaterThanOrEqualTo(1);
        resultado.Actualizados.Should().BeGreaterThanOrEqualTo(1);

        var tipoFinal = await conn.ExecuteScalarAsync<string?>(
            "SELECT tipo FROM licitaciones WHERE codigo_externo = @codigo", new { codigo });
        tipoFinal.Should().Be("LR");
    }

    [Fact]
    public async Task BackfillTipoPorSufijoAsync_EsIdempotente_SegundaCorridaNoLoVuelveAContar()
    {
        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();
        var codigo = await CrearLicitacionConTipoGenericoAsync(conn, "LP26");

        await _service.BackfillTipoPorSufijoAsync(limite: 10000);
        var segundaCorrida = await _service.BackfillTipoPorSufijoAsync(limite: 10000);

        // Tras la primera corrida el tipo ya no es genérico, así que la segunda no debe
        // volver a tomarlo como candidato (la SP filtra WHERE tipo = 'Licitacion'). No se
        // afirma que el total de candidatos globales llegue a 0 -- este método corre contra la
        // tabla real completa (no solo la fila de este test), y puede haber filas preexistentes
        // genuinamente irresolubles (ej. codigo_externo NULL, confirmado en la DB de prueba real)
        // que quedarán como candidatas para siempre, correctamente.
        var tipoFinal = await conn.ExecuteScalarAsync<string?>(
            "SELECT tipo FROM licitaciones WHERE codigo_externo = @codigo", new { codigo });
        tipoFinal.Should().Be("LP");
        segundaCorrida.NoResueltos.Should().NotContain(codigo,
            "esta licitación de prueba sí debía resolverse (LP26 es un sufijo válido) -- no debe reaparecer como no resuelta");
    }
}
