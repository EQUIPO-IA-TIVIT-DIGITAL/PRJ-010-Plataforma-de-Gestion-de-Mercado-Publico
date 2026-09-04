using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPM.Modules.Censo.Models;

namespace MPM.Modules.Censo.Services;

/// <summary>
/// Cliente HTTP de la API de Census (D7.1–D7.11): auth <c>POST /external-auth/token</c> con
/// <c>{Username, Password}</c> (PascalCase, verificado en vivo), búsquedas por tecnología y
/// certificación (substring, sin país por defecto — D7.11), catálogo <c>census/knowledge</c> y
/// descarga de archivos de certificación. Registrado vía <c>AddHttpClient</c> (timeout 100 s).
///
/// Todas las llamadas autenticadas pasan por <see cref="CensusTokenManager"/>; ante un 401
/// (token invalidado antes de expirar — BUG-023) se invalida el token y se reintenta 1 vez.
/// </summary>
public class CensusClient(
    HttpClient http,
    IConfiguration config,
    CensusTokenManager tokenManager,
    ILogger<CensusClient> logger)
{
    private string BaseUrl => (config["Censo:Url"] ?? "").TrimEnd('/');

    /// <summary>Autenticación de servicio: POST /external-auth/token → {accessToken, securityToken}.</summary>
    public virtual async Task<(string Access, string Security)> AuthenticateAsync(CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            Username = config["Censo:Username"] ?? "",
            Password = config["Censo:Password"] ?? "",
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/external-auth/token")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        logger.LogInformation("Autenticando contra Census ({BaseUrl})", BaseUrl);
        using var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var errorBody = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Census respondió {(int)resp.StatusCode} en external-auth/token: {errorBody}");
        }

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var access = root.TryGetProperty("accessToken", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() : null;
        var security = root.TryGetProperty("securityToken", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
        if (string.IsNullOrWhiteSpace(access))
            throw new HttpRequestException("Census no devolvió accessToken en external-auth/token");

        return (access!, security ?? "");
    }

    /// <summary>Personas por tecnología: GET /services/knowledge/technologies/users?technologyName=X[&workCountry=Y].</summary>
    public virtual async Task<List<JsonElement>> GetUsersByTechnologyAsync(string tecnologia, string? pais, CancellationToken ct = default)
        => await GetUsersAsync("technologies/users", "technologyName", tecnologia, pais, ct);

    /// <summary>Personas por certificación (substring): GET /services/knowledge/certifications/users?certificationName=X[&workCountry=Y].</summary>
    public virtual async Task<List<JsonElement>> GetUsersByCertificationAsync(string certificacion, string? pais, CancellationToken ct = default)
        => await GetUsersAsync("certifications/users", "certificationName", certificacion, pais, ct);

    /// <summary>Catálogo de conocimiento completo (grupo→categoría→type→knowledge): GET /census/knowledge → JSON crudo.</summary>
    public virtual async Task<string> GetCatalogoKnowledgeAsync(CancellationToken ct = default)
    {
        using var resp = await EnviarConRetryAsync("/census/knowledge", ct);
        return await resp.Content.ReadAsStringAsync(ct);
    }

    /// <summary>Archivo de certificación: GET /services/knowledge/certifications/file/{fileId} → bytes (D7.5, Fase 3).</summary>
    public virtual async Task<byte[]> DownloadCertificationFileAsync(string fileId, CancellationToken ct = default)
    {
        using var resp = await EnviarConRetryAsync($"/services/knowledge/certifications/file/{Uri.EscapeDataString(fileId)}", ct);
        return await resp.Content.ReadAsByteArrayAsync(ct);
    }

    /// <summary>
    /// Lee la fuente de archivos de certificaciones de Fase 3. Sólo devuelve la proyección
    /// necesaria para sincronizar el catálogo; no expone el payload crudo ni nombres/emails.
    /// </summary>
    public virtual async Task<List<CensusCertificationRecord>> GetUserCertificationsAsync(CancellationToken ct = default)
    {
        using var resp = await EnviarConRetryAsync("/intellectual-capital/user-certifications", ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParseUserCertifications(json);
    }

    internal static List<CensusCertificationRecord> ParseUserCertifications(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in new[] { "data", "items", "results" })
                if (root.TryGetProperty(property, out var nested) && nested.ValueKind == JsonValueKind.Array)
                {
                    root = nested;
                    break;
                }
        }
        if (root.ValueKind != JsonValueKind.Array) return [];

        var result = new List<CensusCertificationRecord>();
        foreach (var item in root.EnumerateArray())
        {
            var certification = GetString(item, "certificationTypeName")
                ?? GetString(item, "certificationName");
            if (string.IsNullOrWhiteSpace(certification)) continue;

            result.Add(new CensusCertificationRecord(
                certification,
                GetFileId(item),
                GetString(item, "institution") ?? GetString(item, "institutionName") ?? GetString(item, "issuingOrganization"),
                GetString(item, "validity") ?? GetString(item, "vigencia") ?? GetString(item, "validUntil") ?? GetString(item, "expirationDate"),
                GetString(item, "userId") ?? GetString(item, "userID"),
                GetString(item, "corporateId") ?? GetString(item, "corporateID")));
        }
        return result;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────────

    private async Task<List<JsonElement>> GetUsersAsync(string path, string paramName, string valor, string? pais, CancellationToken ct)
    {
        var query = $"{paramName}={Uri.EscapeDataString(valor)}";
        if (!string.IsNullOrWhiteSpace(pais))
            query += $"&workCountry={Uri.EscapeDataString(pais)}";

        using var resp = await EnviarConRetryAsync($"/services/knowledge/{path}?{query}", ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(json)) return new List<JsonElement>();

        try
        {
            return JsonSerializer.Deserialize<List<JsonElement>>(json) ?? new List<JsonElement>();
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Respuesta JSON inválida de Census en {Path}", path);
            throw new HttpRequestException($"Census devolvió una respuesta JSON inválida en {path}");
        }
    }

    /// <summary>
    /// GET autenticado con retry ante 401 (1 reintento — BUG-023): renueva el token vía
    /// <see cref="CensusTokenManager"/> y reintenta. Devuelve la respuesta exitosa.
    /// </summary>
    private async Task<HttpResponseMessage> EnviarConRetryAsync(string ruta, CancellationToken ct)
    {
        var (access, security) = await tokenManager.GetTokensAsync(AuthenticateAsync, ct);
        var req = CrearRequest(HttpMethod.Get, $"{BaseUrl}{ruta}", access, security);
        var resp = await http.SendAsync(req, ct);

        if (resp.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Token invalidado prematuramente por Census → renovar y reintentar 1 vez.
            logger.LogWarning("401 de Census en {Ruta} — renovando token y reintentando (BUG-023)", ruta);
            req.Dispose();
            resp.Dispose();
            await tokenManager.InvalidarAsync(ct);

            var (access2, security2) = await tokenManager.GetTokensAsync(AuthenticateAsync, ct);
            var req2 = CrearRequest(HttpMethod.Get, $"{BaseUrl}{ruta}", access2, security2);
            var resp2 = await http.SendAsync(req2, ct);
            if (!resp2.IsSuccessStatusCode)
            {
                var body = await resp2.Content.ReadAsStringAsync(ct);
                req2.Dispose();
                resp2.Dispose();
                throw new HttpRequestException($"Census respondió {(int)resp2.StatusCode} en {ruta}: {body}");
            }
            req2.Dispose();
            return resp2;
        }

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            req.Dispose();
            resp.Dispose();
            throw new HttpRequestException($"Census respondió {(int)resp.StatusCode} en {ruta}: {body}");
        }

        req.Dispose();
        return resp;
    }

    private static HttpRequestMessage CrearRequest(HttpMethod method, string url, string access, string security)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        // Header verificado en vivo 2026-08-16: el securityToken viaja en "x-security"
        // (el cliente Python de la base PRJ-001 lo usaba así; con "securityToken" Census
        // responde 401 en los endpoints de datos).
        if (!string.IsNullOrWhiteSpace(security))
            req.Headers.TryAddWithoutValidation("x-security", security);
        return req;
    }

    private static string? GetFileId(JsonElement item)
    {
        var direct = GetString(item, "fileId") ?? GetString(item, "fileID");
        if (!string.IsNullOrWhiteSpace(direct)) return direct;
        if (!item.TryGetProperty("file", out var file)) return null;
        if (file.ValueKind == JsonValueKind.String) return file.GetString();
        if (file.ValueKind == JsonValueKind.Object)
            return GetString(file, "fileId") ?? GetString(file, "fileID") ?? GetString(file, "id");
        if (file.ValueKind == JsonValueKind.Array)
            return file.EnumerateArray().Select(x => GetString(x, "fileId") ?? GetString(x, "fileID") ?? GetString(x, "id"))
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        return null;
    }

    private static string? GetString(JsonElement item, string name)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }
}
