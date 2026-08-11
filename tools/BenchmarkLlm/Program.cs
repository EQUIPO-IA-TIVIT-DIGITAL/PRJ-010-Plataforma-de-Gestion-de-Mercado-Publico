using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPM.Modules.Analisis.Services;
using MPM.Shared.Services;

// ============================================================================
// Harness de benchmark de calidad de proveedores de IA (033-migracion-qwen-g4, US2).
// Compara la extracción JSON del análisis de PDFs entre Gemini (Vertex AI) y un
// proveedor OpenAI-compatible (Qwen 3.7 G4) usando EL MISMO prompt de producción.
// Emite un informe markdown con paridad campo a campo, latencia, JSON válido y
// veredicto go/no-go contra el umbral acordado (>= 90% de campos críticos idénticos).
//
// Uso:
//   dotnet run --project tools/BenchmarkLlm -- --docs <archivo-o-dir> \
//     --gemini-project <projectId> [--gemini-region us-central1] \
//     --qwen-endpoint http://localhost:8000/v1 --qwen-model qwen3.7-g4 [--qwen-key ...] \
//     [--max-docs 10] [--salida benchmark-qwen-g4.md]
//
// Requisitos: autenticación ADC de gcloud (camino Gemini) y servidor Qwen accesible
// (camino OpenAI-compatible). Ver README.md.
// ============================================================================

if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine("""
        Uso:
          dotnet run --project tools/BenchmarkLlm -- \
            --docs <archivo.txt-con-rutas-o-directorio-de-PDFs> \
            --gemini-project <projectId> \
            [--gemini-region us-central1] \
            --qwen-endpoint <base-url> --qwen-model <modelo> [--qwen-key <api-key>] \
            [--max-docs 10] [--salida benchmark-qwen-g4.md]
        """);
    return 1;
}

string Arg(string name, string fallback = "")
{
    var idx = Array.IndexOf(args, name);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : fallback;
}

var docsSource = Arg("--docs");
var geminiProject = Arg("--gemini-project");
var geminiRegion = Arg("--gemini-region", "us-central1");
var qwenEndpoint = Arg("--qwen-endpoint");
var qwenModel = Arg("--qwen-model");
var qwenKey = Arg("--qwen-key");
var maxDocs = int.TryParse(Arg("--max-docs", "10"), out var m) ? m : 10;
var salida = Arg("--salida", "benchmark-qwen-g4.md");

if (string.IsNullOrWhiteSpace(docsSource) || string.IsNullOrWhiteSpace(geminiProject) ||
    string.IsNullOrWhiteSpace(qwenEndpoint) || string.IsNullOrWhiteSpace(qwenModel))
{
    Console.Error.WriteLine("Faltan argumentos obligatorios (--docs, --gemini-project, --qwen-endpoint, --qwen-model).");
    return 1;
}

// ----------------------------------------------------------------------------
// 1. Cargar documentos (fuera del repo — nunca commitear PDFs reales).
// ----------------------------------------------------------------------------
var pdfs = new List<string>();
if (Directory.Exists(docsSource))
    pdfs = Directory.GetFiles(docsSource, "*.pdf", SearchOption.TopDirectoryOnly).OrderBy(f => f).ToList();
else if (File.Exists(docsSource) && Path.GetExtension(docsSource).Equals(".txt", StringComparison.OrdinalIgnoreCase))
    pdfs = File.ReadAllLines(docsSource).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
else if (File.Exists(docsSource) && Path.GetExtension(docsSource).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
    pdfs = [docsSource];

if (pdfs.Count == 0)
{
    Console.Error.WriteLine("No se encontraron PDFs en la fuente indicada.");
    return 1;
}
pdfs = pdfs.Take(maxDocs).ToList();
Console.WriteLine($"Benchmark con {pdfs.Count} documento(s) (max-docs={maxDocs}).");

// ----------------------------------------------------------------------------
// 2. Construir ambos clientes (mismo request neutral, mismo prompt de producción).
// ----------------------------------------------------------------------------
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["GOOGLE_CLOUD_PROJECT"] = geminiProject,
    ["Vertex:Region"] = geminiRegion,
    ["AI:Endpoint"] = qwenEndpoint,
    ["AI:Model"] = qwenModel,
    ["AI:ApiKey"] = qwenKey
}).Build();

using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
var geminiClient = new VertexGeminiClient(httpClient, config, new GoogleAdcTokenProvider(), Logger<VertexGeminiClient>("Gemini"));
geminiClient.ApplySettings(null, "gemini-2.5-pro");
var qwenClient = new OpenAiCompatClient(httpClient, config, Logger<OpenAiCompatClient>("Qwen"));
qwenClient.ApplySettings(qwenEndpoint, qwenModel);

