using MPM.Modules.Alertas.Data;
using MPM.Modules.Alertas.Models;
using MPM.Modules.Alertas.Services;
using FluentAssertions;
using Xunit;

namespace MPM.Modules.Alertas.Tests.Services;

public class AlertasMatchingServiceTests
{
    private static ReglaActivaRow Regla(
        string keyword = "SOC", string? sinonimosJson = null,
        decimal? montoMinimo = null, decimal? montoMaximo = null,
        string[]? tipos = null, string[]? organismos = null) => new()
    {
        p_id = 1,
        p_usuario_id = "user-1",
        p_keyword = keyword,
        p_sinonimos_ia = sinonimosJson,
        p_monto_minimo = montoMinimo,
        p_monto_maximo = montoMaximo,
        p_tipos_licitacion = tipos,
        p_organismos = organismos,
    };

    private static LicitacionParaMatching Licitacion(
        string nombre = "Contratación de servicios", string? descripcion = null,
        decimal? monto = null, string? tipo = null, string? organismo = null,
        DateTime? fechaCierre = null, string? link = null) =>
        new(1, "COD-1", nombre, descripcion, monto, tipo, organismo, fechaCierre, link);

    [Fact]
    public void EvaluarMatch_DebeCoincidirPorKeywordLiteralEnElNombre()
    {
        var regla = Regla(keyword: "cloud");
        var licitacion = Licitacion(nombre: "Servicios de cloud computing para el ministerio");

        var resultado = AlertasMatchingService.EvaluarMatch(regla, licitacion);

        resultado.Should().Be("cloud");
    }

    [Fact]
    public void EvaluarMatch_DebeCoincidirPorSinonimoAunqueNoEsteLaPalabraLiteral()
    {
        var regla = Regla(keyword: "SOC", sinonimosJson: "[\"centro de operaciones de seguridad\", \"monitoreo 24/7\"]");
        var licitacion = Licitacion(nombre: "Servicio de centro de operaciones de seguridad para la red");

        var resultado = AlertasMatchingService.EvaluarMatch(regla, licitacion);

        resultado.Should().Be("centro de operaciones de seguridad");
    }

    [Fact]
    public void EvaluarMatch_DebeRetornarNullSiNoHayCoincidenciaDeTexto()
    {
        var regla = Regla(keyword: "datacenter");
        var licitacion = Licitacion(nombre: "Compra de mobiliario de oficina");

        var resultado = AlertasMatchingService.EvaluarMatch(regla, licitacion);

        resultado.Should().BeNull();
    }

    [Fact]
    public void EvaluarMatch_DebeRespetarFiltroDeMontoMinimo()
    {
        var regla = Regla(keyword: "cloud", montoMinimo: 10_000_000);
        var licitacionBaja = Licitacion(nombre: "Servicios cloud", monto: 5_000_000);
        var licitacionAlta = Licitacion(nombre: "Servicios cloud", monto: 15_000_000);

        AlertasMatchingService.EvaluarMatch(regla, licitacionBaja).Should().BeNull();
        AlertasMatchingService.EvaluarMatch(regla, licitacionAlta).Should().Be("cloud");
    }

    [Fact]
    public void EvaluarMatch_DebeRespetarFiltroDeTipoLicitacion()
    {
        var regla = Regla(keyword: "cloud", tipos: ["LP", "LE"]);
        var licitacionTipoDistinto = Licitacion(nombre: "Servicios cloud", tipo: "CO");
        var licitacionTipoCorrecto = Licitacion(nombre: "Servicios cloud", tipo: "LP");

        AlertasMatchingService.EvaluarMatch(regla, licitacionTipoDistinto).Should().BeNull();
        AlertasMatchingService.EvaluarMatch(regla, licitacionTipoCorrecto).Should().Be("cloud");
    }

    // ── 032-mejora-alertas-correo (US1): matching con límites de palabra ──
    // Antes, el matching era `texto.Contains(keyword)` sin límites de palabra -- una keyword
    // corta como "TI" matcheaba cualquier texto que contuviera esas 2 letras juntas, incluso
    // como fragmento interno de otra palabra ("par-TI-cipantes"). Confirmado en vivo por el
    // usuario dueño de MPM.

    [Fact]
    public void EvaluarMatch_NoDebeCoincidirCuandoLaKeywordEsSoloUnFragmentoDeOtraPalabra()
    {
        var regla = Regla(keyword: "TI");
        var licitacion = Licitacion(nombre: "Producción evento mujeres participantes");

        var resultado = AlertasMatchingService.EvaluarMatch(regla, licitacion);

        resultado.Should().BeNull("'TI' es un fragmento interno de 'participantes', no la sigla como palabra independiente");
    }

    [Fact]
    public void EvaluarMatch_DebeCoincidirCuandoLaKeywordApareceComoPalabraIndependiente()
    {
        var regla = Regla(keyword: "TI");
        var licitacion = Licitacion(nombre: "Servicio de soporte TI para oficinas regionales");

        var resultado = AlertasMatchingService.EvaluarMatch(regla, licitacion);

        resultado.Should().Be("TI");
    }

    [Fact]
    public void EvaluarMatch_DebeSeguirCoincidiendoConFrasesCompuestasDeVariasPalabras()
    {
        var regla = Regla(keyword: "mesa de ayuda");
        var licitacion = Licitacion(nombre: "Contratación de servicio de mesa de ayuda para la institución");

        var resultado = AlertasMatchingService.EvaluarMatch(regla, licitacion);

        resultado.Should().Be("mesa de ayuda", "el fix de límites de palabra no debe romper el matching de frases existente");
    }

