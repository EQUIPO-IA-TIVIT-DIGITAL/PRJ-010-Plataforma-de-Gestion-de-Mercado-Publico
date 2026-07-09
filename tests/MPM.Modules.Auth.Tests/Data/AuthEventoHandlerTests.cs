using FluentAssertions;
using MPM.Core.Data;
using MPM.Modules.Auth.Data;
using Npgsql;
using Xunit;

namespace MPM.Modules.Auth.Tests.Data;

/// <summary>Cubre QA BUG-010: los logins exitosos no dejaban ningún registro auditable. Corre
/// contra el Postgres real de docker-compose (localhost:5433).</summary>
public class AuthEventoHandlerTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=localhost;Port=5433;Database=mpm;Username=mpm;Password=mpm_password";

    private const string TestEmail = "auth-evento-handler-test@example.com";

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM auth_eventos WHERE email = @email", conn);
        cmd.Parameters.AddWithValue("email", TestEmail);
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task RegistrarAsync_InsertaUnaFilaConsultable()
    {
        var handler = new AuthEventoHandler(new DbConnectionFactory(TestConnectionString));

        await handler.RegistrarAsync("42", "tenant-test", TestEmail, "203.0.113.5", "xUnit-test-agent");

        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT user_id, tenant_id, ip_address, user_agent FROM auth_eventos WHERE email = @email", conn);
        cmd.Parameters.AddWithValue("email", TestEmail);
        await using var reader = await cmd.ExecuteReaderAsync();

        (await reader.ReadAsync()).Should().BeTrue("RegistrarAsync debe dejar una fila consultable en auth_eventos");
        reader.GetString(0).Should().Be("42");
        reader.GetString(1).Should().Be("tenant-test");
        reader.GetString(2).Should().Be("203.0.113.5");
        reader.GetString(3).Should().Be("xUnit-test-agent");
    }

    [Fact]
    public async Task RegistrarAsync_ConIpYUserAgentNulos_NoFalla()
    {
        var handler = new AuthEventoHandler(new DbConnectionFactory(TestConnectionString));

        var act = async () => await handler.RegistrarAsync("43", "tenant-test", TestEmail, null, null);

        await act.Should().NotThrowAsync("ip_address y user_agent son nullable — un proxy o un test no siempre los tiene disponibles");
    }
}
