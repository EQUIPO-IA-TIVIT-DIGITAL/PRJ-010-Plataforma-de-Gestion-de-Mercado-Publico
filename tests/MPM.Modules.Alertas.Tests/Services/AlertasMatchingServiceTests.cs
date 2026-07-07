using MPM.Modules.Alertas.Data;
using MPM.Modules.Alertas.Models;
using MPM.Modules.Alertas.Services;
using FluentAssertions;
using Xunit;

namespace MPM.Modules.Alertas.Tests.Services;

public class AlertasMatchingServiceTests
{
    private static ReglaActivaRow Regla(
        string keyword = "SOC", string? sinonimosJson = null,
        decimal? montoMinimo = null, decimal? montoMaximo = null,
        string[]? tipos = null, string[]? organismos = null) => new()
    {
        p_id = 1,
        p_usuario_id = "user-1",
        p_keyword = keyword,
        p_sinonimos_ia = sinonimosJson,
        p_monto_minimo = montoMinimo,
        p_monto_maximo = montoMaximo,
        p_tipos_licitacion = tipos,
        p_organismos = organismos,
    };

    private static LicitacionParaMatching Licitacion(
        string nombre = "Contratación de servicios", string? descripcion = null,
        decimal? monto = null, string? tipo = null, string? organismo = null) =>
        new(1, "COD-1", nombre, descripcion, monto, tipo, organismo);

    [Fact]
    public void EvaluarMatch_DebeCoincidirPorKeywordLiteralEnElNombre()
    {
        var regla = Regla(keyword: "cloud");
        var licitacion = Licitacion(nombre: "Servicios de cloud computing para el ministerio");

        var resultado = AlertasMatchingService.EvaluarMatch(regla, licitacion);

        resultado.Should().Be("cloud");
    }

    [Fact]
    public void EvaluarMatch_DebeCoincidirPorSinonimoAunqueNoEsteLaPalabraLiteral()
    {
        var regla = Regla(keyword: "SOC", sinonimosJson: "[\"centro de operaciones de seguridad\", \"monitoreo 24/7\"]");
        var licitacion = Licitacion(nombre: "Servicio de centro de operaciones de seguridad para la red");

        var resultado = AlertasMatchingService.EvaluarMatch(regla, licitacion);

        resultado.Should().Be("centro de operaciones de seguridad");
    }

    [Fact]
    public void EvaluarMatch_DebeRetornarNullSiNoHayCoincidenciaDeTexto()
    {
        var regla = Regla(keyword: "datacenter");
        var licitacion = Licitacion(nombre: "Compra de mobiliario de oficina");

        var resultado = AlertasMatchingService.EvaluarMatch(regla, licitacion);

        resultado.Should().BeNull();
    }

    [Fact]
    public void EvaluarMatch_DebeRespetarFiltroDeMontoMinimo()
    {
        var regla = Regla(keyword: "cloud", montoMinimo: 10_000_000);
        var licitacionBaja = Licitacion(nombre: "Servicios cloud", monto: 5_000_000);
        var licitacionAlta = Licitacion(nombre: "Servicios cloud", monto: 15_000_000);

        AlertasMatchingService.EvaluarMatch(regla, licitacionBaja).Should().BeNull();
        AlertasMatchingService.EvaluarMatch(regla, licitacionAlta).Should().Be("cloud");
    }

    [Fact]
    public void EvaluarMatch_DebeRespetarFiltroDeTipoLicitacion()
    {
        var regla = Regla(keyword: "cloud", tipos: ["LP", "LE"]);
        var licitacionTipoDistinto = Licitacion(nombre: "Servicios cloud", tipo: "CO");
        var licitacionTipoCorrecto = Licitacion(nombre: "Servicios cloud", tipo: "LP");

        AlertasMatchingService.EvaluarMatch(regla, licitacionTipoDistinto).Should().BeNull();
        AlertasMatchingService.EvaluarMatch(regla, licitacionTipoCorrecto).Should().Be("cloud");
    }

    [Fact]
    public void EvaluarMatch_DebeIgnorarSinonimosMalFormados_SinLanzarExcepcion()
    {
        var regla = Regla(keyword: "cloud", sinonimosJson: "esto no es json valido");
        var licitacion = Licitacion(nombre: "Compra de mobiliario"); // no matchea por keyword tampoco

        var resultado = AlertasMatchingService.EvaluarMatch(regla, licitacion);

        resultado.Should().BeNull();
    }
}
