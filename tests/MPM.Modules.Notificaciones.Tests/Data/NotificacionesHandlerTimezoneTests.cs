using FluentAssertions;
using MPM.Core.Data;
using MPM.Modules.Notificaciones.Data;
using Xunit;

namespace MPM.Modules.Notificaciones.Tests.Data;

/// <summary>Cubre 030-qol-frontend-y-fix-scraper US2/FR-004/FR-005: notificaciones.created_at es
/// TIMESTAMP sin zona horaria, Npgsql lo mapea con Kind=Unspecified y System.Text.Json lo
/// serializaba sin offset — el navegador lo interpretaba como hora local y desfasaba la
/// notificación. Corre contra el Postgres real de docker-compose (localhost:5433), mismo
/// patrón que <see cref="LicitacionHandlerListarFechaTests"/> en MPM.Modules.Licitaciones.Tests.</summary>
public class NotificacionesHandlerTimezoneTests
{
    private const string TestConnectionString =
        "Host=localhost;Port=5433;Database=mpm;Username=mpm;Password=mpm_password";

    private static NotificacionesHandler BuildHandler() => new(new DbConnectionFactory(TestConnectionString));

    [Fact]
    public async Task ListarAsync_MarcaCreatedAtComoUtc()
    {
        var handler = BuildHandler();
        var (crearId, crearError) = await handler.CrearAsync(
            usuarioId: "test-user-timezone",
            tipo: "scraper_completado",
            titulo: "Test timezone",
            mensaje: "Notificacion de prueba para verificar DateTimeKind.Utc",
            metadataJson: null);

        crearError.Should().BeNull();
        crearId.Should().BeGreaterThan(0);

        var (items, _) = await handler.ListarAsync("test-user-timezone", page: 1, pageSize: 5);

        items.Should().NotBeEmpty();
        items.Should().OnlyContain(i => i.CreatedAt.Kind == DateTimeKind.Utc,
            "el frontend depende de que CreatedAt venga marcado como UTC explicito para convertirlo a America/Santiago sin desfase");
    }
}