    [Fact]
    public void EvaluarMatch_DebeIgnorarSinonimosMalFormados_SinLanzarExcepcion()
    {
        var regla = Regla(keyword: "cloud", sinonimosJson: "esto no es json valido");
        var licitacion = Licitacion(nombre: "Compra de mobiliario"); // no matchea por keyword tampoco

        var resultado = AlertasMatchingService.EvaluarMatch(regla, licitacion);

        resultado.Should().BeNull();
    }

    // ── BUG-012: ListarAccountManagersAsync una sola vez por ciclo, no por licitación ──
    // AlertasHandler/AlertaEnriquecimientoService/TelegramNotificationService/
    // NotificacionesService son clases concretas sin interfaz (no mockeables con Moq sin
    // refactor); se verifica en el código fuente que la consulta ya no vive dentro del método
    // por-licitación (ProcesarGrupoAsync) sino en los puntos de entrada por ciclo
    // (EvaluarLicitacionesAsync/ProbarAsync).

    [Fact]
    public void SourceCode_ListarAccountManagersAsync_YaNoSeLlamaDentroDeProcesarGrupoAsync()
    {
        var source = File.ReadAllText(FindSourceFile("AlertasMatchingService.cs")).Replace("\r\n", "\n");

        var inicioMetodo = source.IndexOf("private async Task<ProbarAlertaResponse> ProcesarGrupoAsync(", StringComparison.Ordinal);
        inicioMetodo.Should().BeGreaterThanOrEqualTo(0);
        var finMetodo = source.IndexOf("\n    private", inicioMetodo + 1, StringComparison.Ordinal);
        if (finMetodo < 0) finMetodo = source.IndexOf("\n    internal", inicioMetodo + 1, StringComparison.Ordinal);
        var cuerpoMetodo = source[inicioMetodo..finMetodo];

        cuerpoMetodo.Should().NotContain("ListarAccountManagersAsync",
            "la consulta de destinatarios no debe repetirse por cada licitación con match (QA BUG-012)");
    }

    [Fact]
    public void SourceCode_EvaluarLicitacionesAsync_ConsultaDestinatariosUnaVezPorCiclo()
    {
        var source = File.ReadAllText(FindSourceFile("AlertasMatchingService.cs")).Replace("\r\n", "\n");

        var inicioMetodo = source.IndexOf("public async Task EvaluarLicitacionesAsync(", StringComparison.Ordinal);
        inicioMetodo.Should().BeGreaterThanOrEqualTo(0);
        var finMetodo = source.IndexOf("\n    public async Task<ProbarAlertaResponse> ProbarAsync", inicioMetodo + 1, StringComparison.Ordinal);
        var cuerpoMetodo = source[inicioMetodo..finMetodo];

        cuerpoMetodo.Should().Contain("ListarAccountManagersAsync",
            "la consulta debe ocurrir una vez, antes del foreach de licitaciones");
    }

    // ── US3 (024-inteligencia-competencia-alertas): canal de correo independiente de Telegram ──
    // FR-011: el fallo o ausencia de un canal no debe impedir el intento en el otro. Se verifica
    // en el codigo fuente que el bloque de envio de correo NO esta anidado dentro del
    // `if (grupo.Any(g => g.Regla.p_notificar_telegram))` que gatea el envio de Telegram.

    [Fact]
    public void SourceCode_EnvioDeEmail_NoEstaGateadoPorNotificarTelegram()
    {
        var source = File.ReadAllText(FindSourceFile("AlertasMatchingService.cs")).Replace("\r\n", "\n");

        var inicioMetodo = source.IndexOf("private async Task<ProbarAlertaResponse> ProcesarGrupoAsync(", StringComparison.Ordinal);
        inicioMetodo.Should().BeGreaterThanOrEqualTo(0);
        var finMetodo = source.IndexOf("\n    /// <summary>", inicioMetodo + 1, StringComparison.Ordinal);
        var cuerpoMetodo = source[inicioMetodo..finMetodo];

        var inicioBloqueTelegram = cuerpoMetodo.IndexOf("if (grupo.Any(g => g.Regla.p_notificar_telegram))", StringComparison.Ordinal);
        inicioBloqueTelegram.Should().BeGreaterThanOrEqualTo(0);
        var finBloqueTelegram = cuerpoMetodo.IndexOf("\n        }", inicioBloqueTelegram, StringComparison.Ordinal);
        var bloqueTelegram = cuerpoMetodo[inicioBloqueTelegram..finBloqueTelegram];

        bloqueTelegram.Should().NotContain("email.EnviarAsync",
            "el envio de correo debe vivir fuera del if de Telegram para que un destinatario " +
            "sin telegramChatId (o con notificarTelegram=false) igual reciba el correo si lo configuro");
        cuerpoMetodo.Should().Contain("email.EnviarAsync",
            "el metodo debe intentar el envio de correo en algun punto");
    }

    [Fact]
    public void SourceCode_EnvioDeEmail_SoloParaDestinatariosConEmailConfigurado()
    {
        var source = File.ReadAllText(FindSourceFile("AlertasMatchingService.cs")).Replace("\r\n", "\n");

        source.Should().Contain("string.IsNullOrEmpty(destinatario.EmailAlertas)",
            "debe saltar destinatarios sin correo configurado, igual que se hace con TelegramChatId");
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
