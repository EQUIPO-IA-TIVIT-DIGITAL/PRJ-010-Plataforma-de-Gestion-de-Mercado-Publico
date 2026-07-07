using MPM.Modules.Licitaciones.Services;
using FluentAssertions;
using Xunit;

namespace MPM.Modules.Licitaciones.Tests.Services;

public class WebFormsParserTests
{
    // Estructura basada en lo que hoy lee tools/scraper-mp/modulos/adjuntos.js vía DOM real
    // (tabla #DWNL_grdId, fila 0 = header, celdas 1-5 = datos, celda 6 = botón de descarga).
    private const string HtmlConAdjuntos = """
        <html><body>
        <form id="form1">
            <input type="hidden" name="__VIEWSTATE" value="VS123" />
            <input type="hidden" name="__VIEWSTATEGENERATOR" value="GEN456" />
            <input type="hidden" name="__EVENTVALIDATION" value="EV789" />
            <table id="DWNL_grdId">
                <tr><th>#</th><th>Nombre</th><th>Tipo</th><th>Descripcion</th><th>Tamanio</th><th>Fecha</th><th>Accion</th></tr>
                <tr>
                    <td>1</td>
                    <td>Bases Administrativas.pdf</td>
                    <td>Bases</td>
                    <td>Bases del proceso</td>
                    <td>120 KB</td>
                    <td>01-01-2026</td>
                    <td><input type="image" name="ctl00$Main$DWNL_grdId$ctl02$imgVer" id="Main_DWNL_grdId_ctl02_imgVer" /></td>
                </tr>
                <tr>
                    <td>2</td>
                    <td>Acta de Evaluacion Final.pdf</td>
                    <td>Acta de Evaluación</td>
                    <td>Resultado del proceso</td>
                    <td>340 KB</td>
                    <td>15-01-2026</td>
                    <td><input type="image" name="ctl00$Main$DWNL_grdId$ctl03$imgVer" id="Main_DWNL_grdId_ctl03_imgVer" /></td>
                </tr>
            </table>
        </form>
        </body></html>
        """;

    private const string HtmlSinAdjuntos = """
        <html><body>
        <form id="form1">
            <input type="hidden" name="__VIEWSTATE" value="VS000" />
        </form>
        </body></html>
        """;

    [Fact]
    public async Task ParseAsync_DebeExtraerCamposOcultosDeViewState()
    {
        var parser = new WebFormsParser();

        var resultado = await parser.ParseAsync(HtmlConAdjuntos);

        resultado.State.ViewState.Should().Be("VS123");
        resultado.State.ViewStateGenerator.Should().Be("GEN456");
        resultado.State.EventValidation.Should().Be("EV789");
        resultado.State.TodosLosCamposOcultos.Should().ContainKey("__VIEWSTATE");
    }

    [Fact]
    public async Task ParseAsync_DebeExtraerTodasLasFilasDeLaTabla()
    {
        var parser = new WebFormsParser();

        var resultado = await parser.ParseAsync(HtmlConAdjuntos);

        resultado.Filas.Should().HaveCount(2);
        resultado.Filas[0].Nombre.Should().Be("Bases Administrativas.pdf");
        resultado.Filas[0].BotonNombrePostback.Should().Be("ctl00$Main$DWNL_grdId$ctl02$imgVer");
    }

    [Fact]
    public async Task ParseAsync_DebeIdentificarElActaDeEvaluacionPorTipo()
    {
        var parser = new WebFormsParser();

        var resultado = await parser.ParseAsync(HtmlConAdjuntos);

        var acta = resultado.Filas.Should().ContainSingle(f => f.EsActa).Subject;
        acta.Nombre.Should().Be("Acta de Evaluacion Final.pdf");
        acta.BotonNombrePostback.Should().Be("ctl00$Main$DWNL_grdId$ctl03$imgVer");
    }

    [Fact]
    public async Task ParseAsync_DebeIdentificarActaPorNombreSiElTipoNoCoincideExacto()
    {
        var html = HtmlConAdjuntos.Replace("Acta de Evaluación", "Otro Tipo")
            .Replace("Acta de Evaluacion Final.pdf", "acta evaluacion 2026.pdf");
        var parser = new WebFormsParser();

        var resultado = await parser.ParseAsync(html);

        resultado.Filas.Should().Contain(f => f.EsActa);
    }

    [Fact]
    public async Task ParseAsync_DebeRetornarListaVaciaSiNoHayTablaDeAdjuntos()
    {
        var parser = new WebFormsParser();

        var resultado = await parser.ParseAsync(HtmlSinAdjuntos);

        resultado.Filas.Should().BeEmpty();
    }
}
