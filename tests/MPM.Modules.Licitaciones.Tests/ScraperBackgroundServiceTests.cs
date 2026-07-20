using FluentAssertions;
using Microsoft.Extensions.Configuration;
using MPM.Modules.Licitaciones.Services;
using Xunit;

namespace MPM.Modules.Licitaciones.Tests;

public class ScraperBackgroundServiceTests
{
    // ── BUG-005: DB_HOST/DB_PORT ya no hardcodeados a "db" ──────────────────────────────

    [Fact]
    public void BuildScraperEnvironmentVariables_ConDbHostConfigurado_UsaElValorDeConfiguracion()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DB_HOST"] = "10.33.176.3", // IP privada de Cloud SQL, no "db"
            ["DB_PORT"] = "5432",
        }).Build();

        var env = ScraperBackgroundService.BuildScraperEnvironmentVariables(config);

        env["DB_HOST"].Should().Be("10.33.176.3");
        env["DB_PORT"].Should().Be("5432");
    }

    [Fact]
    public void BuildScraperEnvironmentVariables_SinDbHostConfigurado_UsaDbComoDefaultLocal()
    {
        // Docker Compose local no define DB_HOST explícitamente para el scraper — "db" (el
        // nombre del servicio de Docker Compose) debe seguir siendo el default cuando no hay
        // configuración, para no romper el flujo local.
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        var env = ScraperBackgroundService.BuildScraperEnvironmentVariables(config);

        env["DB_HOST"].Should().Be("db");
        env["DB_PORT"].Should().Be("5432");
    }

    // ── BUG-007: 0 resultados es anómalo, no éxito ──────────────────────────────────────

    [Theory]
    [InlineData(0, 5, true)]
    [InlineData(0, 0, false)]
    [InlineData(1, 5, false)]
    [InlineData(1, 0, false)]
    public void EsCicloExitoso_ClasificaSegunExitCodeYTotal(int exitCode, int total, bool esperado)
    {
        ScraperBackgroundService.EsCicloExitoso(exitCode, total).Should().Be(esperado);
    }

    // ── BUG-006: lectura de stdout/stderr en paralelo (guarda de regresión de código fuente) ──
    // No se puede probar el comportamiento del deadlock de forma determinística sin un proceso
    // real que llene el buffer de stderr (frágil y lento en CI); se verifica en el código fuente
    // que la lectura sigue el patrón Task.WhenAll en vez de dos awaits secuenciales.

    [Fact]
    public void SourceCode_LecturaDeStdoutYStderr_UsaTaskWhenAll()
    {
        var source = File.ReadAllText(FindSourceFile("ScraperBackgroundService.cs"));

        source.Should().Contain("Task.WhenAll(stdoutTask, stderrTask)",
            "stdout y stderr deben leerse en paralelo para evitar el deadlock si el proceso hijo llena el buffer de stderr (QA BUG-006)");
    }

    // ── BUG-007: alertas van a Telegram, no a un GUID sin dueño ─────────────────────────

    [Fact]
    public void SourceCode_NotificacionesDelScraper_YaNoApuntanSoloAlGuidVacio()
    {
        var source = File.ReadAllText(FindSourceFile("ScraperBackgroundService.cs"));

        source.Should().Contain("NotificarOperacionesTelegramAsync",
            "las fallas/anomalías del scraper deben enrutarse a Telegram, no quedarse solo como notificación in-app a un usuario inexistente (QA BUG-007)");
        source.Should().Contain("ListarAccountManagersAsync",
            "el envío a Telegram debe usar la lista real de destinatarios con chat vinculado");
    }

    // ── Regresión detectada durante pruebas en vivo el 2026-07-08: al activar MarkdownV2
    // (BUG-013) en TelegramNotificationService, las alertas operativas del scraper (que
    // interpolan texto con paréntesis/puntos sin escapar) empezaron a fallar con 400 de
    // Telegram ("Character '(' is reserved"). NotificarOperacionesTelegramAsync ahora escapa
    // título y detalle internamente antes de armar el mensaje.

    [Fact]
    public void SourceCode_AlertasOperativasDelScraper_EscapanMarkdownV2AntesDeEnviar()
    {
        var source = File.ReadAllText(FindSourceFile("ScraperBackgroundService.cs")).Replace("\r\n", "\n");

        var inicioMetodo = source.IndexOf("private async Task NotificarOperacionesTelegramAsync(", StringComparison.Ordinal);
        inicioMetodo.Should().BeGreaterThanOrEqualTo(0);
        var cuerpoMetodo = source[inicioMetodo..(inicioMetodo + 600)];

        cuerpoMetodo.Should().Contain("TelegramNotificationService.EscaparMarkdownV2",
            "el texto interpolado en las alertas operativas del scraper (código, totales, detalles de error) puede traer paréntesis/puntos sin escapar y romper MarkdownV2");
    }

    // ── Xvfb: detección automática y MP_HEADLESS (002-fase5-deploy-gcp §1b) ──────────────
    // Cloud Run no tiene pantalla física → Chromium headless recibe 403 de reCAPTCHA en
    // "Ver Adjuntos". Xvfb da un framebuffer virtual para correr en modo headed. El scraper
    // debe detectar xvfb-run automáticamente y setear MP_HEADLESS=false en ese caso.

    [Fact]
    public void BuildScraperEnvironmentVariables_ConXvfb_SetHeadlessFalse()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MP_HEADLESS"] = "true", // incluso si el config dice true
        }).Build();

        var env = ScraperBackgroundService.BuildScraperEnvironmentVariables(config, useXvfb: true);

        env["MP_HEADLESS"].Should().Be("false",
            "con Xvfb el scraper debe correr en modo headed dentro del framebuffer virtual para evitar reCAPTCHA");
    }

    [Fact]
    public void BuildScraperEnvironmentVariables_SinXvfb_UsaHeadlessTruePorDefault()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        var env = ScraperBackgroundService.BuildScraperEnvironmentVariables(config, useXvfb: false);

        env["MP_HEADLESS"].Should().Be("true",
            "sin Xvfb (local) el scraper debe correr en modo headless tradicional");
    }

    [Fact]
    public void BuildScraperEnvironmentVariables_SinXvfb_PermiteOverrideDeConfig()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MP_HEADLESS"] = "false", // override explícito (ej. debug con pantalla real)
        }).Build();

        var env = ScraperBackgroundService.BuildScraperEnvironmentVariables(config, useXvfb: false);

        env["MP_HEADLESS"].Should().Be("false",
            "sin Xvfb, MP_HEADLESS del config debe respetarse para permitir overrides manuales");
    }

    [Fact]
    public void BuildScraperEnvironmentVariables_ConXvfb_IgnoraConfigHeadless()
    {
        // Xvfb manda sobre cualquier config — no tiene sentido correr headed dentro de Xvfb
        // y a la vez forzar headless=true (rompería el propósito de la mitigación).
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MP_HEADLESS"] = "true",
        }).Build();

        var env = ScraperBackgroundService.BuildScraperEnvironmentVariables(config, useXvfb: true);

        env["MP_HEADLESS"].Should().Be("false",
            "Xvfb debe imponerse sobre el config de MP_HEADLESS — no se puede correr headless dentro de un framebuffer");
    }

    // ── Guardias de código fuente: la invocación del proceso usa xvfb-run cuando está disponible ──

    [Fact]
    public void SourceCode_DeteccionXvfb_LlamaIsXvfbAvailable()
    {
        var source = File.ReadAllText(FindSourceFile("ScraperBackgroundService.cs"));

        source.Should().Contain("IsXvfbAvailable()",
            "el scraper debe detectar xvfb-run en el PATH antes de decidir cómo invocar el proceso Node");
    }

    [Fact]
    public void SourceCode_InvocacionConXvfb_UsaXvfbRunWrapper()
    {
        var source = File.ReadAllText(FindSourceFile("ScraperBackgroundService.cs"));

        source.Should().Contain("xvfb-run",
            "cuando Xvfb está disponible, la invocación debe envolver Node con xvfb-run");
        source.Should().Contain("--auto-servernum",
            "xvfb-run debe usar --auto-servernum para manejar el display automáticamente");
    }

    [Fact]
    public void SourceCode_IsXvfbAvailable_ProbeConXvfbRunHelp()
    {
        var source = File.ReadAllText(FindSourceFile("ScraperBackgroundService.cs")).Replace("\r\n", "\n");

        var inicioMetodo = source.IndexOf("private static bool IsXvfbAvailable()", StringComparison.Ordinal);
        inicioMetodo.Should().BeGreaterThanOrEqualTo(0, "IsXvfbAvailable debe existir");
        var cuerpoMetodo = source[inicioMetodo..(inicioMetodo + 500)];

        cuerpoMetodo.Should().Contain("\"xvfb-run\"",
            "la detección debe hacer un probe con xvfb-run (no asumir que existe)");
        cuerpoMetodo.Should().Contain("--help",
            "el probe debe usar --help o equivalente para no iniciar un servidor Xvfb real");
    }

    // ── 030-qol-frontend-y-fix-scraper US3: "0 licitaciones, código 0" ya no se confunde con
    // una falla real ────────────────────────────────────────────────────────────────────

    [Fact]
    public void SourceCode_NotificarResultadoAsync_DistingueSinResultadosDeError()
    {
        var source = File.ReadAllText(FindSourceFile("ScraperBackgroundService.cs"));

        source.Should().Contain("scraper_sin_resultados",
            "un ciclo con exitCode == 0 y 0 licitaciones (lectura exitosa, sin novedades) debe " +
            "notificarse distinto de un ciclo con exitCode != 0 (falla real de lectura) — antes " +
            "ambos compartían el mismo tipo 'scraper_error' y el mismo mensaje ambiguo");
    }

    [Fact]
    public void SourceCode_BuscarLicitaciones_LanzaErrorSiNingunEstadoTuvoExito()
    {
        var source = File.ReadAllText(FindScraperV2File("modulos", "buscar.js"));

        source.Should().Contain("estadosExitosos",
            "buscarLicitaciones debe contar cuántos de los 5 estados de búsqueda pudieron leerse");
        source.Should().Contain("estadosExitosos === 0",
            "si 0 de 5 estados pudieron leerse, la función debe lanzar un error en vez de " +
            "retornar [] silenciosamente (antes se reportaba como '0 licitaciones legítimas')");
    }

    [Fact]
    public void SourceCode_SchedulerCycle_MarcaExitCodeEnFallo()
    {
        var source = File.ReadAllText(FindScraperV2File("modulos", "scheduler.js"));

        source.Should().Contain("process.exitCode = 1",
            "el catch de cycle() en modo --daemon solo logueaba el error sin marcar el proceso " +
            "como fallido — el proceso terminaba con exit code 0 (Node por defecto) aunque el " +
            "ciclo hubiera fallado, y el wrapper .NET lo leía como éxito silencioso");
    }

    // scraper-mp (v1, deprecado) y scraper-mp-v2 comparten nombres de archivo (buscar.js,
    // scheduler.js) — FindSourceFile (que asume nombre único en el repo) no sirve aquí.
    private static string FindScraperV2File(string subfolder, string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MPM.sln")))
            dir = dir.Parent;

        if (dir == null) throw new FileNotFoundException("No se encontró MPM.sln subiendo desde el directorio de test.");

        return Path.Combine(dir.FullName, "tools", "scraper-mp-v2", subfolder, fileName);
    }

    // ── Dockerfile: Xvfb + xauth instalados en la imagen de runtime ──────────────────────

    [Fact]
    public void Dockerfile_InstalaXvfbYXauth()
    {
        var source = File.ReadAllText(FindDockerfile());

        source.Should().Contain("xvfb",
            "el Dockerfile del API debe instalar Xvfb para que el scraper funcione en Cloud Run sin pantalla física");
        source.Should().Contain("xauth",
            "xvfb-run requiere xauth (no viene instalado por defecto aunque Xvfb sí)");
    }

    private static string FindDockerfile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MPM.sln")))
            dir = dir.Parent;

        if (dir == null) throw new FileNotFoundException("No se encontró MPM.sln subiendo desde el directorio de test.");

        return Path.Combine(dir.FullName, "src", "MPM.Api", "Dockerfile");
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
