using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace MPM.Modules.Analisis.Services;

/// <summary>
/// Post-proceso determinístico del resultado de análisis: contrasta los archivos
/// realmente enviados (documentos del workspace) contra lo que el acta/Gemini
/// declara como faltante u observado. Agrega (nunca elimina) inconsistencias que
/// el modelo no haya detectado y garantiza que la sección validacion_documental exista.
/// </summary>
public static class ValidacionDocumentalService
{
    // Verbos/frases con los que un acta declara un documento como no presentado
    private static readonly string[] FrasesFaltante =
    [
        "falt", "no presento", "no presentó", "no adjunto", "no adjuntó",
        "no envio", "no envió", "no acompaño", "no acompañó", "omitio", "omitió", "ausencia de"
    ];

    // Palabras genéricas que no identifican un documento
    private static readonly HashSet<string> StopWords =
    [
        "de", "del", "la", "las", "el", "los", "un", "una", "para", "por", "con",
        "documento", "documentos", "archivo", "archivos", "copia", "original"
    ];

    /// <summary>
    /// Aplica la validación cruzada sobre el JSON de análisis y retorna el JSON (posiblemente) modificado.
    /// <paramref name="archivosEnviados"/> son los nombres de los documentos subidos al workspace.
    /// </summary>
    public static string AplicarValidacion(string analisisJson, IReadOnlyCollection<string> archivosEnviados)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(analisisJson); }
        catch { return analisisJson; }
        if (root is not JsonObject rootObj) return analisisJson;

        var validacion = rootObj["validacion_documental"] as JsonObject;
        if (validacion == null)
        {
            validacion = new JsonObject
            {
                ["documentos"] = new JsonArray(),
                ["inconsistencias"] = new JsonArray(),
                ["resumen"] = "",
                ["coherente"] = true
            };
            rootObj["validacion_documental"] = validacion;
        }

        var documentos = validacion["documentos"] as JsonArray ?? new JsonArray();
        validacion["documentos"] = documentos.Parent == validacion ? documentos : documentos;
        var inconsistencias = validacion["inconsistencias"] as JsonArray ?? new JsonArray();
        if (inconsistencias.Parent != validacion) validacion["inconsistencias"] = inconsistencias;

        var sinInformacion = archivosEnviados.Count == 0;

        if (sinInformacion)
        {
            // FR-007: sin registro de envíos no se inventa nada
            foreach (var doc in documentos.OfType<JsonObject>())
            {
                doc["enviado"] = false;
                doc["estado"] = "sin_informacion";
            }
            validacion["resumen"] = "No hay registro de los documentos enviados para contrastar con el acta.";
            validacion["coherente"] = true;
            return rootObj.ToJsonString();
        }

        var archivosNormalizados = archivosEnviados
            .Select(a => (Original: a, Normalizado: Normalizar(a)))
            .ToList();

        // 1) Corregir documentos declarados como faltantes que sí fueron enviados
        foreach (var doc in documentos.OfType<JsonObject>())
        {
            var nombre = doc["nombre"]?.GetValue<string>() ?? "";
            var estado = doc["estado"]?.GetValue<string>() ?? "";
            var observado = doc["observado_en_acta"]?.GetValue<string>() ?? "";

            var declaradoFaltante = estado == "faltante" || IndicaFaltante(observado);
            if (!declaradoFaltante) continue;

            var match = BuscarArchivo(nombre, archivosNormalizados);
            if (match == null) continue;

            doc["enviado"] = true;
            doc["estado"] = "inconsistente";
            AgregarInconsistencia(inconsistencias, nombre,
                string.IsNullOrWhiteSpace(observado) ? "El acta lo declara faltante u observado" : observado,
                $"El documento '{match}' consta entre los archivos enviados", "alta");
        }

        // 2) Revisar los motivos textuales del análisis (debilidades, brechas, motivo de pérdida)
        foreach (var motivo in ExtraerMotivos(rootObj))
        {
            if (!IndicaFaltante(motivo)) continue;

            foreach (var (original, normalizado) in archivosNormalizados)
            {
                if (!ComparteTokenSignificativo(Normalizar(motivo), normalizado)) continue;
                AgregarInconsistencia(inconsistencias, original, motivo,
                    $"El documento '{original}' consta entre los archivos enviados", "alta");
            }
        }

        var hayAltas = inconsistencias.OfType<JsonObject>()
            .Any(i => (i["severidad"]?.GetValue<string>() ?? "") == "alta");

        validacion["coherente"] = !hayAltas;
        if (hayAltas)
        {
            var resumenActual = validacion["resumen"]?.GetValue<string>() ?? "";
            if (!resumenActual.Contains("inconsistencia", StringComparison.OrdinalIgnoreCase))
                validacion["resumen"] = ("Se detectaron inconsistencias entre lo declarado en el acta y los documentos efectivamente enviados. " + resumenActual).Trim();
        }

        return rootObj.ToJsonString();
    }

    private static void AgregarInconsistencia(JsonArray inconsistencias, string documento, string diceActa, string evidencia, string severidad)
    {
        var yaExiste = inconsistencias.OfType<JsonObject>().Any(i =>
            Normalizar(i["documento"]?.GetValue<string>() ?? "") == Normalizar(documento));
        if (yaExiste) return;

        inconsistencias.Add(new JsonObject
        {
            ["documento"] = documento,
            ["dice_acta"] = diceActa,
            ["evidencia"] = evidencia,
            ["severidad"] = severidad
        });
    }

    private static IEnumerable<string> ExtraerMotivos(JsonObject root)
    {
        if (root["analisis_tivit"] is JsonObject at)
        {
            if (at["debilidades"] is JsonArray debs)
                foreach (var d in debs)
                    if (d?.GetValue<string>() is string s) yield return s;

            if (at["brechas_identificadas"] is JsonArray brechas)
                foreach (var b in brechas.OfType<JsonObject>())
                    if (b["descripcion"]?.GetValue<string>() is string s) yield return s;
        }

        // Compatibilidad con esquemas anteriores que usan analisis_perdida
        if (root["analisis_perdida"] is JsonObject ap)
        {
            if (ap["motivo_principal"]?.GetValue<string>() is string mp) yield return mp;
            if (ap["factores"] is JsonArray factores)
                foreach (var f in factores)
                    if (f?.GetValue<string>() is string s) yield return s;
        }
    }

    private static bool IndicaFaltante(string texto)
    {
        var normalizado = Normalizar(texto);
        return FrasesFaltante.Any(f => normalizado.Contains(Normalizar(f)));
    }

    private static string? BuscarArchivo(string nombreDocumento, List<(string Original, string Normalizado)> archivos)
    {
        var docNorm = Normalizar(nombreDocumento);
        foreach (var (original, normalizado) in archivos)
        {
            if (ComparteTokenSignificativo(docNorm, normalizado))
                return original;
        }
        return null;
    }

    private static bool ComparteTokenSignificativo(string a, string b)
    {
        var tokensA = Tokenizar(a);
        var tokensB = Tokenizar(b);
        return tokensA.Overlaps(tokensB);
    }

    private static HashSet<string> Tokenizar(string texto) =>
        texto.Split([' ', '_', '-', '.', ',', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 4 && !StopWords.Contains(t))
            .ToHashSet();

    private static string Normalizar(string texto)
    {
        var descompuesto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }
}
