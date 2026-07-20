using FluentAssertions;
using MPM.Modules.Analisis.Services;
using System.Text.Json;
using Xunit;

namespace MPM.Modules.Analisis.Tests.Services;

/// <summary>
/// Cubre 029-fix-hallazgos-code-review-competidores-alertas FR-017/US13 (QA BUG-007): menciones
/// de moneda en prosa (ej. "DÓLAR AMERICANO") deben normalizarse a la misma sigla que usa
/// formatMoney en el frontend (ej. "US$"), para que un mismo hecho no se muestre de dos formas
/// distintas en el mismo dashboard.
/// </summary>
public class MonedaNormalizerServiceTests
{
    [Fact]
    public void Normalizar_ResumenConDolarAmericano_LoReemplazaPorSigla()
    {
        var json = """
        {
          "validacion_documental": {
            "resumen": "El monto adjudicado está expresado en DÓLAR AMERICANO según el acta."
          }
        }
        """;

        var resultado = MonedaNormalizerService.Normalizar(json);
        var root = JsonDocument.Parse(resultado).RootElement;

        root.GetProperty("validacion_documental").GetProperty("resumen").GetString()
            .Should().Be("El monto adjudicado está expresado en USD según el acta.");
    }

    [Fact]
    public void Normalizar_DebilidadesYFortalezasConPesosChilenosYUF_LasReemplaza()
    {
        var json = """
        {
          "analisis_tivit": {
            "fortalezas": ["Oferta competitiva en pesos chilenos frente al ganador"],
            "debilidades": ["No contempló ajuste en Unidad de Fomento como el ganador"]
          }
        }
        """;

        var resultado = MonedaNormalizerService.Normalizar(json);
        var root = JsonDocument.Parse(resultado).RootElement;
        var at = root.GetProperty("analisis_tivit");

        at.GetProperty("fortalezas")[0].GetString().Should().Be("Oferta competitiva en CLP frente al ganador");
        at.GetProperty("debilidades")[0].GetString().Should().Be("No contempló ajuste en UF como el ganador");
    }

    [Fact]
    public void Normalizar_MotivoInadmisibilidadYBrecha_TambienSeNormalizan()
    {
        var json = """
        {
          "adjudicacion": {
            "ofertantes": [
              { "nombre": "Proveedor X", "motivo_inadmisibilidad": "Oferta económica en dólares americanos fuera de rango" }
            ]
          },
          "analisis_tivit": {
            "brechas_identificadas": [
              { "area": "Económica", "descripcion": "Diferencia de 1000 euros europeos vs el ganador", "recomendacion_mejora": "Ajustar oferta en dólares de estados unidos" }
            ]
          }
        }
        """;

        var resultado = MonedaNormalizerService.Normalizar(json);
        var root = JsonDocument.Parse(resultado).RootElement;

        root.GetProperty("adjudicacion").GetProperty("ofertantes")[0].GetProperty("motivo_inadmisibilidad").GetString()
            .Should().Be("Oferta económica en USD fuera de rango");

        var brecha = root.GetProperty("analisis_tivit").GetProperty("brechas_identificadas")[0];
        brecha.GetProperty("descripcion").GetString().Should().Be("Diferencia de 1000 EUR vs el ganador");
        brecha.GetProperty("recomendacion_mejora").GetString().Should().Be("Ajustar oferta en USD");
    }

    [Fact]
    public void Normalizar_NoTocaNombresDeProveedoresQueContenganPalabrasParciales()
    {
        // "Euros Import SPA" no debe convertirse en "EUR Import SPA" -- el patrón exige la frase
        // completa "euro(s) europeo(s)"/"de la unión europea", no la palabra suelta.
        var json = """
        {
          "adjudicacion": { "adjudicatario": { "nombre": "Euros Import SPA" } }
        }
        """;

        var resultado = MonedaNormalizerService.Normalizar(json);
        var root = JsonDocument.Parse(resultado).RootElement;

        root.GetProperty("adjudicacion").GetProperty("adjudicatario").GetProperty("nombre").GetString()
            .Should().Be("Euros Import SPA", "el nombre del proveedor no es un campo de texto libre normalizado");
    }

    [Fact]
    public void Normalizar_JsonInvalido_DevuelveElOriginalSinLanzar()
    {
        const string jsonInvalido = "{ esto no es json";
        var resultado = MonedaNormalizerService.Normalizar(jsonInvalido);
        resultado.Should().Be(jsonInvalido);
    }
}