// ----------------------------------------------------------------------------
// 3. Correr el benchmark.
// ----------------------------------------------------------------------------
var reportes = new List<DocumentoReporte>();
foreach (var pdf in pdfs)
{
    Console.WriteLine($"Analizando {Path.GetFileName(pdf)} ...");
    var pdfBytes = await File.ReadAllBytesAsync(pdf);
    var request = new LlmRequest(
        Messages: [new LlmMessage("user", [new LlmPdfPart(pdfBytes, Path.GetFileName(pdf), null), new LlmTextPart(GeminiService.GetAnalisisPrompt(1))])],
        Temperature: 0.2,
        MaxOutputTokens: VertexGeminiClient.DefaultMaxOutputTokens,
        JsonResponse: true);

    var (resultadoA, latenciaA) = await EjecutarConMedicionAsync(() => geminiClient.GenerarContenidoAsync(request));
    var (resultadoB, latenciaB) = await EjecutarConMedicionAsync(() => qwenClient.GenerarContenidoAsync(request));

    reportes.Add(DocumentoReporte.Crear(Path.GetFileName(pdf), resultadoA, latenciaA, resultadoB, latenciaB));
}

// ----------------------------------------------------------------------------
// 4. Informe.
// ----------------------------------------------------------------------------
var lineas = new List<string>
{
    "# Benchmark: Gemini 2.5 Pro vs Qwen 3.7 G4",
    "",
    $"**Fecha**: {DateTime.UtcNow:yyyy-MM-dd HH:mm 'UTC'}",
    $"**Documentos**: {reportes.Count} | **Umbral**: >= 90% de campos críticos idénticos (montos/criterios con prioridad de revisión)",
    "",
    "## Resumen",
    "",
    "| Métrica | Gemini (A) | Qwen (B) |",
    "|---------|-----------|----------|",
    $"| JSON válido | {reportes.Count(r => r.JsonValidoA)}/{reportes.Count} | {reportes.Count(r => r.JsonValidoB)}/{reportes.Count} |",
    $"| Truncado (finish=length) | {reportes.Count(r => r.TruncadoA)} | {reportes.Count(r => r.TruncadoB)} |",
    $"| Latencia p50 | {Percentil(reportes.Select(r => r.LatenciaA).ToList(), 0.5):F1}s | {Percentil(reportes.Select(r => r.LatenciaB).ToList(), 0.5):F1}s |",
    $"| Latencia p95 | {Percentil(reportes.Select(r => r.LatenciaA).ToList(), 0.95):F1}s | {Percentil(reportes.Select(r => r.LatenciaB).ToList(), 0.95):F1}s |",
    "",
    "## Paridad de campos críticos por documento",
    "",
    "| Documento | Iguales | Diferentes | Solo A | Solo B | Paridad |",
    "|-----------|---------|------------|--------|--------|---------|",
};

var totalIguales = 0; var totalComparables = 0;
foreach (var r in reportes)
{
    lineas.Add($"| {r.Nombre} | {r.Iguales} | {r.Diferentes} | {r.SoloA} | {r.SoloB} | {r.Paridad:P0} |");
    totalIguales += r.Iguales;
    totalComparables += r.Iguales + r.Diferentes + r.SoloA + r.SoloB;
}

var paridadGlobal = totalComparables > 0 ? (double)totalIguales / totalComparables : 0;
lineas.Add("");
lineas.Add($"**Paridad global de campos críticos: {paridadGlobal:P0}** ({totalIguales}/{totalComparables})");
lineas.Add("");
lineas.Add($"## Veredicto: {(paridadGlobal >= 0.9 ? "GO" : "NO-GO")} (umbral 90%)");
lineas.Add("");
lineas.Add("> El veredicto es automatizado; los montos y criterios discrepantes REQUIEREN revisión manual antes de decidir.");

var discrepancias = reportes
    .Where(r => r.Discrepancias.Count > 0)
    .SelectMany(r => r.Discrepancias.Select(d => (r.Nombre, d)));
if (discrepancias.Any())
{
    lineas.Add("");
    lineas.Add("## Discrepancias para revisión manual (montos/criterios primero)");
    lineas.Add("");
    lineas.Add("| Documento | Campo | Estado |");
    lineas.Add("|-----------|-------|--------|");
    foreach (var (doc, d) in discrepancias.OrderByDescending(x => EsCritico(x.d.Campo)))
        lineas.Add($"| {doc} | `{d.Campo}` | {d.Estado} |");
}

await File.WriteAllLinesAsync(salida, lineas);
Console.WriteLine($"Informe generado: {salida}");
return 0;

// ----------------------------------------------------------------------------
// Helpers.
// ----------------------------------------------------------------------------
static ILogger<T> Logger<T>(string name) =>
    LoggerFactory.Create(b => b.AddSimpleConsole(o => o.SingleLine = true)).CreateLogger<T>();

static async Task<(LlmResult Resultado, double Latencia)> EjecutarConMedicionAsync(Func<Task<LlmResult>> action)
{
    var sw = Stopwatch.StartNew();
    var resultado = await action();
    sw.Stop();
    return (resultado, sw.Elapsed.TotalSeconds);
}

