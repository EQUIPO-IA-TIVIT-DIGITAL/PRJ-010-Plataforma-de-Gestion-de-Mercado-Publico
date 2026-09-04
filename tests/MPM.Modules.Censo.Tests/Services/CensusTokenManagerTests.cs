using FluentAssertions;
using MPM.Modules.Censo.Services;
using Xunit;

namespace MPM.Modules.Censo.Tests.Services;

/// <summary>
/// 036-flujo-comercial-ofertas (Fase 2, CEN-R011 / D7.4): contrato de CensusTokenManager —
/// cache en memoria del par access/security con margen de seguridad de 2 min sobre el JWT
/// <c>exp</c>, renovación única concurrente vía SemaphoreSlim e invalidación ante 401 (BUG-023).
/// No llama a Census: el delegado de autenticación se mockea con un contador de invocaciones.
/// </summary>
public class CensusTokenManagerTests
{
    private const string TokenSecurity = "security-token";

    [Fact]
    public async Task GetTokensAsync_TokenFresco_CacheaYNoReautentica()
    {
        var manager = new CensusTokenManager();
        var llamadas = 0;
        var jwt = JwtConExp(DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds());

        Task<(string Access, string Security)> Renovar(CancellationToken _)
        {
            llamadas++;
            return Task.FromResult((jwt, TokenSecurity));
        }

        var (a1, s1) = await manager.GetTokensAsync(Renovar);
        var (a2, s2) = await manager.GetTokensAsync(Renovar);

        llamadas.Should().Be(1, "con el token vigente no se debe volver a autenticar contra Census");
        a2.Should().Be(a1);
        s2.Should().Be(s1);
    }

    [Fact]
    public async Task GetTokensAsync_ExpDentroDelMargenDeSeguridad_Renueva()
    {
        var manager = new CensusTokenManager();
        var llamadas = 0;
        // exp en 1 min → exp − margen (2 min) ya pasó: el token no es válido al segundo pedido.
        var jwt = JwtConExp(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds());

        Task<(string Access, string Security)> Renovar(CancellationToken _)
        {
            llamadas++;
            return Task.FromResult((jwt, TokenSecurity));
        }

        await manager.GetTokensAsync(Renovar);
        var (access, security) = await manager.GetTokensAsync(Renovar);

        llamadas.Should().Be(2, "si el JWT expira dentro del margen de 2 minutos se debe renovar antes de usarlo");
        access.Should().Be(jwt);
        security.Should().Be(TokenSecurity);
    }

    [Fact]
    public async Task InvalidarAsync_FuerzaReautenticacionEnElSiguientePedido()
    {
        var manager = new CensusTokenManager();
        var llamadas = 0;
        var jwt = JwtConExp(DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds());

        Task<(string Access, string Security)> Renovar(CancellationToken _)
        {
            llamadas++;
            return Task.FromResult((jwt, TokenSecurity));
        }

        await manager.GetTokensAsync(Renovar);
        await manager.InvalidarAsync();
        var (access, security) = await manager.GetTokensAsync(Renovar);

        llamadas.Should().Be(2, "InvalidarAsync descarta el cache (Census invalidó el token antes de expirar — BUG-023)");
        access.Should().Be(jwt);
        security.Should().Be(TokenSecurity);
    }

    [Fact]
    public async Task GetTokensAsync_LlamadasConcurrentes_DelegadoSeLlamaUnaVez()
    {
        var manager = new CensusTokenManager();
        var llamadas = 0;
        var jwt = JwtConExp(DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds());

        Task<(string Access, string Security)> RenovarLento(CancellationToken _)
        {
            llamadas++;
            return Task.Run(async () =>
            {
                await Task.Delay(80); // simula la llamada HTTP real de AuthenticateAsync
                return (jwt, TokenSecurity);
            });
        }

        var t1 = manager.GetTokensAsync(RenovarLento);
        var t2 = manager.GetTokensAsync(RenovarLento);
        var pares = await Task.WhenAll(t1, t2);

        llamadas.Should().Be(1, "el SemaphoreSlim debe garantizar una única renovación concurrente (CEN-R011)");
        pares[0].Access.Should().Be(pares[1].Access);
        pares[0].Security.Should().Be(pares[1].Security);
    }

    // ── DecodificarExp (helper interno expuesto vía InternalsVisibleTo) ─────────────

    [Fact]
    public void DecodificarExp_JwtValido_DevuelveLaFechaDelClaimExp()
    {
        var epoch = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();

        CensusTokenManager.DecodificarExp(JwtConExp(epoch)).Should().Be(DateTimeOffset.FromUnixTimeSeconds(epoch));
    }

    [Fact]
    public void DecodificarExp_JwtSinExp_DevuelveNull()
    {
        // payload "{}" (sin exp) y JWT malformado → null → TTL conservador (CEN-R011).
        CensusTokenManager.DecodificarExp(JwtConPayload("{}")).Should().BeNull();
        CensusTokenManager.DecodificarExp("abc.def.ghi").Should().BeNull();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────

    private static string JwtConExp(long expEpoch) => JwtConPayload("{\"exp\":" + expEpoch + "}");

    private static string JwtConPayload(string payloadJson)
        => $"{Base64Url("""{"alg":"none"}""")}.{Base64Url(payloadJson)}.firma";

    private static string Base64Url(string texto)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(texto))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
