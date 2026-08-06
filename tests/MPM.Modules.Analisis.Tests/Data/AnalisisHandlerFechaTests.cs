using FluentAssertions;
using MPM.Core.Data;
using MPM.Modules.Analisis.Data;
using System.Linq;
using Xunit;

namespace MPM.Modules.Analisis.Tests.Data;

/// <summary>Cubre 030-qol-frontend-y-fix-scraper US4/FR-010: filtro de fecha en /analisis.
/// Corre contra el Postgres real de docker-compose (localhost:5433), mismo patrón que
/// <see cref="LicitacionHandlerListarFechaTests"/> en MPM.Modules.Licitaciones.Tests.</summary>
public class AnalisisHandlerFechaTests
{
    private const string TestConnectionString =
        "Host=localhost;Port=5433;Database=mpm;Username=mpm;Password=mpm_password";

    private static AnalisisHandler BuildHandler() => new(new DbConnectionFactory(TestConnectionString));

    [Fact]
    public async Task ListarWorkspacesAsync_SinFiltroDeFecha_NoLanzaExcepcion()
    {
        var handler = BuildHandler();

        var act = () => handler.ListarWorkspacesAsync(page: 1, pageSize: 5, search: null, estado: null);

        await act.Should().NotThrowAsync("el comportamiento sin filtro de fecha debe ser identico al de antes de agregar p_fecha_desde/p_fecha_hasta");
    }

    [Fact]
    public async Task ListarWorkspacesAsync_ConRangoDeFechaFuturo_NoLanzaExcepcion_YDevuelveVacio()
    {
        var handler = BuildHandler();
        var futuro = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(5));

        var act = () => handler.ListarWorkspacesAsync(
            page: 1, pageSize: 5, search: null, estado: null,
            fechaDesde: futuro, fechaHasta: futuro.AddDays(1));

        await act.Should().NotThrowAsync("un filtro de fecha real no debe producir un error 42883 por parametro sin tipar (mismo patron que QA BUG-002)");

        var (items, total) = await act();
        items.Should().BeEmpty("no puede haber workspaces creados en el futuro");
        total.Should().Be(0);
    }

    [Fact]
    public async Task ListarWorkspacesAsync_ConRangoDeFechaAmplio_NoExcluyeResultadosExistentes()
    {
        var handler = BuildHandler();

        var (sinFiltro, totalSinFiltro) = await handler.ListarWorkspacesAsync(page: 1, pageSize: 5, search: null, estado: null);
        if (totalSinFiltro == 0) return; // entorno sin datos de prueba -- no hay nada que comparar

        var (conFiltro, totalConFiltro) = await handler.ListarWorkspacesAsync(
            page: 1, pageSize: 5, search: null, estado: null,
            fechaDesde: new DateOnly(2020, 1, 1), fechaHasta: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        totalConFiltro.Should().Be(totalSinFiltro, "un rango que cubre todo el historico no debe excluir workspaces existentes");
        conFiltro.Should().HaveCount(sinFiltro.Count);
    }

    // spec 031 (US3): el listado debe ordenarse por fecha de adjudicación de la licitación,
    // no por created_at del workspace (V121).
    [Fact]
    public async Task ListarWorkspacesAsync_ExponeFechaAdjudicacion_YOrdenaPorEllaDescendente()
    {
        var handler = BuildHandler();

        var (items, total) = await handler.ListarWorkspacesAsync(page: 1, pageSize: 50, search: null, estado: null);
        if (total == 0) return; // entorno sin datos de prueba

        // los items con FechaAdjudicacion no nula deben venir en orden estrictamente no-creciente;
        // los que tienen NULL deben quedar después de cualquiera con fecha (NULLS LAST)
        var conFecha = items.Where(i => i.FechaAdjudicacion != null).ToList();
        for (var i = 1; i < conFecha.Count; i++)
        {
            conFecha[i].FechaAdjudicacion.Should().BeOnOrBefore(conFecha[i - 1].FechaAdjudicacion!.Value,
                "el listado debe venir ordenado por fecha de adjudicación descendente (FR-007)");
        }

        var primerIndiceSinFecha = items.FindIndex(i => i.FechaAdjudicacion == null);
        if (primerIndiceSinFecha >= 0)
        {
            items.Skip(primerIndiceSinFecha).Should().OnlyContain(i => i.FechaAdjudicacion == null,
                "una vez que aparece un item sin fecha de adjudicación, todos los siguientes deben carecer de ella (NULLS LAST)");
        }
    }
}
