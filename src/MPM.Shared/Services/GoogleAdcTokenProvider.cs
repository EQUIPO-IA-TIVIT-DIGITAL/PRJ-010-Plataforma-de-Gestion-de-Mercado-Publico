using Google.Apis.Auth.OAuth2;

namespace MPM.Shared.Services;

/// <summary>
/// Obtiene tokens de acceso vía Application Default Credentials (ADC) para llamar APIs de
/// Google autenticadas por cuenta/Service Account en vez de API key (020-migracion-gemini-adc).
///
/// En local: usa las credenciales de `gcloud auth application-default login` del desarrollador
/// (montadas en el contenedor, ver docker-compose.yml). En Cloud Run: usa automáticamente la
/// Service Account asociada al servicio/Job (metadata server), sin ningún archivo de
/// credenciales que gestionar.
///
/// `GoogleCredential` cachea y refresca el token internamente — no hay que reimplementar eso.
/// </summary>
public class GoogleAdcTokenProvider
{
    private static readonly string[] Scopes = ["https://www.googleapis.com/auth/cloud-platform"];

    private readonly SemaphoreSlim _lock = new(1, 1);
    private GoogleCredential? _credential;

    // virtual para poder mockear en unit tests (Moq requiere miembros virtuales) sin
    // ejecutar la resolución real de ADC, que requiere credenciales de la máquina.
    public virtual async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var credential = await GetCredentialAsync(ct);
        return await credential.UnderlyingCredential.GetAccessTokenForRequestAsync(cancellationToken: ct);
    }

    private async Task<GoogleCredential> GetCredentialAsync(CancellationToken ct)
    {
        if (_credential != null) return _credential;

        await _lock.WaitAsync(ct);
        try
        {
            _credential ??= (await GoogleCredential.GetApplicationDefaultAsync()).CreateScoped(Scopes);
            return _credential;
        }
        finally
        {
            _lock.Release();
        }
    }
}
