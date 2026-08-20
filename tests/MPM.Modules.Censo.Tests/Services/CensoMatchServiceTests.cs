using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPM.Core.Data;
using MPM.Modules.Censo.Data;
using MPM.Modules.Censo.Models;
using MPM.Modules.Censo.Services;
using Xunit;

namespace MPM.Modules.Censo.Tests.Services;

/// <summary>
/// 036-flujo-comercial-ofertas (Fase 2, CEN-R001..R010 / D7.9-D7.12): contrato de CensoMatchService —
/// dedup por email/corporateId, cobertura con parciales válidas, bonus por país de ejecución con
/// filtro OFF (CEN-R009) y requisitos desde body/analisis. No toca Postgres ni Census reales:
/// mockea CensoHandler (virtual), CensusClient (virtual) y CensoExpansionService (virtual).
/// </summary>
public class CensoMatchServiceTests
{
    private readonly Mock<CensoHandler> _handlerMock;
    private readonly Mock<CensusClient> _censusMock;
    private readonly Mock<CensoExpansionService> _expansionMock;
    private readonly CensoMatchService _service;

    public CensoMatchServiceTests()
    {
        var dbFactory = new DbConnectionFactory("Host=localhost;Database=unused");
        _handlerMock = new Mock<CensoHandler>(dbFactory);
        _censusMock = new Mock<CensusClient>(
            new HttpClient(),
            new ConfigurationBuilder().Build(),
            new CensusTokenManager(),
            NullLogger<CensusClient>.Instance);
        _expansionMock = new Mock<CensoExpansionService>(
            _handlerMock.Object, null!, null!, NullLogger<CensoExpansionService>.Instance);

        // Pre-condiciones comunes: sin cache de personas y sin preferencias (defaults).
        _handlerMock.Setup(h => h.CachePersonasFrescoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<JsonElement>?)null);
        _handlerMock.Setup(h => h.PreferenciasObtenerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CensoPreferenciasDto?)null);

        _service = new CensoMatchService(
            _handlerMock.Object,
            _censusMock.Object,
            _expansionMock.Object,
            NullLogger<CensoMatchService>.Instance);
    }

    private void SetupExpansion(string concepto, string tecnologia)
        => _expansionMock.Setup(e => e.ExpandirAsync(concepto, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<string> { tecnologia }, "catalogo"));

    private void SetupPersonas(string tecnologia, params JsonElement[] personas)
        => _censusMock.Setup(c => c.GetUsersByTechnologyAsync(tecnologia, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(personas.ToList());

    [Fact]
    public async Task EjecutarMatchAsync_MismaPersonaEnDosTecnologias_DedupConCobertura2()
    {
        var request = new CensoMatchRequest { Tecnologias = new List<string> { "react", "angular" } };
        SetupExpansion("react", "React");
        SetupExpansion("angular", "Angular");
        SetupPersonas("React", Persona("p1@tivit.cl", "Persona Uno", "Chile", "React"));
        SetupPersonas("Angular", Persona("p1@tivit.cl", "Persona Uno", "Chile", "Angular"));

        var resultado = await _service.EjecutarMatchAsync(10, "usuario@tivit.cl", request);

        resultado.Personas.Should().HaveCount(1, "el dedup es por email: la misma persona aparece una sola vez (CEN-R001)");
        var persona = resultado.Personas[0];
        persona.Email.Should().Be("p1@tivit.cl");
        persona.Skills.Should().Equal("React", "Angular");
        persona.Cobertura.Should().Be(2);
        persona.TotalRequeridos.Should().Be(2);
        resultado.TecnologiasExpandidas.Should().Equal("React", "Angular");
        resultado.Consultas.Should().Be(2);
        resultado.Resumen.MaxCobertura.Should().Be(2);
        resultado.Resumen.PersonasConCoberturaAlta.Should().Be(1, "2/2 = 100 % ≥ 70 %");
        _censusMock.Verify(c => c.GetUsersByCertificationAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EjecutarMatchAsync_CoberturaParcial_DosDeTresRequeridos()
    {
        var request = new CensoMatchRequest { Tecnologias = new List<string> { "react", "angular", "vue" } };
        SetupExpansion("react", "React");
        SetupExpansion("angular", "Angular");
        SetupExpansion("vue", "Vue");
        SetupPersonas("React", Persona("p1@tivit.cl", "Persona Uno", "Chile", "React"));
        SetupPersonas("Angular", Persona("p1@tivit.cl", "Persona Uno", "Chile", "Angular"));
        SetupPersonas("Vue");

        var resultado = await _service.EjecutarMatchAsync(10, "usuario@tivit.cl", request);

        var persona = resultado.Personas.Single();
        persona.Cobertura.Should().Be(2);
        persona.TotalRequeridos.Should().Be(3);
        resultado.Resumen.MaxCobertura.Should().Be(2);
        resultado.Resumen.PersonasConCoberturaAlta.Should().Be(0, "2/3 = 66 % < 70 % (CEN-R001: la parcial se reporta igual)");
    }

    [Fact]
    public async Task EjecutarMatchAsync_FiltroPaisOff_PersonaDelPaisDeEjecucionRankeaArriba()
    {
        var request = new CensoMatchRequest { Tecnologias = new List<string> { "react" } };
        SetupExpansion("react", "React");
        SetupPersonas("React",
            Persona("arg@tivit.cl", "Persona Argentina", "Argentina", "React"),
            Persona("chile@tivit.cl", "Persona Chilena", "Chile", "React"));

        var resultado = await _service.EjecutarMatchAsync(10, "usuario@tivit.cl", request);

        resultado.Personas.Should().HaveCount(2);
        resultado.Personas[0].Email.Should().Be("chile@tivit.cl",
            "con filtro OFF el país de ejecución ('Chile' por defecto) da bonus de ranking sin excluir a nadie (CEN-R009)");
        resultado.Personas[1].Email.Should().Be("arg@tivit.cl");
        resultado.Personas[0].Cobertura.Should().Be(1, "el bonus no altera la cobertura mostrada");
        resultado.Personas[1].Cobertura.Should().Be(1);
    }

    [Fact]
    public async Task EjecutarMatchAsync_SinRequisitos_LanzaSinRequisitos()
    {
        _handlerMock.Setup(h => h.AnalisisRequisitosAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CensoHandler.AnalisisRequisitosResult(true, new List<string>(), new List<string>()));

        var request = new CensoMatchRequest { Tecnologias = new List<string>(), Certificaciones = new List<string>() };
        var act = async () => await _service.EjecutarMatchAsync(10, "usuario@tivit.cl", request);

        await act.Should().ThrowAsync<CensoMatchService.SinRequisitosException>();
        _censusMock.Verify(c => c.GetUsersByTechnologyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EjecutarMatchAsync_SinAnalisisNiBody_LanzaSinAnalisis()
    {
        _handlerMock.Setup(h => h.AnalisisRequisitosAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CensoHandler.AnalisisRequisitosResult(false, new List<string>(), new List<string>()));

        var act = async () => await _service.EjecutarMatchAsync(10, "usuario@tivit.cl", null);

        await act.Should().ThrowAsync<CensoMatchService.SinAnalisisException>(
            "sin body ni análisis comercial completado no hay requisitos de dónde sacar el match (CEN_004)");
    }

    /// <summary>Persona de Census en el shape real: userEmail, userName, workCountry, technologies[].name.</summary>
    private static JsonElement Persona(string email, string nombre, string pais, params string[] tecnologias)
    {
        var tecnologiasJson = string.Join(",", tecnologias.Select(t => $$"""{"name":"{{t}}"}"""));
        var json = $$"""{"userEmail":"{{email}}","corporateId":"CP-{{email}}","userName":"{{nombre}}","workCountry":"{{pais}}","functionFullName":"Ingeniero","technologies":[{{tecnologiasJson}}]}""";
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
