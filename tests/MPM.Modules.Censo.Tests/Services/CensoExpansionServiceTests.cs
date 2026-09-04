using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPM.Core.Data;
using MPM.Core.SystemConfig;
using MPM.Modules.Censo.Data;
using MPM.Modules.Censo.Models;
using MPM.Modules.Censo.Services;
using MPM.Shared.Services;
using Xunit;

namespace MPM.Modules.Censo.Tests.Services;

/// <summary>
/// 036-flujo-comercial-ofertas (Fase 2, CEN-R004 / D7.7-D7.8): contrato de CensoExpansionService —
/// Capa 1 fuzzy contra types (≥80), Capa 2 fuzzy contra tecnologías, Capa 3 IA fallback validada
/// contra el catálogo y persistida en censo_expansiones con fuente 'ia' (se paga 1 vez por concepto).
/// No llama a Census ni a un LLM real: mockea CensoCatalogoService.ListarAsync (virtual) y
/// LlmClientResolver.GetClientAsync (patrón de AnalisisComercialServiceTests).
/// </summary>
public class CensoExpansionServiceTests
{
    private readonly Mock<CensoHandler> _handlerMock;
    private readonly Mock<CensoCatalogoService> _catalogoMock;
    private readonly Mock<LlmClientResolver> _resolverMock;
    private readonly Mock<ILlmClient> _llmMock;
    private readonly CensoExpansionService _service;

    public CensoExpansionServiceTests()
    {
        var dbFactory = new DbConnectionFactory("Host=localhost;Database=unused");
        _handlerMock = new Mock<CensoHandler>(dbFactory);
        _catalogoMock = new Mock<CensoCatalogoService>(_handlerMock.Object, null!, NullLogger<CensoCatalogoService>.Instance);
        _resolverMock = new Mock<LlmClientResolver>(MockBehavior.Loose, null!, null!, null!);
        _llmMock = new Mock<ILlmClient>();
        _resolverMock.Setup(r => r.GetClientAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_llmMock.Object);

        _service = new CensoExpansionService(
            _handlerMock.Object,
            _catalogoMock.Object,
            _resolverMock.Object,
            NullLogger<CensoExpansionService>.Instance);
    }

    private static List<CensoCatalogoItemDto> CatalogoBase() => new()
    {
        new() { Grupo = "Lenguajes", Categoria = "Backend", TypeName = "Lenguajes", Tecnologia = "Python" },
        new() { Grupo = "Lenguajes", Categoria = "Backend", TypeName = "Lenguajes", Tecnologia = "Java" },
        new() { Grupo = "Web", Categoria = "Frontend", TypeName = "Front-End", Tecnologia = "React" },
        new() { Grupo = "Web", Categoria = "Frontend", TypeName = "Front-End", Tecnologia = "Angular" },
        new() { Grupo = "Web", Categoria = "Frontend", TypeName = "Front-End", Tecnologia = "Vue" },
        new() { Grupo = "Web", Categoria = "Frontend", TypeName = "Front-End", Tecnologia = "Svelte" },
        new() { Grupo = "Web", Categoria = "Frontend", TypeName = "Front-End", Tecnologia = "Next.js" },
    };

