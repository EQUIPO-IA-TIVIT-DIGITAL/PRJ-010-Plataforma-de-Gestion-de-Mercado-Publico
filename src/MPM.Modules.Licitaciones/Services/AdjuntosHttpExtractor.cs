using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPM.Modules.Licitaciones.Data;
using MPM.Modules.Licitaciones.Models;
using MPM.Shared.Services;

namespace MPM.Modules.Licitaciones.Services;

/// <summary>Se lanza cuando el portal responde 401/403 — señal para que el orquestador renueve la sesión.</summary>
public class SesionExpiradaException(string mensaje) : Exception(mensaje);

/// <summary>
/// Descarga adjuntos de una licitación replicando por HTTP el postback WebForms que hoy
/// hace un navegador real (ver <see cref="WebFormsParser"/>).
///
/// Flujo validado contra el portal real el 2026-07-06 (ver
/// <c>tools/scraper-mp/spike-validacion-http-pura.js</c> y contracts/internal-api.md de
/// 016-extraccion-documentos-api):
/// 1. GET a la ficha pública por código (<c>DetailsAcquisition.aspx?idlicitacion={codigo}</c>)
///    — funciona sin el token <c>qs</c> opaco que usa la búsqueda del portal.
/// 2. Extraer el token <c>enc</c> del onclick de <c>#imgAdjuntos</c> en el HTML de la ficha.
/// 3. GET a <c>Attachment/ViewAttachment.aspx?enc={token}</c>.
///
/// ⚠️ HALLAZGO CRÍTICO (2026-07-06): el paso 3 está protegido por **Google reCAPTCHA
/// Enterprise** ejecutado client-side (`grecaptcha.enterprise.execute` + POST a
/// `ViewAttachment.aspx?ajax=1` con el token, que si pasa redirige vía JS a
/// `ViewAttachmentLC.aspx` con el listado real). Un `HttpClient` sin motor JS **no puede
/// resolver ese challenge** — recibe la página del challenge, no el listado. Este método
/// implementa fielmente el resto del flujo (está listo para cuando exista una forma de
/// pasar el challenge), pero **hoy falla de forma controlada en este paso específico** y
/// cae al fallback de `DocumentExtractionService` (navegador). Esto significa que 016 NO
/// elimina el uso de Chromium para la descarga de adjuntos como se esperaba — ver
/// research.md de 002-fase5-deploy-gcp para el impacto en la migración a Cloud Run.
/// </summary>
public class AdjuntosHttpExtractor(
    ILogger<AdjuntosHttpExtractor> logger,
    IConfiguration config,
    MpSessionProvider sessionProvider,
    WebFormsParser parser,
    IStorageService storageService,
    ExtraccionLogHandler extraccionLogHandler)
{
    // Confirmados por spike contra el portal real (tools/scraper-mp/spike-ficha-directa.js, 2026-07-06).
    private const string FichaUrlTemplate =
        "https://www.mercadopublico.cl/Procurement/Modules/RFB/DetailsAcquisition.aspx?idlicitacion={codigo}";
    private const string AttachmentBaseUrl =
        "https://www.mercadopublico.cl/Procurement/Modules/Attachment/ViewAttachment.aspx?enc=";

    private static readonly Regex ImgAdjuntosOnclickRegex = new(
        @"id=[""']imgAdjuntos[""'][^>]*onclick=[""']([^""']+)[""']|onclick=[""']([^""']*OpenGlobalPopup[^""']*)[""'][^>]*id=[""']imgAdjuntos[""']|open\('(\.\./Attachment/ViewAttachment\.aspx\?enc=[^']+)'",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<ResultadoExtraccion> ExtraerAsync(LicitacionRef lic, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var urlTemplate = config["Extraccion:UrlFichaTemplate"] ?? FichaUrlTemplate;

        var sesion = await sessionProvider.ObtenerSesionAsync(ct: ct);

        try
        {
            return await ExtraerConSesionAsync(lic, urlTemplate, sesion, sw, ct);
        }
        catch (SesionExpiradaException)
        {
            logger.LogInformation("Sesión expirada para licitación {Codigo}, renovando y reintentando una vez", lic.CodigoExterno);
            var nuevaSesion = await sessionProvider.ObtenerSesionAsync(forzarRenovacion: true, ct: ct);
            return await ExtraerConSesionAsync(lic, urlTemplate, nuevaSesion, sw, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo extrayendo adjuntos (directo) para licitación {Codigo}", lic.CodigoExterno);
            return new ResultadoExtraccion("directo", "fallo", 0, false, ex.Message, sw.ElapsedMilliseconds, false);
        }
    }

    private async Task<ResultadoExtraccion> ExtraerConSesionAsync(
        LicitacionRef lic, string fichaUrlTemplate, MpSession sesion, Stopwatch sw, CancellationToken ct)
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = sesion.Cookies,
            UseCookies = true,
            AllowAutoRedirect = true,
        };
        using var client = new HttpClient(handler);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");

        // Paso 1: ficha pública por código
        var fichaUrl = fichaUrlTemplate.Replace("{codigo}", Uri.EscapeDataString(lic.CodigoExterno));
        var fichaResponse = await client.GetAsync(fichaUrl, ct);
        SignalarSiSesionExpirada(fichaResponse);
        fichaResponse.EnsureSuccessStatusCode();
        var fichaHtml = await fichaResponse.Content.ReadAsStringAsync(ct);

        // Paso 2: extraer el token enc del onclick de #imgAdjuntos
        var encToken = ExtraerEncToken(fichaHtml);
        if (encToken == null)
            return new ResultadoExtraccion("directo", "sin_adjuntos", 0, false, "No se encontró #imgAdjuntos en la ficha (licitación sin adjuntos o estructura de página cambió)", sw.ElapsedMilliseconds, false);

        // Paso 3: GET al listado real de adjuntos (con el enc token)
        var listadoUrl = AttachmentBaseUrl + encToken;
        var getResponse = await client.GetAsync(listadoUrl, ct);
        SignalarSiSesionExpirada(getResponse);
        getResponse.EnsureSuccessStatusCode();
        listadoUrl = getResponse.RequestMessage?.RequestUri?.ToString() ?? listadoUrl; // sigue el redirect a ViewAttachmentLC.aspx

        var html = await getResponse.Content.ReadAsStringAsync(ct);

        if (html.Contains("grecaptcha.enterprise", StringComparison.OrdinalIgnoreCase))
        {
            // Ver comentario de clase: este paso está gateado por reCAPTCHA Enterprise
            // client-side, irresoluble por un HttpClient sin motor JS (hallazgo 2026-07-06).
            return new ResultadoExtraccion(
                "directo", "fallo", 0, false,
                "Bloqueado por reCAPTCHA Enterprise en ViewAttachment.aspx — requiere navegador real, no resoluble por HTTP puro",
                sw.ElapsedMilliseconds, false);
        }

        var listado = await parser.ParseAsync(html);

        if (listado.Filas.Count == 0)
            return new ResultadoExtraccion("directo", "sin_adjuntos", 0, false, null, sw.ElapsedMilliseconds, false);

        var descargados = 0;
        var actaObtenida = false;
        string? primerError = null;

        foreach (var fila in listado.Filas)
        {
            try
            {
                var body = new List<KeyValuePair<string, string>>(listado.State.TodosLosCamposOcultos)
                {
                    new($"{fila.BotonNombrePostback}.x", "1"),
                    new($"{fila.BotonNombrePostback}.y", "1"),
                };

                using var content = new FormUrlEncodedContent(body);
                client.DefaultRequestHeaders.Referrer = new Uri(listadoUrl);

                var postResponse = await client.PostAsync(listadoUrl, content, ct);
                SignalarSiSesionExpirada(postResponse);
                postResponse.EnsureSuccessStatusCode();

                var fileName = ObtenerNombreArchivo(postResponse, fila.Nombre);
                var mimeType = postResponse.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

                await using var stream = await postResponse.Content.ReadAsStreamAsync(ct);
                var rutaStorage = await storageService.UploadAsync(
                    $"licitaciones/{lic.CodigoExterno}/adjuntos", fileName, stream, mimeType, ct);

                await extraccionLogHandler.RegistrarAdjuntoDirectoAsync(
                    lic.LicitacionId, fila.Tipo, fileName, rutaStorage,
                    postResponse.Content.Headers.ContentLength, mimeType, fila.EsActa, ct);

                descargados++;
                if (fila.EsActa) actaObtenida = true;
            }
            catch (Exception ex) when (ex is not SesionExpiradaException)
            {
                primerError ??= ex.Message;
                logger.LogWarning(ex, "No se pudo descargar el adjunto '{Nombre}' de la licitación {Codigo}", fila.Nombre, lic.CodigoExterno);
            }
        }

        var estado = descargados > 0 ? "exito" : "fallo";
        return new ResultadoExtraccion("directo", estado, descargados, actaObtenida, descargados == 0 ? primerError : null, sw.ElapsedMilliseconds, false);
    }

    /// <summary>
    /// Extrae el token <c>enc</c> del atributo <c>onclick</c> de <c>#imgAdjuntos</c> en el
    /// HTML de la ficha. El onclick observado en el spike tiene la forma:
    /// <c>open('../Attachment/ViewAttachment.aspx?enc=TOKEN', 'MercadoPublico', ...)</c>,
    /// con el HTML entity-encoded (comillas como <c>&amp;#39;</c>).
    /// </summary>
    private static string? ExtraerEncToken(string html)
    {
        var decoded = WebUtility.HtmlDecode(html);
        var match = ImgAdjuntosOnclickRegex.Match(decoded);
        if (!match.Success) return null;

        var onclickTexto = match.Groups.Cast<Group>().Skip(1).FirstOrDefault(g => g.Success)?.Value;
        if (string.IsNullOrEmpty(onclickTexto)) return null;

        var encMatch = Regex.Match(onclickTexto, @"[?&]enc=([^'""&]+)");
        return encMatch.Success ? encMatch.Groups[1].Value : null;
    }

    private static void SignalarSiSesionExpirada(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new SesionExpiradaException($"El portal respondió {(int)response.StatusCode}");
    }

    private static string ObtenerNombreArchivo(HttpResponseMessage response, string fallback)
    {
        var disposition = response.Content.Headers.ContentDisposition;
        if (disposition?.FileNameStar is { } fileNameStar) return fileNameStar.Trim('"');
        if (disposition?.FileName is { } fileName) return fileName.Trim('"');
        return string.IsNullOrWhiteSpace(fallback) ? $"adjunto-{Guid.NewGuid():N}.pdf" : $"{fallback}.pdf";
    }
}
