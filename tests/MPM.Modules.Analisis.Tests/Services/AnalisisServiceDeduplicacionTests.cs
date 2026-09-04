using FluentAssertions;
using Moq;
using MPM.Core.Data;
using MPM.Modules.Analisis.Data;
using MPM.Modules.Analisis.Models;
using MPM.Modules.Analisis.Services;
using MPM.Shared.Services;
using Xunit;

namespace MPM.Modules.Analisis.Tests.Services;

public class AnalisisServiceDeduplicacionTests
{
    [Fact]
    public async Task GetDashboardEjecutivoAsync_DeduplicaWorkspacesYResultadosRepetidos()
    {
        // Arrange
        var handlerMock = new Mock<AnalisisHandler>(new DbConnectionFactory("Host=unused"));
        
        var json1 = """
        {
            "licitacion": {
                "nombre": "Licitacion AWS Cloud",
                "fechas": { "adjudicacion": "2026-07-17" }
            },
            "analisis_tivit": {
                "es_ganador": true,
                "resultado": "Adjudicado",
                "puntaje_obtenido": 98.5,
                "puntaje_maximo_posible": 100.0,
                "monto_ofertado": 50000000
            },
            "adjudicacion": {
                "adjudicatario": {
                    "nombre": "TIVIT SpA",
                    "monto_adjudicado": 50000000
                }
            }
        }
        """;

        var json2 = """
        {
            "licitacion": {
                "nombre": "Licitacion Azure",
                "fechas": { "adjudicacion": "2026-07-18" }
            },
            "analisis_tivit": {
                "es_ganador": false,
                "resultado": "Perdido",
                "puntaje_obtenido": 80.0,
                "puntaje_maximo_posible": 100.0,
                "monto_ofertado": 40000000
            },
            "adjudicacion": {
                "adjudicatario": {
                    "nombre": "Competidor Cloud",
                    "monto_adjudicado": 38000000
                }
            }
        }
        """;

        // Simulamos que el workspace 80 (licitacion 100) viene 5 veces repetido
        var listaResultados = new List<ResultadoCompletoDto>
        {
            new() { WorkspaceId = 80, LicitacionId = 100, WorkspaceNombre = "Licitacion AWS Cloud", ContenidoJson = json1, CreadoEn = DateTime.UtcNow },
            new() { WorkspaceId = 80, LicitacionId = 100, WorkspaceNombre = "Licitacion AWS Cloud", ContenidoJson = json1, CreadoEn = DateTime.UtcNow.AddMinutes(-5) },
            new() { WorkspaceId = 80, LicitacionId = 100, WorkspaceNombre = "Licitacion AWS Cloud", ContenidoJson = json1, CreadoEn = DateTime.UtcNow.AddMinutes(-10) },
            new() { WorkspaceId = 81, LicitacionId = 200, WorkspaceNombre = "Licitacion Azure", ContenidoJson = json2, CreadoEn = DateTime.UtcNow }
        };

        handlerMock.Setup(h => h.ObtenerResultadosCompletosAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listaResultados);

        var service = new AnalisisService(
            handlerMock.Object,
            null!,
            Mock.Of<IStorageService>(),
            Mock.Of<IAnalisisBackgroundService>());

        // Act
        var (dashboard, error) = await service.GetDashboardEjecutivoAsync(null);

        // Assert
        error.Should().BeNull();
        dashboard.Should().NotBeNull();
        dashboard!.TotalAnalizadas.Should().Be(2, "Solo deben contarse 2 licitaciones únicas a pesar de que el workspace 80 viene 3 veces");
        dashboard.TotalGanadas.Should().Be(1);
        dashboard.TotalPerdidas.Should().Be(1);
        dashboard.Licitaciones.Should().HaveCount(2);
        dashboard.Licitaciones.Select(l => l.WorkspaceId).Should().BeEquivalentTo(new[] { 80L, 81L });
    }

    [Fact]
    public async Task GetDashboardEjecutivoAsync_CompetidorConNoAdjudicado_NoSeCuentaComoGanador()
    {
        var handlerMock = new Mock<AnalisisHandler>(new DbConnectionFactory("Host=unused"));

        var json = """
        {
            "licitacion": {
                "nombre": "Licitacion Telecomunicaciones",
                "fechas": { "adjudicacion": "2026-07-17" }
            },
            "analisis_tivit": {
                "es_ganador": false,
                "resultado": "No adjudicado"
            },
            "adjudicacion": {
                "adjudicatario": {
                    "nombre": "CLARO CHILE SPA",
                    "rut": "96.799.250-K",
                    "monto_adjudicado": 50000000
                },
                "ofertantes": [
                    {
                        "nombre": "CLARO CHILE SPA",
                        "rut": "96.799.250-K",
                        "resultado": "Adjudicado",
                        "monto_ofertado": 50000000
                    },
                    {
                        "nombre": "ENTEL CHILE S.A.",
                        "rut": "92.580.000-7",
                        "resultado": "No adjudicado",
                        "monto_ofertado": 49000000
                    }
                ]
            }
        }
        """;

        var listaResultados = new List<ResultadoCompletoDto>
        {
            new() { WorkspaceId = 1, LicitacionId = 1, WorkspaceNombre = "Licitacion Telecomunicaciones", ContenidoJson = json, CreadoEn = DateTime.UtcNow }
        };

        handlerMock.Setup(h => h.ObtenerResultadosCompletosAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listaResultados);

        var service = new AnalisisService(
            handlerMock.Object,
            null!,
            Mock.Of<IStorageService>(),
            Mock.Of<IAnalisisBackgroundService>());

        var (dashboard, error) = await service.GetDashboardEjecutivoAsync(null);

        error.Should().BeNull();
        dashboard.Should().NotBeNull();

        var claro = dashboard!.RankingCompetidores.FirstOrDefault(c => c.Nombre.Contains("CLARO"));
        claro.Should().NotBeNull();
        claro!.VecesGanador.Should().Be(1);
        claro.MontoTotalAdjudicado.Should().Be(50000000);

        var entel = dashboard.RankingCompetidores.FirstOrDefault(c => c.Nombre.Contains("ENTEL"));
        entel.Should().NotBeNull();
        entel!.VecesCompetidor.Should().Be(1);
        entel.VecesGanador.Should().Be(0, "ENTEL tenía resultado 'No adjudicado', no debe contarse como ganador");
        entel.MontoTotalAdjudicado.Should().Be(0);
        entel.Licitaciones[0].CompetidorGano.Should().BeFalse();
        entel.Licitaciones[0].ResultadoCompetidor.Should().Be("No adjudicado");
    }
}