static double Percentil(List<double> valores, double p)
{
    if (valores.Count == 0) return 0;
    var ordenados = valores.OrderBy(v => v).ToList();
    var idx = (int)Math.Ceiling(p * (ordenados.Count - 1));
    return ordenados[idx];
}

static bool EsCritico(string campo) =>
    campo.Contains("monto", StringComparison.OrdinalIgnoreCase) ||
    campo.Contains("criterio", StringComparison.OrdinalIgnoreCase) ||
    campo.Contains("puntaje", StringComparison.OrdinalIgnoreCase);

sealed class DocumentoReporte
{
    // Campos críticos del contrato de análisis (paths JSON): montos, fechas, puntuaciones y resultado.
    private static readonly string[] CamposCriticos =
    {
        "licitacion.id", "licitacion.nombre", "licitacion.organismo.nombre", "licitacion.tipo_licitacion",
        "licitacion.estado", "licitacion.monto_estimado", "licitacion.fechas.publicacion",
        "licitacion.fechas.cierre_ofertas", "licitacion.fechas.adjudicacion",
        "adjudicacion.adjudicatario.nombre", "adjudicacion.adjudicatario.monto_adjudicado",
        "adjudicacion.ofertantes.count", "evaluacion.criterios.count",
        "analisis_tivit.participa", "analisis_tivit.es_ganador", "analisis_tivit.monto_ofertado",
        "analisis_tivit.puntaje_obtenido", "analisis_tivit.resultado",
        "metricas_clave.diferencia_puntaje_total", "metricas_clave.diferencia_monto_ofertado",
        "validacion_documental.coherente", "revocacion.detectada"
    };

    public string Nombre { get; }
    public LlmResult ResultadoA { get; }
    public double LatenciaA { get; }
    public LlmResult ResultadoB { get; }
    public double LatenciaB { get; }
    public bool JsonValidoA { get; }
    public bool JsonValidoB { get; }
    public bool TruncadoA { get; }
    public bool TruncadoB { get; }
    public int Iguales { get; private set; }
    public int Diferentes { get; private set; }
    public int SoloA { get; private set; }
    public int SoloB { get; private set; }
    public double Paridad { get; private set; }
    public List<(string Campo, string Estado)> Discrepancias { get; } = [];

    private DocumentoReporte(string nombre, LlmResult a, double latA, LlmResult b, double latB)
    {
        Nombre = nombre; ResultadoA = a; LatenciaA = latA; ResultadoB = b; LatenciaB = latB;
        JsonValidoA = EsJsonValido(a.Text);
        JsonValidoB = EsJsonValido(b.Text);
        TruncadoA = a.FinishReason.Contains("length", StringComparison.OrdinalIgnoreCase);
        TruncadoB = b.FinishReason.Contains("length", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EsJsonValido(string text)
    {
        try { using var doc = JsonDocument.Parse(text); return true; }
        catch (JsonException) { return false; }
    }

    public static DocumentoReporte Crear(string nombre, LlmResult a, double latA, LlmResult b, double latB)
    {
        var r = new DocumentoReporte(nombre, a, latA, b, latB);
        r.Comparar();
        return r;
    }

    private void Comparar()
    {
        if (!JsonValidoA || !JsonValidoB)
        {
            Diferentes = CamposCriticos.Length;
            return;
        }

        using var docA = JsonDocument.Parse(ResultadoA.Text);
        using var docB = JsonDocument.Parse(ResultadoB.Text);

        foreach (var campo in CamposCriticos)
        {
            var valorA = ObtenerPorPath(docA.RootElement, campo);
            var valorB = ObtenerPorPath(docB.RootElement, campo);

            if (valorA == null && valorB == null) continue; // ambos ausentes: no comparable
            if (valorA == null) { SoloB++; Discrepancias.Add((campo, "solo en Qwen")); continue; }
            if (valorB == null) { SoloA++; Discrepancias.Add((campo, "solo en Gemini")); continue; }

            var nA = Normalizar(valorA.Value);
            var nB = Normalizar(valorB.Value);
            if (string.Equals(nA, nB, StringComparison.OrdinalIgnoreCase))
            {
                Iguales++;
            }
            else
            {
                Diferentes++;
                Discrepancias.Add((campo, $"Gemini={nA} | Qwen={nB}"));
            }
        }

        var comparables = Iguales + Diferentes + SoloA + SoloB;
        Paridad = comparables > 0 ? (double)Iguales / comparables : 0;
    }

    private static JsonElement? ObtenerPorPath(JsonElement root, string path)
    {
        var current = root;
        foreach (var segment in path.Split('.'))
        {
            if (segment == "count")
                return JsonDocument.Parse(current.GetArrayLength().ToString()).RootElement;

            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
                return null;
            current = next;
        }
        return current.Clone();
    }

    private static string Normalizar(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Number => e.GetDecimal().ToString("0.##########"),
        JsonValueKind.String => e.GetString()?.Trim() ?? "",
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        JsonValueKind.Array => $"[{e.GetArrayLength()}]",
        JsonValueKind.Object => "{obj}",
        _ => e.ToString()
    };
}
