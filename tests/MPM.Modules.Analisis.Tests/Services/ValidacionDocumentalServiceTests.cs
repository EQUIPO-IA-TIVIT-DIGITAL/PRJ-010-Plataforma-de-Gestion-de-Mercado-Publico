using System.Text.Json;
using FluentAssertions;
using MPM.Modules.Analisis.Services;
using Xunit;

namespace MPM.Modules.Analisis.Tests.Services;

public class ValidacionDocumentalServiceTests
{
    private static JsonElement Validacion(string json) =>
        JsonDocument.Parse(json).RootElement.GetProperty("validacion_documental");

    [Fact]
    public void DetectaInconsistencia_CuandoActaDeclaraFaltanteUnDocumentoEnviado()
    {
        // El caso reportado por el cliente: el acta dice que faltó la garantía, pero sí se envió
        var analisis = """
        {
          "analisis_tivit": {
            "debilidades": ["No presentó garantía de seriedad de la oferta"]
          },
          "validacion_documental": {
            "documentos": [
              { "nombre": "Garantía de seriedad", "requerido": true, "enviado": false, "observado_en_acta": "faltante", "estado": "faltante" }
            ],
            "inconsistencias": [],
            "resumen": "",
            "coherente": true
          }
        }
        """;
        var archivos = new[] { "Garantia_Seriedad_Oferta_TIVIT.pdf", "Anexo_3_Oferta_Economica.pdf" };

        var resultado = ValidacionDocumentalService.AplicarValidacion(analisis, archivos);

        var validacion = Validacion(resultado);
        validacion.GetProperty("coherente").GetBoolean().Should().BeFalse();
        validacion.GetProperty("inconsistencias").GetArrayLength().Should().BeGreaterThan(0);

        var doc = validacion.GetProperty("documentos")[0];
        doc.GetProperty("estado").GetString().Should().Be("inconsistente");
        doc.GetProperty("enviado").GetBoolean().Should().BeTrue();

        var inconsistencia = validacion.GetProperty("inconsistencias")[0];
        inconsistencia.GetProperty("severidad").GetString().Should().Be("alta");
    }

    [Fact]
    public void NoGeneraFalsasAlarmas_CuandoAnalisisEsCoherente()
    {
        var analisis = """
        {
          "analisis_tivit": {
            "debilidades": ["Menor puntaje en experiencia del equipo propuesto"]
          },
          "validacion_documental": {
            "documentos": [
              { "nombre": "Anexo económico", "requerido": true, "enviado": true, "observado_en_acta": "conforme", "estado": "ok" }
            ],
            "inconsistencias": [],
            "resumen": "Documentación coherente",
            "coherente": true
          }
        }
        """;
        var archivos = new[] { "Anexo_Economico.pdf" };

        var resultado = ValidacionDocumentalService.AplicarValidacion(analisis, archivos);

        var validacion = Validacion(resultado);
        validacion.GetProperty("coherente").GetBoolean().Should().BeTrue();
        validacion.GetProperty("inconsistencias").GetArrayLength().Should().Be(0);
        validacion.GetProperty("documentos")[0].GetProperty("estado").GetString().Should().Be("ok");
    }

    [Fact]
    public void MarcaSinInformacion_CuandoNoHayRegistroDeEnvios()
    {
        var analisis = """
        {
          "validacion_documental": {
            "documentos": [
              { "nombre": "Garantía de seriedad", "requerido": true, "enviado": false, "observado_en_acta": "faltante", "estado": "faltante" }
            ],
            "inconsistencias": [],
            "resumen": "",
            "coherente": true
          }
        }
        """;

        var resultado = ValidacionDocumentalService.AplicarValidacion(analisis, Array.Empty<string>());

        var validacion = Validacion(resultado);
        validacion.GetProperty("documentos")[0].GetProperty("estado").GetString().Should().Be("sin_informacion");
        validacion.GetProperty("resumen").GetString().Should().Contain("No hay registro");
        validacion.GetProperty("coherente").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void CreaSeccionValidacion_CuandoGeminiNoLaIncluyo()
    {
        var analisis = """{ "analisis_tivit": { "debilidades": [] } }""";
        var archivos = new[] { "Anexo_1.pdf" };

        var resultado = ValidacionDocumentalService.AplicarValidacion(analisis, archivos);

        var validacion = Validacion(resultado);
        validacion.TryGetProperty("documentos", out _).Should().BeTrue();
        validacion.TryGetProperty("inconsistencias", out _).Should().BeTrue();
        validacion.TryGetProperty("coherente", out _).Should().BeTrue();
    }

    [Fact]
    public void DetectaInconsistencia_DesdeMotivoDePerdidaEsquemaAnterior()
    {
        // Compatibilidad: esquemas antiguos usan analisis_perdida.motivo_principal
        var analisis = """
        {
          "analisis_perdida": {
            "motivo_principal": "Oferta inadmisible porque no adjuntó la declaración jurada simple"
          }
        }
        """;
        var archivos = new[] { "Declaracion_Jurada_Simple_TIVIT.pdf" };

        var resultado = ValidacionDocumentalService.AplicarValidacion(analisis, archivos);

        var validacion = Validacion(resultado);
        validacion.GetProperty("coherente").GetBoolean().Should().BeFalse();
        validacion.GetProperty("inconsistencias").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public void NoRompe_ConJsonInvalido()
    {
        var invalido = "esto no es json";
        var resultado = ValidacionDocumentalService.AplicarValidacion(invalido, new[] { "a.pdf" });
        resultado.Should().Be(invalido);
    }
}
