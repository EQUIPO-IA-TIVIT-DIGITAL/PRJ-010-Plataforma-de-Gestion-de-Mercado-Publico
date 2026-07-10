using System.Text.Json;
using FluentAssertions;
using MPM.Modules.Competidores.Data;
using MPM.Modules.Competidores.Models;
using MPM.Modules.Competidores.Services;
using Xunit;

namespace MPM.Modules.Competidores.Tests.Services;

// FR-004/FR-005/FR-006: nunca se dispara Gemini sin confirmación explícita, y una consulta
// idéntica (mismo competidor+rango) reutiliza el análisis ya guardado. Estos tests verifican el
// contrato de CompetidorAnalysisService inspeccionando su código fuente (source-guard), ya que
// este proyecto no monta una conexión Postgres real ni un cliente Gemini real -- mismo patrón
// usado en 023-fix-bugs-produccion para AlertasDestinatariosTelegramFixTests.
public class CompetidorAnalysisServiceTests
{
    [Fact]
    public void ObtenerOGenerarAnalisisAsync_SiNoConfirmaYNoHayCache_NuncaLlamaAGeminiService()
    {
        var source = File.ReadAllText(FindSourceFile("CompetidorAnalysisService.cs")).Replace("\r\n", "\n");

        var inicioMetodo = source.IndexOf("public async Task<AnalisisCompetidorResponse> ObtenerOGenerarAnalisisAsync(", StringComparison.Ordinal);
        inicioMetodo.Should().BeGreaterThanOrEqualTo(0);

        var indiceConfirmarCheck = source.IndexOf("if (!request.Confirmar)", inicioMetodo, StringComparison.Ordinal);
        indiceConfirmarCheck.Should().BeGreaterThan(inicioMetodo,
            "debe existir un chequeo explícito de request.Confirmar antes de cualquier llamada a Gemini (FR-004)");

        var indicePrimeraLlamadaGemini = source.IndexOf("geminiService.AnalizarCompetidorAsync", inicioMetodo, StringComparison.Ordinal);
        indicePrimeraLlamadaGemini.Should().BeGreaterThan(indiceConfirmarCheck,
            "la llamada a Gemini debe ocurrir DESPUÉS del chequeo de Confirmar, nunca antes (FR-004) -- " +
            "sin este orden, se podría gastar tokens de Gemini sin que el usuario lo haya pedido explícitamente");
    }

    [Fact]
    public void ObtenerOGenerarAnalisisAsync_PrimeroConsultaCacheAntesDeContarODeGenerar()
    {
        var source = File.ReadAllText(FindSourceFile("CompetidorAnalysisService.cs")).Replace("\r\n", "\n");

        var inicioMetodo = source.IndexOf("public async Task<AnalisisCompetidorResponse> ObtenerOGenerarAnalisisAsync(", StringComparison.Ordinal);
        var indiceBuscarCacheado = source.IndexOf("analisisHandler.BuscarCacheadoAsync", inicioMetodo, StringComparison.Ordinal);
        var indiceContar = source.IndexOf("ofertasHandler.ContarPorCompetidorYRangoAsync", inicioMetodo, StringComparison.Ordinal);

        indiceBuscarCacheado.Should().BeGreaterThan(inicioMetodo);
        indiceContar.Should().BeGreaterThan(indiceBuscarCacheado,
            "debe consultarse el caché ANTES de contar/generar -- si ya existe un análisis para " +
            "el mismo competidor+rango exacto, se reutiliza sin generar uno nuevo (FR-005)");
    }

    [Fact]
    public void CompetidoresStoredProcedures_AnalisisGuardar_CasteaContenidoAJsonb()
    {
        CompetidoresStoredProcedures.AnalisisGuardar.Should().Contain("@p_contenido_json::jsonb",
            "sin el cast explícito se puede repetir el mismo bug de QA BUG-014 (Postgres no resuelve " +
            "la sobrecarga de la función por un parámetro de tipo ambiguo)");
    }

    [Fact]
    public void UspListarCompetidores_ExcluyeTivitDelListado()
    {
        var source = File.ReadAllText(FindSourceFile("V100__Create_usp_ListarCompetidores.sql")).Replace("\r\n", "\n");

        source.Should().Contain("NOT ILIKE '%tivit%'",
            "el dropdown de competidores no debe ofrecer a TIVIT como opción -- no tiene sentido comparar a TIVIT contra sí mismo");
    }

    [Fact]
    public void CompetidoresController_ExponeEndpointListaAntesQueBuscarPorNombre()
    {
        var source = File.ReadAllText(FindSourceFile("CompetidoresController.cs")).Replace("\r\n", "\n");

        var indiceLista = source.IndexOf("[HttpGet(\"lista\")]", StringComparison.Ordinal);
        var indiceBuscarPorNombre = source.IndexOf("public async Task<ActionResult<ApiResponse<IEnumerable<OfertaDto>>>> BuscarPorNombre", StringComparison.Ordinal);

        indiceLista.Should().BeGreaterThanOrEqualTo(0, "debe existir un endpoint GET /lista para poblar el dropdown del frontend");
        indiceLista.Should().BeLessThan(indiceBuscarPorNombre,
            "la ruta explícita \"lista\" debe declararse antes que la ruta raíz para evitar ambigüedad de routing");
    }

    private static string FindSourceFile(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MPM.sln")))
            dir = dir.Parent;

        if (dir == null) throw new FileNotFoundException("No se encontró MPM.sln subiendo desde el directorio de test.");

        return Directory.GetFiles(dir.FullName, fileName, SearchOption.AllDirectories)
            .Single(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
    }
}