    [Fact]
    public async Task ExpandirAsync_ConceptoCoincideConType_Capa1DevuelveTecnologiasDelType()
    {
        _handlerMock.Setup(h => h.ExpansionObtenerAsync("frontend", It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string>?)null);
        _catalogoMock.Setup(c => c.ListarAsync(null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CensoCatalogoListadoDto { Items = CatalogoBase() });

        var (tecnologias, fuente) = await _service.ExpandirAsync("frontend");

        tecnologias.Should().Equal(new[] { "React", "Angular", "Vue", "Svelte" },
            "el type 'Front-End' agrupa las tecnologías del frontend y se acotan a 4 (Capa 1)");
        fuente.Should().Be("catalogo");
        _handlerMock.Verify(h => h.ExpansionUpsertAsync("frontend",
            It.Is<List<string>>(l => l.Count == 4 && l[0] == "React"), "catalogo", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExpandirAsync_ConceptoEsTecnologiaExacta_Capa2DevuelveLaTecnologia()
    {
        _handlerMock.Setup(h => h.ExpansionObtenerAsync("python", It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string>?)null);
        _catalogoMock.Setup(c => c.ListarAsync(null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CensoCatalogoListadoDto { Items = CatalogoBase() });

        var (tecnologias, fuente) = await _service.ExpandirAsync("Python");

        tecnologias.Should().Equal("Python");
        fuente.Should().Be("catalogo");
        _handlerMock.Verify(h => h.ExpansionUpsertAsync("python",
            It.Is<List<string>>(l => l.Count == 1 && l[0] == "Python"), "catalogo", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExpandirAsync_SinMatchEnCatalogo_Capa3IaGuardaConFuenteIa()
    {
        _handlerMock.Setup(h => h.ExpansionObtenerAsync("industria 4.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string>?)null);
        _catalogoMock.Setup(c => c.ListarAsync(null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CensoCatalogoListadoDto { Items = CatalogoBase() });
        _llmMock.Setup(c => c.GenerarContenidoAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResult("""{"tecnologias":["react","python"]}""", "{}", new LlmUsage(10, 5, 15), "STOP"));

        var (tecnologias, fuente) = await _service.ExpandirAsync("industria 4.0");

        tecnologias.Should().Equal(new[] { "React", "Python" },
            "el LLM propone y el catálogo valida (≥80) tomando el nombre canónico (Capa 3)");
        fuente.Should().Be("ia");
        _handlerMock.Verify(h => h.ExpansionUpsertAsync("industria 4.0",
            It.Is<List<string>>(l => l.Count == 2), "ia", It.IsAny<CancellationToken>()), Times.Once,
            "la expansión IA se persiste en censo_expansiones con fuente 'ia'");
    }

    [Fact]
    public async Task ExpandirAsync_SegundaLlamadaAlMismoConcepto_UsaCacheSinIA()
    {
        var cacheado = new List<string> { "React", "Python" };
        _handlerMock.SetupSequence(h => h.ExpansionObtenerAsync("industria 4.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string>?)null)
            .ReturnsAsync(cacheado);
        _catalogoMock.Setup(c => c.ListarAsync(null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CensoCatalogoListadoDto { Items = CatalogoBase() });
        _llmMock.Setup(c => c.GenerarContenidoAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResult("""{"tecnologias":["react","python"]}""", "{}", new LlmUsage(), "STOP"));

        var primera = await _service.ExpandirAsync("industria 4.0");
        var segunda = await _service.ExpandirAsync("industria 4.0");

        primera.Fuente.Should().Be("ia");
        segunda.Tecnologias.Should().Equal("React", "Python");
        _resolverMock.Verify(r => r.GetClientAsync(It.IsAny<CancellationToken>()), Times.Once,
            "la expansión IA se paga 1 vez por concepto (CEN-R004): la segunda llamada usa el cache");
        _handlerMock.Verify(h => h.ExpansionUpsertAsync(It.IsAny<string>(), It.IsAny<List<string>>(), "ia", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Helpers internos (expuestos vía InternalsVisibleTo) ─────────────────────────

    [Theory]
    [InlineData("""{"tecnologias":["React","Python"]}""", 2)]
    [InlineData("""["React"]""", 1)]
    [InlineData("no es json", 0)]
    [InlineData("", 0)]
    [InlineData("""{"tecnologias":[]}""", 0)]
    public void ParseTecnologias_ExtraeNombresDeRespuestasJson(string texto, int esperados)
        => CensoExpansionService.ParseTecnologias(texto).Should().HaveCount(esperados);

    [Fact]
    public void TokenSetRatio_FrontendVsFrontEnd_SuperaUmbralFuzzy()
        => CensoExpansionService.TokenSetRatio("frontend", "front-end").Should().BeGreaterThanOrEqualTo(80);

    [Fact]
    public void Normalizar_MinusculasYSinAcentos()
        => CensoExpansionService.Normalizar("Front-End Ágil").Should().Be("front-end agil");
}
