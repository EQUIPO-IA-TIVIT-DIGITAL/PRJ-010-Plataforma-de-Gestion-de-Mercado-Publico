using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MPM.Modules.Notificaciones.Services;
using MPM.Modules.Alertas.Data;
using MPM.Modules.Alertas.Services;
using System.Diagnostics;
using System.Text.Json;

namespace MPM.Modules.Licitaciones.Services;

public class ScraperBackgroundService(
    ILogger<ScraperBackgroundService> logger,
    IConfiguration config,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    private Timer? _timer;
    private const string NodeBinary = "node";

    private string GetScriptPath() =>
        config["Scraper:ScriptPath"] ??
        config["SCRAPER_SCRIPT_PATH"] ??
        Path.Combine(AppContext.BaseDirectory, "tools", "agente-mp.js");

    /// <summary>
    /// Variables de entorno para el subproceso Node del scraper. DB_HOST/DB_PORT ya no están
    /// hardcodeados a "db" (nombre del servicio de Docker Compose local) — se leen de
    /// configuración, con "db"/"5432" como default solo para no romper el flujo local
    /// (QA BUG-005). Extraído como método estático testeable sin spawnear un proceso real.
    /// </summary>
    /// <param name="useXvfb">
    /// Si <c>true</c>, el scraper corre dentro de un framebuffer virtual (Xvfb) en modo
    /// "headed" (MP_HEADLESS=false) para evitar reCAPTCHA en Cloud Run (ver research.md §1b
    /// de 002-fase5-deploy-gcp). Si <c>false</c>, cae al modo headless tradicional (local).
    /// </param>
    internal static Dictionary<string, string> BuildScraperEnvironmentVariables(IConfiguration config, bool useXvfb = false) => new()
    {
        ["MP_HEADLESS"] = useXvfb ? "false" : (config["MP_HEADLESS"] ?? "true"),
        ["SCRAPER_DAEMON"] = "true",
        ["MP_RUT"] = config["MP_RUT"] ?? "",
        ["MP_PASSWORD"] = config["MP_PASSWORD"] ?? "",
        ["MP_ANALISIS_IA"] = config["MP_ANALISIS_IA"] ?? "true",
        ["MP_FECHA_DESDE"] = config["MP_FECHA_DESDE"] ?? "01-01-2025",
        ["MP_TICKET"] = config["MP_TICKET"] ?? "",
        ["API_BASE_URL"] = config["API_BASE_URL"] ?? "http://localhost:80",
        ["JWT_SECRET"] = config["JWT_SECRET"] ?? config["JWT:Secret"] ?? "",
        ["JWT_ISSUER"] = config["JWT_ISSUER"] ?? config["JWT:Issuer"] ?? "",
        ["JWT_AUDIENCE"] = config["JWT_AUDIENCE"] ?? config["JWT:Audience"] ?? "",
        ["DB_HOST"] = config["DB_HOST"] ?? "db",
        ["DB_PORT"] = config["DB_PORT"] ?? "5432",
        ["DB_NAME"] = config["DB_NAME"] ?? "",
        ["DB_USER"] = config["DB_USER"] ?? "",
        ["DB_PASSWORD"] = config["DB_PASSWORD"] ?? "",
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = config.GetValue<bool>("Scraper:Enabled") || config.GetValue<bool>("SCRAPER_ENABLED");
        if (!enabled)
        {
            logger.LogInformation("ScraperBackgroundService disabled (SCRAPER_ENABLED=false)");
            return;
        }

        var intervalHours = config.GetValue<int>("Scraper:IntervalHours");
        if (intervalHours <= 0)
            intervalHours = config.GetValue<int>("SCRAPER_INTERVAL_HOURS");
        if (intervalHours <= 0) intervalHours = 12;

        logger.LogInformation("Retrasando el inicio inicial del Scraper de Playwright por 10 minutos para evitar colisiones con la sincronización general...");
        await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);

        await Task.Run(() => EjecutarScraperAsync(stoppingToken), stoppingToken);

        _timer = new Timer(
            _ => EjecutarScraperAsync(stoppingToken).ConfigureAwait(false),
            null,
            TimeSpan.FromHours(intervalHours),
            TimeSpan.FromHours(intervalHours));

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    /// <summary>
    /// Ejecuta un solo ciclo del scraper y retorna (sin Timer). Pensado para invocarse desde
    /// el "modo worker" de <c>Program.cs</c> (Cloud Run Job <c>scraper-job</c>, ver
    /// 002-fase5-deploy-gcp plan.md T008/T010) — requiere que 016-extraccion-documentos-api
    /// ya haya reducido el uso de Chromium a una renovación de sesión corta; de lo contrario
    /// esta ejecución puede tardar tanto como el ciclo completo del daemon Node actual.
    /// </summary>
    public Task EjecutarCicloUnaVezAsync(CancellationToken ct = default) => EjecutarScraperAsync(ct);

    private async Task EjecutarScraperAsync(CancellationToken ct)
    {
        var exitCode = -1;
        var outputLines = new List<string>();

        try
        {
            logger.LogInformation("Scraper cycle triggered at {Time}", DateTime.UtcNow);

            if (!await IsNodeAvailableAsync())
            {
                logger.LogError("Node.js binary '{Binary}' no encontrado. El scraper no puede ejecutarse.", NodeBinary);
                await NotificarErrorConfigAsync($"Node.js ('{NodeBinary}') no está disponible en el PATH del contenedor. Verifique que Node.js esté instalado.", ct);
                return;
            }

            var scraperPath = GetScriptPath();

            if (!Path.IsPathRooted(scraperPath))
            {
                var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
                if (!Directory.Exists(baseDir))
                    baseDir = Directory.GetCurrentDirectory();
                scraperPath = Path.GetFullPath(Path.Combine(baseDir, scraperPath));
            }

            var workingDir = Path.GetDirectoryName(scraperPath) ?? Directory.GetCurrentDirectory();

            if (!File.Exists(scraperPath))
            {
                logger.LogError("Scraper script no encontrado: {Path}", scraperPath);
                await NotificarErrorConfigAsync($"Script no encontrado en '{scraperPath}'. Verifique que Scraper:ScriptPath esté configurado correctamente.", ct);
                return;
            }

            // SCRAPER_BACKFILL_COMPETIDORES: override manual de un solo uso (gcloud run jobs
            // execute scraper-job --update-env-vars=SCRAPER_BACKFILL_COMPETIDORES=true), para
            // recuperar el Cuadro de Ofertas de licitaciones viejas ya analizadas -- el ciclo
            // normal (--daemon --incremental) las salta sin abrir su ficha (agente-mp.js linea
            // ~140) porque para Acta/IA no hay nada nuevo que hacer con ellas. No usar --daemon
            // aca: --backfill-competidores es un ciclo de una sola pasada, no un proceso residente.
            var backfillCompetidores = config.GetValue<bool>("SCRAPER_BACKFILL_COMPETIDORES");
            var scraperArgs = backfillCompetidores ? "--backfill-competidores" : "--daemon --incremental";

            var startInfo = new ProcessStartInfo
            {
                FileName = NodeBinary,
                Arguments = $"\"{scraperPath}\" {scraperArgs}",
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            // Xvfb: si xvfb-run está disponible (Dockerfile de Cloud Run lo instala), envolver
            // la invocación de Node con un framebuffer virtual y correr Chromium en modo headed
            // (MP_HEADLESS=false). Mercado Público penaliza el fingerprint headless con
            // reCAPTCHA en "Ver Adjuntos" (403 sistemático, verificado en vivo 2026-07-06).
            // En local (sin xvfb-run) cae al flujo headless tradicional sin cambios.
            var useXvfb = IsXvfbAvailable();
            if (useXvfb)
            {
                startInfo.FileName = "xvfb-run";
                startInfo.Arguments = $"--auto-servernum -- {NodeBinary} \"{scraperPath}\" {scraperArgs}";
                logger.LogInformation("Xvfb detectado — scraper correrá en modo headed dentro de framebuffer virtual");
            }

            foreach (var (key, value) in BuildScraperEnvironmentVariables(config, useXvfb))
                startInfo.EnvironmentVariables[key] = value;

            logger.LogInformation("Ejecutando: {FileName} {Args} en {Dir}", startInfo.FileName, startInfo.Arguments, workingDir);

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            // Leer stdout y stderr en paralelo: si se drena uno primero por completo, el pipe
            // del otro puede llenarse y el proceso hijo se bloquea escribiendo (deadlock
            // clásico si Chromium/Playwright vuelca mucho stderr) — QA BUG-006.
            var stdoutTask = LeerLineasAsync(process.StandardOutput, ct);
            var stderrTask = LeerLineasAsync(process.StandardError, ct);
            await Task.WhenAll(stdoutTask, stderrTask);
            outputLines = stdoutTask.Result;
            var errorLines = stderrTask.Result;

            await process.WaitForExitAsync(ct);
            exitCode = process.ExitCode;

            if (exitCode != 0)
            {
                logger.LogWarning("Scraper exited with code {ExitCode}", exitCode);
                foreach (var line in errorLines)
                    logger.LogWarning("[SCRAPER/ERR] {Line}", line);
            }
            else
            {
                logger.LogInformation("Scraper cycle completed successfully");
            }

            await NotificarResultadoAsync(exitCode, outputLines, ct);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Scraper cycle cancelled");
            await NotificarErrorAsync("Ciclo cancelado", ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scraper cycle failed");
            await NotificarErrorAsync(ex.Message, ct);
        }
    }

    private static async Task<bool> IsNodeAvailableAsync()
    {
        try
        {
            using var probe = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = NodeBinary,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };
            probe.Start();
            await probe.WaitForExitAsync();
            return probe.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Detecta si <c>xvfb-run</c> está disponible en el PATH. Si lo está, el scraper se
    /// envuelve con <c>xvfb-run --auto-servernum --</c> para correr Chromium en modo headed
    /// dentro de un framebuffer virtual, evitando reCAPTCHA en Cloud Run (sin pantalla física).
    /// En local (sin Xvfb instalado) retorna <c>false</c> y el scraper cae al modo headless.
    /// </summary>
    private static bool IsXvfbAvailable()
    {
        try
        {
            using var probe = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "xvfb-run",
                    Arguments = "--help",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };
            probe.Start();
            probe.WaitForExit(3000);
            return probe.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task NotificarErrorConfigAsync(string detalle, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var notificaciones = scope.ServiceProvider.GetRequiredService<NotificacionesService>();
            await notificaciones.CrearAsync(
                usuarioId: "00000000-0000-0000-0000-000000000000",
                tipo: "scraper_config_error",
                titulo: "Error de configuración del scraper",
                mensaje: $"El scraper no puede ejecutarse: {detalle}",
                metadata: new { error = detalle });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error creando notificacion scraper_config_error");
        }

        await NotificarOperacionesTelegramAsync("⚠️", "Scraper no pudo ejecutarse", detalle, ct);
    }

    /// <summary>
    /// Un ciclo con exitCode == 0 pero 0 licitaciones procesadas es anómalo (posible cambio de
    /// estructura del sitio no detectado, cupo agotado sin reintento, etc.) — QA BUG-007: ya no
    /// se reporta como "Scraper completado" silencioso.
    /// </summary>
    internal static bool EsCicloExitoso(int exitCode, int totalLicitaciones) => exitCode == 0 && totalLicitaciones > 0;

    private async Task NotificarResultadoAsync(int exitCode, List<string> output, CancellationToken ct)
    {
        var total = ExtraerValor(output, "Total licitaciones:");
        var conActa = ExtraerValor(output, "Con Acta:");
        var sinActa = ExtraerValor(output, "Sin Acta:");
        var conError = ExtraerValor(output, "Con error:");

        var totalNumerico = int.TryParse(total, out var t) ? t : 0;
        var esExitoso = EsCicloExitoso(exitCode, totalNumerico);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var notificaciones = scope.ServiceProvider.GetRequiredService<NotificacionesService>();

            if (esExitoso)
            {
                await notificaciones.CrearAsync(
                    usuarioId: "00000000-0000-0000-0000-000000000000",
                    tipo: "scraper_completado",
                    titulo: "Scraper completado",
                    mensaje: $"Se procesaron {total} licitaciones. Actas descargadas: {conActa}. Sin acta: {sinActa}. Errores: {conError}.",
                    metadata: new { total, conActa, sinActa, conError, exitCode });
            }
            else if (exitCode == 0 && totalNumerico == 0)
            {
                // 030-qol-frontend-y-fix-scraper US3/FR-007: antes este caso compartía el mismo
                // tipo/mensaje ("scraper_error"/"El scraper terminó con código 0...") que un
                // ciclo con exitCode != 0 (falla real de lectura del sitio), y el usuario no
                // podía distinguir "no había licitaciones nuevas" de "el scraper no pudo leer
                // Mercado Público" sin abrir logs. Ahora que scraper-mp-v2 lanza un error real
                // (exitCode != 0) cuando 0 de 5 estados de búsqueda pudieron leerse (ver
                // buscar.js), este branch solo se alcanza cuando el scraper SÍ pudo leer el
                // sitio pero legítimamente no encontró licitaciones nuevas — se notifica en tono
                // neutro, no como error.
                await notificaciones.CrearAsync(
                    usuarioId: "00000000-0000-0000-0000-000000000000",
                    tipo: "scraper_sin_resultados",
                    titulo: "Scraper completado sin licitaciones nuevas",
                    mensaje: "El scraper corrió correctamente pero no encontró licitaciones nuevas en este ciclo.",
                    metadata: new { total, conActa, sinActa, conError, exitCode });
            }
            else
            {
                await notificaciones.CrearAsync(
                    usuarioId: "00000000-0000-0000-0000-000000000000",
                    tipo: "scraper_error",
                    titulo: "Scraper finalizó con error",
                    mensaje: $"El scraper terminó con código {exitCode}. Licitaciones: {total}, Actas: {conActa}.",
                    metadata: new { total, conActa, sinActa, conError, exitCode });
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error creando notificacion del scraper");
        }

        // Marcador emitido por agente-mp.js cuando adjuntos.js detecta que el grid de adjuntos
        // cambió de estructura (no un cupo agotado) — QA BUG-003. Se distingue del resto porque
        // no se resuelve solo ni con un reintento de sesión: requiere revisión manual.
        var estructuraCambio = output.Any(l => l.Contains("ESTRUCTURA_CAMBIO_DETECTADA: true"));

        if (estructuraCambio)
        {
            await NotificarOperacionesTelegramAsync(
                "🔴", "Mercado Público cambió la estructura de su sitio",
                "El scraper detectó que el grid de adjuntos ya no coincide con el selector esperado. Requiere revisión manual, no es cupo agotado.", ct);
        }
        else if (exitCode != 0)
        {
            await NotificarOperacionesTelegramAsync(
                "🔴", "Scraper finalizó con error",
                $"Código {exitCode}. Licitaciones: {total}, Actas: {conActa}.", ct);
        }
        else if (totalNumerico == 0)
        {
            await NotificarOperacionesTelegramAsync(
                "🟡", "Scraper completó el ciclo sin encontrar licitaciones",
                "0 resultados — revisar si es un cambio real del sitio.", ct);
        }
    }

    private async Task NotificarErrorAsync(string detalle, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var notificaciones = scope.ServiceProvider.GetRequiredService<NotificacionesService>();

            await notificaciones.CrearAsync(
                usuarioId: "00000000-0000-0000-0000-000000000000",
                tipo: "scraper_error",
                titulo: "Error en scraper",
                mensaje: $"El scraper no pudo ejecutarse: {detalle}",
                metadata: new { error = detalle });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error creando notificacion de error del scraper");
        }

        await NotificarOperacionesTelegramAsync("🔴", "Scraper no pudo ejecutarse", detalle, ct);
    }

    /// <summary>
    /// Envía una alerta operativa a Telegram, a todos los account managers con chat vinculado
    /// (mismo destinatario que las alertas de licitaciones — no hay un canal de "operaciones"
    /// separado hoy). Reemplaza las notificaciones in-app dirigidas al GUID
    /// 00000000-0000-0000-0000-000000000000, que nadie podía ver (QA BUG-007).
    /// <paramref name="titulo"/> y <paramref name="detalle"/> se escapan para MarkdownV2 acá
    /// mismo — los call-sites no deben preocuparse por caracteres reservados (paréntesis,
    /// puntos, guiones son comunes en estos mensajes y rompían el envío tras el fix de BUG-013).
    /// </summary>
    private async Task NotificarOperacionesTelegramAsync(string emoji, string titulo, string detalle, CancellationToken ct)
    {
        var mensaje = $"{emoji} *{TelegramNotificationService.EscaparMarkdownV2(titulo)}*\n{TelegramNotificationService.EscaparMarkdownV2(detalle)}";
        try
        {
            using var scope = scopeFactory.CreateScope();
            var alertasHandler = scope.ServiceProvider.GetRequiredService<AlertasHandler>();
            var telegram = scope.ServiceProvider.GetRequiredService<TelegramNotificationService>();

            var destinatarios = await alertasHandler.ListarAccountManagersAsync(ct);
            foreach (var destinatario in destinatarios)
            {
                if (string.IsNullOrWhiteSpace(destinatario.TelegramChatId)) continue;
                var (enviada, error) = await telegram.EnviarAsync(destinatario.TelegramChatId, mensaje, ct: ct);
                if (!enviada)
                    logger.LogWarning("No se pudo enviar alerta operativa del scraper a Telegram (chat {ChatId}): {Error}", destinatario.TelegramChatId, error);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error enviando alerta operativa del scraper a Telegram");
        }
    }

    private static string ExtraerValor(List<string> lines, string prefijo)
    {
        var linea = lines.FirstOrDefault(l => l.Trim().StartsWith(prefijo));
        if (linea == null) return "0";
        var idx = linea.IndexOf(':');
        return idx >= 0 ? linea[(idx + 1)..].Trim() : "0";
    }

    private async Task<List<string>> LeerLineasAsync(StreamReader reader, CancellationToken ct)
    {
        var lines = new List<string>();
        try
        {
            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line != null)
                {
                    logger.LogInformation("[SCRAPER] {Line}", line);
                    lines.Add(line);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error leyendo output del scraper");
        }
        return lines;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("ScraperBackgroundService stopping.");
        _timer?.Change(Timeout.Infinite, 0);
        return base.StopAsync(cancellationToken);
    }
}