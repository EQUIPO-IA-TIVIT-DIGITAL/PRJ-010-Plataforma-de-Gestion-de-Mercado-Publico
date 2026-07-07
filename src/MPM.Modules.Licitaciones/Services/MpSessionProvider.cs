using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPM.Modules.Licitaciones.Models;
using StackExchange.Redis;

namespace MPM.Modules.Licitaciones.Services;

/// <summary>
/// Obtiene y cachea las cookies de sesión del portal de Mercado Público, reutilizando el
/// login Node/Playwright existente (<c>tools/scraper-mp/exportar-sesion.js</c>) en vez de
/// reimplementar el flujo de Keycloak/Heimdall en C#. Ver research.md R2 de
/// 016-extraccion-documentos-api.
/// </summary>
public class MpSessionProvider(
    ILogger<MpSessionProvider> logger,
    IConfiguration config,
    IConnectionMultiplexer redis)
{
    private const string RedisKey = "mpm:extraccion:sesion";
    private const string RedisLockKey = "mpm:extraccion:sesion:lock";
    private const string NodeBinary = "node";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<MpSession> ObtenerSesionAsync(bool forzarRenovacion = false, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();

        if (!forzarRenovacion)
        {
            var cacheada = await db.StringGetAsync(RedisKey);
            if (cacheada.HasValue)
            {
                var sesion = Deserializar(cacheada!);
                if (sesion != null) return sesion;
            }
        }

        return await RenovarConLockAsync(db, ct);
    }

    private async Task<MpSession> RenovarConLockAsync(IDatabase db, CancellationToken ct)
    {
        var lockValue = Guid.NewGuid().ToString("N");
        var tomoLock = await db.LockTakeAsync(RedisLockKey, lockValue, TimeSpan.FromMinutes(3));

        if (!tomoLock)
        {
            // Otra instancia ya está renovando: esperar y reintentar leer del cache
            // en vez de disparar un segundo login concurrente contra el portal.
            logger.LogInformation("Renovación de sesión ya en curso por otro proceso, esperando...");
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            var cacheada = await db.StringGetAsync(RedisKey);
            var sesion = cacheada.HasValue ? Deserializar(cacheada!) : null;
            if (sesion != null) return sesion;

            // Si tras esperar sigue sin haber sesión válida, se renueva de todas formas
            // (más seguro que fallar el ciclo completo de extracción).
        }

        try
        {
            var nuevaSesion = await EjecutarLoginNodeAsync(ct);

            var ttlHoras = config.GetValue("Extraccion:SesionTtlHoras", 6);
            var payload = Serializar(nuevaSesion);
            await db.StringSetAsync(RedisKey, payload, TimeSpan.FromHours(ttlHoras));

            return nuevaSesion;
        }
        finally
        {
            if (tomoLock)
                await db.LockReleaseAsync(RedisLockKey, lockValue);
        }
    }

    private async Task<MpSession> EjecutarLoginNodeAsync(CancellationToken ct)
    {
        var scriptPath = config["Extraccion:ExportarSesionScriptPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "tools", "scraper-mp", "exportar-sesion.js");

        if (!Path.IsPathRooted(scriptPath))
        {
            var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            if (!Directory.Exists(baseDir))
                baseDir = Directory.GetCurrentDirectory();
            scriptPath = Path.GetFullPath(Path.Combine(baseDir, scriptPath));
        }

        if (!File.Exists(scriptPath))
            throw new InvalidOperationException($"Script de exportación de sesión no encontrado: {scriptPath}");

        var startInfo = new ProcessStartInfo
        {
            FileName = NodeBinary,
            Arguments = $"\"{scriptPath}\"",
            WorkingDirectory = Path.GetDirectoryName(scriptPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        startInfo.EnvironmentVariables["MP_HEADLESS"] = "true";
        startInfo.EnvironmentVariables["MP_RUT"] = config["MP_RUT"] ?? "";
        startInfo.EnvironmentVariables["MP_PASSWORD"] = config["MP_PASSWORD"] ?? "";

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (!string.IsNullOrWhiteSpace(stderr))
            logger.LogInformation("[exportar-sesion] {Stderr}", stderr.Trim());

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            throw new InvalidOperationException($"exportar-sesion.js terminó con código {process.ExitCode}");

        var cookiesJson = JsonSerializer.Deserialize<List<CookieJson>>(stdout, JsonOptions)
            ?? throw new InvalidOperationException("No se pudo parsear la salida de exportar-sesion.js");

        var container = new CookieContainer();
        foreach (var c in cookiesJson)
        {
            try
            {
                container.Add(new Cookie(c.Name, c.Value, c.Path ?? "/", c.Domain?.TrimStart('.') ?? "www.mercadopublico.cl"));
            }
            catch (Exception ex)
            {
                // Una cookie individual mal formada no debe tumbar toda la sesión
                logger.LogWarning(ex, "No se pudo agregar la cookie {Name} al CookieContainer", c.Name);
            }
        }

        return new MpSession { Cookies = container, ObtenidaEn = DateTime.UtcNow };
    }

    private static string Serializar(MpSession sesion)
    {
        var cookies = sesion.Cookies.GetAllCookies()
            .Cast<Cookie>()
            .Select(c => new CookieJson { Name = c.Name, Value = c.Value, Domain = c.Domain, Path = c.Path });
        return JsonSerializer.Serialize(new { obtenidaEn = sesion.ObtenidaEn, cookies });
    }

    private static MpSession? Deserializar(string payload)
    {
        try
        {
            var doc = JsonSerializer.Deserialize<CachedSessionJson>(payload, JsonOptions);
            if (doc == null) return null;

            var container = new CookieContainer();
            foreach (var c in doc.Cookies)
                container.Add(new Cookie(c.Name, c.Value, c.Path ?? "/", c.Domain?.TrimStart('.') ?? "www.mercadopublico.cl"));

            return new MpSession { Cookies = container, ObtenidaEn = doc.ObtenidaEn };
        }
        catch
        {
            return null;
        }
    }

    private class CookieJson
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
        public string? Domain { get; set; }
        public string? Path { get; set; }
    }

    private class CachedSessionJson
    {
        public DateTime ObtenidaEn { get; set; }
        public List<CookieJson> Cookies { get; set; } = [];
    }
}
