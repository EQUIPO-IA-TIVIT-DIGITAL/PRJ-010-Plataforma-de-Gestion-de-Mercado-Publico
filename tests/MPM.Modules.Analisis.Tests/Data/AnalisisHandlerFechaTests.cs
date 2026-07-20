using FluentAssertions;
using MPM.Core.Data;
using MPM.Modules.Analisis.Data;
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
}
