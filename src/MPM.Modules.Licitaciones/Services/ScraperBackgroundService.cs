using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MPM.Modules.Notificaciones.Services;
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

        logger.LogInformation("ScraperBackgroundService starting. Interval: {Hours}h", intervalHours);

        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        await Task.Run(() => EjecutarScraperAsync(stoppingToken), stoppingToken);

        _timer = new Timer(
            _ => EjecutarScraperAsync(stoppingToken).ConfigureAwait(false),
            null,
            TimeSpan.FromHours(intervalHours),
            TimeSpan.FromHours(intervalHours));

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

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

            var startInfo = new ProcessStartInfo
            {
                FileName = NodeBinary,
                Arguments = $"\"{scraperPath}\" --daemon --incremental",
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            startInfo.EnvironmentVariables["MP_HEADLESS"] = "true";
            startInfo.EnvironmentVariables["SCRAPER_DAEMON"] = "true";
            startInfo.EnvironmentVariables["MP_RUT"] = config["MP_RUT"] ?? "";
            startInfo.EnvironmentVariables["MP_PASSWORD"] = config["MP_PASSWORD"] ?? "";
            startInfo.EnvironmentVariables["MP_ANALISIS_IA"] = config["MP_ANALISIS_IA"] ?? "true";
            startInfo.EnvironmentVariables["MP_FECHA_DESDE"] = config["MP_FECHA_DESDE"] ?? "01-01-2025";
            startInfo.EnvironmentVariables["API_BASE_URL"] = config["API_BASE_URL"] ?? "http://localhost:80";
            startInfo.EnvironmentVariables["JWT_SECRET"] = config["JWT_SECRET"] ?? config["JWT:Secret"] ?? "";
            startInfo.EnvironmentVariables["JWT_ISSUER"] = config["JWT_ISSUER"] ?? config["JWT:Issuer"] ?? "";
            startInfo.EnvironmentVariables["JWT_AUDIENCE"] = config["JWT_AUDIENCE"] ?? config["JWT:Audience"] ?? "";
            startInfo.EnvironmentVariables["DB_HOST"] = "db";
            startInfo.EnvironmentVariables["DB_PORT"] = "5432";
            startInfo.EnvironmentVariables["DB_NAME"] = config["DB_NAME"] ?? "";
            startInfo.EnvironmentVariables["DB_USER"] = config["DB_USER"] ?? "";
            startInfo.EnvironmentVariables["DB_PASSWORD"] = config["DB_PASSWORD"] ?? "";

            logger.LogInformation("Ejecutando: node {Args} en {Dir}", startInfo.Arguments, workingDir);

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            outputLines = await LeerLineasAsync(process.StandardOutput, ct);
            var errorLines = await LeerLineasAsync(process.StandardError, ct);

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
    }

    private async Task NotificarResultadoAsync(int exitCode, List<string> output, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var notificaciones = scope.ServiceProvider.GetRequiredService<NotificacionesService>();

            var total = ExtraerValor(output, "Total licitaciones:");
            var conActa = ExtraerValor(output, "Con Acta:");
            var sinActa = ExtraerValor(output, "Sin Acta:");
            var conError = ExtraerValor(output, "Con error:");

            if (exitCode == 0)
            {
                await notificaciones.CrearAsync(
                    usuarioId: "00000000-0000-0000-0000-000000000000",
                    tipo: "scraper_completado",
                    titulo: "Scraper completado",
                    mensaje: $"Se procesaron {total} licitaciones. Actas descargadas: {conActa}. Sin acta: {sinActa}. Errores: {conError}.",
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