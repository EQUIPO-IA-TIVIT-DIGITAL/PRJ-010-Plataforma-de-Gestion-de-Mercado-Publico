using AngleSharp;
using AngleSharp.Dom;
using MPM.Modules.Licitaciones.Models;

namespace MPM.Modules.Licitaciones.Services;

/// <summary>
/// Parsea el HTML de la ventana de adjuntos del portal de Mercado Público (tabla WebForms
/// <c>#DWNL_grdId</c>), replicando en HTML estático la misma estructura que hoy lee
/// <c>tools/scraper-mp/modulos/adjuntos.js</c> vía DOM en un navegador real.
///
/// Nota: los <c>&lt;input type="image"&gt;</c> de ASP.NET WebForms (ImageButton) no usan el
/// mecanismo clásico de postback <c>__EVENTTARGET</c> — al hacer click, el navegador envía
/// el atributo <c>name</c> del input como <c>{name}.x</c>/<c>{name}.y</c> (coordenadas de
/// click). Por eso <see cref="AdjuntoFila.BotonNombrePostback"/> captura el <c>name</c>, no
/// el <c>id</c>. Ver <c>AdjuntosHttpExtractor</c> para cómo se arma el POST.
/// </summary>
public class WebFormsParser
{
    private const string TablaAdjuntosId = "DWNL_grdId";

    public async Task<AdjuntosListado> ParseAsync(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = await context.OpenAsync(req => req.Content(html));

        var state = ExtraerEstado(document);
        var filas = ExtraerFilas(document);

        return new AdjuntosListado(filas, state);
    }

    private static WebFormsState ExtraerEstado(IDocument document)
    {
        var camposOcultos = document.QuerySelectorAll("input[type=hidden]")
            .ToDictionary(
                el => el.GetAttribute("name") ?? el.GetAttribute("id") ?? "",
                el => el.GetAttribute("value") ?? "");
        camposOcultos.Remove("");

        camposOcultos.TryGetValue("__VIEWSTATE", out var viewState);
        camposOcultos.TryGetValue("__VIEWSTATEGENERATOR", out var viewStateGenerator);
        camposOcultos.TryGetValue("__EVENTVALIDATION", out var eventValidation);

        return new WebFormsState(
            viewState ?? "",
            viewStateGenerator ?? "",
            eventValidation ?? "",
            camposOcultos);
    }

    private static List<AdjuntoFila> ExtraerFilas(IDocument document)
    {
        var tabla = document.GetElementById(TablaAdjuntosId)
            ?? document.QuerySelector("table[id*='DWNL_grdId']")
            ?? document.QuerySelector("table[id*='grdId']")
            ?? document.QuerySelector("table[id*='DWNL']")
            ?? document.QuerySelector("table[id*='Adjunto']");
        if (tabla == null) return [];

        var filas = new List<AdjuntoFila>();
        var rows = tabla.QuerySelectorAll("tr");

        // Fila 0 es el header, igual que en adjuntos.js
        for (var i = 1; i < rows.Length; i++)
        {
            var cells = rows[i].QuerySelectorAll("td");
            if (cells.Length < 7) continue;

            var nombre = cells[1].TextContent?.Trim() ?? "";
            var tipo = cells[2].TextContent?.Trim() ?? "";
            var descripcion = cells[3].TextContent?.Trim() ?? "";
            var tamanio = cells[4].TextContent?.Trim() ?? "";
            var fecha = cells[5].TextContent?.Trim() ?? "";

            var boton = cells[6].QuerySelector("input[type=image]");
            var botonNombre = boton?.GetAttribute("name") ?? "";
            if (string.IsNullOrEmpty(botonNombre)) continue;

            var esActa = tipo.Contains("Acta de Evaluaci", StringComparison.OrdinalIgnoreCase)
                || (nombre.Contains("acta", StringComparison.OrdinalIgnoreCase)
                    && nombre.Contains("evaluaci", StringComparison.OrdinalIgnoreCase));

            filas.Add(new AdjuntoFila(nombre, tipo, descripcion, tamanio, fecha, botonNombre, esActa));
        }

        return filas;
    }
}
