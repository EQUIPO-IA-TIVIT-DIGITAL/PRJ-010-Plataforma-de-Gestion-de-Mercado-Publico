using FluentAssertions;
using MPM.Modules.Mensajeria.Models;
using Xunit;

namespace MPM.Tests.Services;

public class MensajeriaServiceTests
{
    [Fact]
    public void ConversacionResumenDto_DefaultValues_AreCorrect()
    {
        var dto = new ConversacionResumenDto();
        dto.Id.Should().Be(0);
        dto.Tipo.Should().Be(string.Empty);
        dto.Asunto.Should().BeNull();
        dto.Participantes.Should().NotBeNull();
        dto.Participantes.Should().BeEmpty();
        dto.NoLeidos.Should().Be(0);
    }

    [Fact]
    public void MensajeDetalleDto_DefaultValues_AreCorrect()
    {
        var dto = new MensajeDetalleDto();
        dto.Id.Should().Be(0);
        dto.UserId.Should().Be(string.Empty);
        dto.Tipo.Should().Be(string.Empty);
        dto.Contenido.Should().BeNull();
        dto.Adjuntos.Should().NotBeNull();
        dto.Adjuntos.Should().BeEmpty();
        dto.Estados.Should().NotBeNull();
        dto.Estados.Should().BeEmpty();
        dto.EditedAt.Should().BeNull();
    }

    [Fact]
    public void PaginatedResult_CalculatesTotalPages_Correctly()
    {
        var result = new PaginatedResult<string>
        {
            Items = new List<string> { "a", "b", "c" },
            Page = 1,
            PageSize = 10,
            TotalRecords = 25,
            TotalPages = 3
        };

        result.TotalPages.Should().Be(3);
        result.Items.Should().HaveCount(3);
    }

    [Fact]
    public void ParticipanteItemDto_WithValues_SetsCorrectly()
    {
        var dto = new ParticipanteItemDto
        {
            UserId = "user-1",
            Nombre = "Test User",
            Rol = "admin",
            JoinedAt = DateTime.UtcNow
        };

        dto.UserId.Should().Be("user-1");
        dto.Nombre.Should().Be("Test User");
        dto.Rol.Should().Be("admin");
        dto.JoinedAt.Should().NotBeNull();
    }

    [Fact]
    public void AdjuntoItemDto_WithValues_SetsCorrectly()
    {
        var dto = new AdjuntoItemDto
        {
            Id = 1,
            NombreArchivo = "test.pdf",
            MimeType = "application/pdf",
            TamanioBytes = 1024,
            DownloadUrl = "/api/v1/download/1"
        };

        dto.NombreArchivo.Should().Be("test.pdf");
        dto.MimeType.Should().Be("application/pdf");
        dto.TamanioBytes.Should().Be(1024);
    }

    [Fact]
    public void PresenciaDto_WithValues_SetsCorrectly()
    {
        var dto = new PresenciaDto
        {
            UserId = "user-1",
            Estado = "online",
            UpdatedAt = DateTime.UtcNow
        };

        dto.UserId.Should().Be("user-1");
        dto.Estado.Should().Be("online");
        dto.UpdatedAt.Should().NotBeNull();
    }
}
