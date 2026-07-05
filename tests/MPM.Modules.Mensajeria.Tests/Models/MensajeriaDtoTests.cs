using MPM.Modules.Mensajeria.Models;
using FluentAssertions;
using Xunit;

namespace MPM.Modules.Mensajeria.Tests.Models;

public class MensajeriaDtoTests
{
    [Fact]
    public void ConversacionResumenDto_Defaults()
    {
        var dto = new ConversacionResumenDto();
        dto.Id.Should().Be(0);
        dto.Tipo.Should().BeEmpty();
        dto.Asunto.Should().BeNull();
        dto.LicitacionId.Should().BeNull();
        dto.LicitacionNombre.Should().BeNull();
        dto.Participantes.Should().NotBeNull().And.BeEmpty();
        dto.UltimoMensaje.Should().BeNull();
        dto.NoLeidos.Should().Be(0);
    }

    [Fact]
    public void ConversacionDetalleDto_Defaults()
    {
        var dto = new ConversacionDetalleDto();
        dto.Id.Should().Be(0);
        dto.Tipo.Should().BeEmpty();
        dto.Asunto.Should().BeNull();
        dto.Participantes.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ParticipanteItemDto_WithValues()
    {
        var now = DateTime.UtcNow;
        var dto = new ParticipanteItemDto
        {
            UserId = "user-1",
            Nombre = "Test User",
            Rol = "admin",
            AvatarUrl = "https://example.com/avatar.png",
            JoinedAt = now,
            LeftAt = null
        };
        dto.UserId.Should().Be("user-1");
        dto.Nombre.Should().Be("Test User");
        dto.Rol.Should().Be("admin");
        dto.AvatarUrl.Should().Be("https://example.com/avatar.png");
        dto.JoinedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
        dto.LeftAt.Should().BeNull();
    }

    [Fact]
    public void MensajeResumenDto_Defaults()
    {
        var dto = new MensajeResumenDto();
        dto.Id.Should().Be(0);
        dto.UserId.Should().BeEmpty();
        dto.Tipo.Should().BeEmpty();
        dto.Contenido.Should().BeEmpty();
    }

    [Fact]
    public void MensajeDetalleDto_Defaults()
    {
        var dto = new MensajeDetalleDto();
        dto.Id.Should().Be(0);
        dto.UserId.Should().BeEmpty();
        dto.UserName.Should().BeEmpty();
        dto.Tipo.Should().BeEmpty();
        dto.Contenido.Should().BeNull();
        dto.ReplyTo.Should().BeNull();
        dto.Adjuntos.Should().NotBeNull().And.BeEmpty();
        dto.Estados.Should().NotBeNull().And.BeEmpty();
        dto.EditedAt.Should().BeNull();
    }

    [Fact]
    public void AdjuntoItemDto_WithValues()
    {
        var dto = new AdjuntoItemDto
        {
            Id = 1,
            NombreArchivo = "doc.pdf",
            MimeType = "application/pdf",
            TamanioBytes = 2048,
            DownloadUrl = "/api/v1/conversaciones/1/mensajes/1/adjuntos/1"
        };
        dto.NombreArchivo.Should().Be("doc.pdf");
        dto.MimeType.Should().Be("application/pdf");
        dto.TamanioBytes.Should().Be(2048);
    }

    [Fact]
    public void AdjuntoDetalleDto_Defaults()
    {
        var dto = new AdjuntoDetalleDto();
        dto.Id.Should().Be(0);
        dto.MensajeId.Should().Be(0);
        dto.NombreArchivo.Should().BeEmpty();
        dto.MimeType.Should().BeEmpty();
        dto.TamanioBytes.Should().Be(0);
        dto.RutaStorage.Should().BeEmpty();
    }

    [Fact]
    public void MensajeEstadoDto_Defaults()
    {
        var dto = new MensajeEstadoDto();
        dto.UserId.Should().BeEmpty();
        dto.Estado.Should().BeEmpty();
    }

    [Fact]
    public void PresenciaDto_WithValues()
    {
        var now = DateTime.UtcNow;
        var dto = new PresenciaDto { UserId = "user-1", Estado = "online", UpdatedAt = now };
        dto.UserId.Should().Be("user-1");
        dto.Estado.Should().Be("online");
        dto.UpdatedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void PaginatedResult_CalculatesTotalPages_Correctly()
    {
        var result = new PaginatedResult<string>
        {
            Items = ["a", "b", "c"],
            Page = 1,
            PageSize = 10,
            TotalRecords = 25,
            TotalPages = 3
        };
        result.TotalPages.Should().Be(3);
        result.Items.Should().HaveCount(3);
    }

    [Fact]
    public void PaginatedResult_SinglePage()
    {
        var result = new PaginatedResult<string>
        {
            Items = ["a"],
            Page = 1,
            PageSize = 50,
            TotalRecords = 1,
            TotalPages = 1
        };
        result.TotalPages.Should().Be(1);
    }

    [Fact]
    public void PaginatedResult_EmptyPage()
    {
        var result = new PaginatedResult<ConversacionResumenDto>
        {
            Items = [],
            Page = 1,
            PageSize = 20,
            TotalRecords = 0,
            TotalPages = 0
        };
        result.Items.Should().BeEmpty();
        result.TotalRecords.Should().Be(0);
    }
}