using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MPM.Modules.Analisis.Services;

/// <summary>
/// 029-fix-hallazgos-code-review-competidores-alertas (FR-017/US13, QA BUG-007): los campos
/// numéricos de monto ya usan un código de moneda restringido (CLP/USD/UF/EUR/NO_DETERMINADA,
/// ver GetAnalisisPrompt) y se formatean de forma consistente en el frontend vía formatMoney
/// (Intl.NumberFormat -- siempre "US$", nunca "DÓLAR AMERICANO"). Pero los campos de texto libre
/// (resumen, debilidades, brechas, motivo de inadmisibilidad) no tienen esa restricción -- Gemini
/// puede escribir el nombre de la moneda en prosa de forma inconsistente con el símbolo mostrado
/// junto al monto. Este post-proceso normaliza esas menciones a la misma sigla que usa formatMoney,
/// para que un mismo dato no aparezca con dos representaciones distintas en el mismo dashboard.
/// </summary>
public static class MonedaNormalizerService
{
    // Frases multi-palabra específicas -- evita coincidir con nombres de empresas/documentos que
    // contengan una palabra suelta como "euros" o "dólar".
    private static readonly (Regex Patron, string Reemplazo)[] Patrones =
    [
        (new Regex(@"d[oó]lar(?:es)?\s+(?:americanos?|de\s+estados\s+unidos|estadounidenses?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "USD"),
        (new Regex(@"pesos?\s+chilenos?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "CLP"),
        (new Regex(@"unidad(?:es)?\s+de\s+fomento", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "UF"),
        (new Regex(@"euros?\s+(?:europeos?|de\s+la\s+uni[oó]n\s+europea)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "EUR"),
    ];

    // Campos de texto libre conocidos donde Gemini puede narrar montos -- no se recorre el JSON
    // completo para no tocar nombres de proveedores/organismos/documentos que puedan contener
    // coincidencias parciales por accidente.
    private static readonly string[] RutasTexto =
    [
        "validacion_documental.resumen",
    ];

    private static readonly string[] RutasArregloTexto =
    [
        "analisis_tivit.fortalezas",
        "analisis_tivit.debilidades",
    ];

    public static string Normalizar(string analisisJson)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(analisisJson); }
        catch { return analisisJson; }
        if (root is not JsonObject rootObj) return analisisJson;

        foreach (var ruta in RutasTexto)
            NormalizarCampoTextoPorRuta(rootObj, ruta);

        foreach (var ruta in RutasArregloTexto)
            NormalizarArregloTexto(rootObj, ruta);

        if (rootObj["adjudicacion"]?["ofertantes"] is JsonArray ofertantes)
            foreach (var of in ofertantes.OfType<JsonObject>())
                NormalizarCampoTexto(of, "motivo_inadmisibilidad");

        if (rootObj["analisis_tivit"]?["brechas_identificadas"] is JsonArray brechas)
            foreach (var b in brechas.OfType<JsonObject>())
            {
                NormalizarCampoTexto(b, "descripcion");
                NormalizarCampoTexto(b, "recomendacion_mejora");
            }

        return rootObj.ToJsonString();
    }

    private static void NormalizarCampoTexto(JsonObject obj, string campo)
    {
        if (obj[campo]?.GetValue<string>() is string s)
            obj[campo] = NormalizarTexto(s);
    }

    private static void NormalizarCampoTextoPorRuta(JsonObject rootObj, string rutaPunteada)
    {
        var partes = rutaPunteada.Split('.');
        JsonObject? actual = rootObj;
        for (var i = 0; i < partes.Length - 1 && actual != null; i++)
            actual = actual[partes[i]] as JsonObject;
        if (actual == null) return;
        NormalizarCampoTexto(actual, partes[^1]);
    }

    private static void NormalizarArregloTexto(JsonObject rootObj, string rutaPunteada)
    {
        var partes = rutaPunteada.Split('.');
        JsonObject? actual = rootObj;
        for (var i = 0; i < partes.Length - 1 && actual != null; i++)
            actual = actual[partes[i]] as JsonObject;
        if (actual?[partes[^1]] is not JsonArray arr) return;

        for (var i = 0; i < arr.Count; i++)
            if (arr[i]?.GetValue<string>() is string s)
                arr[i] = NormalizarTexto(s);
    }

    private static string NormalizarTexto(string texto)
    {
        foreach (var (patron, reemplazo) in Patrones)
            texto = patron.Replace(texto, reemplazo);
        return texto;
    }
}
