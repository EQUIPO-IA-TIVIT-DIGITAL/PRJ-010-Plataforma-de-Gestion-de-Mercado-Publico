using System.Text.Json;

namespace MPM.Modules.Censo.Services;

/// <summary>
/// Gestión del token de Census (D7.4 / CEN-R011): cache en memoria del par
/// <c>accessToken</c>/<c>securityToken</c>, expiración derivada del JWT <c>exp</c> con
/// margen de seguridad de 2 minutos (renovar antes), y renovación única concurrente
/// vía <see cref="SemaphoreSlim"/>. Ante un 401 (Census puede invalidar el token antes
/// de expirar — BUG-023), el cliente llama <see cref="InvalidarAsync"/> y reintenta.
///
/// Singleton, sin HTTP propio: la renovación la ejecuta <see cref="CensusClient"/>
/// (que sí tiene el HttpClient tipado) pasando su <c>AuthenticateAsync</c> como delegado —
/// evita la dependencia circular manager → cliente.
/// </summary>
public class CensusTokenManager
{
    private static readonly TimeSpan MargenSeguridad = TimeSpan.FromMinutes(2);
    // CEN-R011: sin `exp` en el JWT → TTL conservador (default 4 min).
    private static readonly TimeSpan TtlConservador = TimeSpan.FromMinutes(4);

    private readonly SemaphoreSlim _sem = new(1, 1);
    private string? _accessToken;
    private string? _securityToken;
    private DateTimeOffset _expiraEn = DateTimeOffset.MinValue;

    /// <summary>
    /// Devuelve el par de tokens vigente; si no es válido (o expira dentro del margen),
    /// renueva exactamente una vez bajo el semáforo (los concurrentes esperan).
    /// </summary>
    /// <param name="renovar">Delegado que ejecuta la autenticación contra Census (provisto por
    /// <see cref="CensusClient.AuthenticateAsync"/>). Solo se invoca cuando hace falta renovar.</param>
    public virtual async Task<(string Access, string Security)> GetTokensAsync(
        Func<CancellationToken, Task<(string Access, string Security)>> renovar,
        CancellationToken ct = default)
    {
        if (Valido()) return (_accessToken!, _securityToken!);

        await _sem.WaitAsync(ct);
        try
        {
            // Double-check: otro request pudo renovar mientras esperábamos el semáforo.
            if (Valido()) return (_accessToken!, _securityToken!);

            var (access, security) = await renovar(ct);
            Guardar(access, security);
            return (access, security);
        }
        finally
        {
            _sem.Release();
        }
    }

    /// <summary>Invalida el token cacheado (llamado ante un 401: token invalidado prematuramente).</summary>
    public virtual async Task InvalidarAsync(CancellationToken ct = default)
    {
        await _sem.WaitAsync(ct);
        try
        {
            _accessToken = null;
            _securityToken = null;
            _expiraEn = DateTimeOffset.MinValue;
        }
        finally
        {
            _sem.Release();
        }
    }

    /// <summary>¿El token cacheado sigue vigente con el margen de seguridad aplicado?</summary>
    private bool Valido()
        => _accessToken != null && DateTimeOffset.UtcNow < _expiraEn;

    private void Guardar(string access, string security)
    {
        _accessToken = access;
        _securityToken = security;
        var exp = DecodificarExp(access);
        _expiraEn = exp.HasValue
            ? exp.Value - MargenSeguridad
            : DateTimeOffset.UtcNow + TtlConservador;
    }

    /// <summary>Decodifica el payload (base64url) de un JWT y extrae <c>exp</c> (segundos Unix).</summary>
    internal static DateTimeOffset? DecodificarExp(string jwt)
    {
        var partes = jwt.Split('.');
        if (partes.Length < 2) return null;

        var payload = partes[1].Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }

        try
        {
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("exp", out var exp) &&
                exp.ValueKind == JsonValueKind.Number &&
                exp.TryGetInt64(out var unix))
            {
                return DateTimeOffset.FromUnixTimeSeconds(unix);
            }
        }
        catch
        {
            // JWT malformado → TTL conservador (renovar antes de tiempo, nunca después).
        }

        return null;
    }
}
