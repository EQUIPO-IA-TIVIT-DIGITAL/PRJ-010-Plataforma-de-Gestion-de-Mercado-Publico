using FluentAssertions;
using MPM.Core.Data;
using MPM.Modules.Licitaciones.Data;
using Xunit;

namespace MPM.Modules.Licitaciones.Tests.Data;

/// <summary>Cubre 029-fix-hallazgos-code-review-competidores-alertas FR-009 (QA BUG-002):
/// ListarAsync pasaba p_fecha_desde/p_fecha_hasta en un objeto anónimo sin DbType.Date
/// explícito, y Postgres no resolvía el overload de usp_Licitaciones_Listar (DATE), 500 en
/// cada filtro de fecha. Corre contra el Postgres real de docker-compose (localhost:5433),
/// mismo patrón que <see cref="LicitacionSearchTests"/>.</summary>
public class LicitacionHandlerListarFechaTests
{
    private const string TestConnectionString =
        "Host=localhost;Port=5433;Database=mpm;Username=mpm;Password=mpm_password";

    private static LicitacionHandler BuildHandler() => new(new DbConnectionFactory(TestConnectionString));

    [Fact]
    public async Task ListarAsync_ConFechaDesde_NoLanzaExcepcion_YDevuelveResultados()
    {
        var handler = BuildHandler();

        var act = () => handler.ListarAsync(
            page: 1, pageSize: 5, search: null, estado: null, tipo: null, organismo: null,
            fechaDesde: new DateTime(2020, 1, 1), fechaHasta: null,
            sortBy: "fecha_publicacion", sortDir: "desc");

        await act.Should().NotThrowAsync("un filtro de fecha real no debe producir un error 42883 por parámetro sin tipar");

        var (items, totalCount) = await act();
        totalCount.Should().BeGreaterThan(0, "hay licitaciones reales publicadas después de 2020-01-01");
        items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ListarAsync_ConFechaDesdeYFechaHasta_NoLanzaExcepcion()
    {
        var handler = BuildHandler();

        var act = () => handler.ListarAsync(
            page: 1, pageSize: 5, search: null, estado: null, tipo: null, organismo: null,
            fechaDesde: new DateTime(2025, 1, 1), fechaHasta: new DateTime(2025, 12, 31),
            sortBy: "fecha_publicacion", sortDir: "desc");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ListarAsync_ConFechaYOtrosFiltrosCombinados_NoLanzaExcepcion()
    {
        // Edge case de la spec (US2b): el filtro de fecha debe funcionar en combinación con
        // cualquier otro filtro ya soportado por usp_Licitaciones_Listar, no solo aislado.
        var handler = BuildHandler();

        var act = () => handler.ListarAsync(
            page: 1, pageSize: 5, search: null, estado: (short)5, tipo: "LE", organismo: null,
            fechaDesde: new DateTime(2025, 1, 1), fechaHasta: null,
            sortBy: "fecha_publicacion", sortDir: "desc");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ListarAsync_SinFiltrosDeFecha_SigueFuncionando()
    {
        // No debe regresionar el caso feliz ya existente (sin filtro de fecha).
        var handler = BuildHandler();

        var act = () => handler.ListarAsync(
            page: 1, pageSize: 5, search: null, estado: null, tipo: null, organismo: null,
            fechaDesde: null, fechaHasta: null,
            sortBy: "fecha_publicacion", sortDir: "desc");

        await act.Should().NotThrowAsync();
    }
}
