using MPM.Modules.Analisis.Models;
using FluentAssertions;
using Xunit;

namespace MPM.Modules.Analisis.Tests.Models;

public class AnalisisDtosTests
{
    [Fact]
    public void WorkspaceItemDto_ShouldInitializeWithDefaults()
    {
        var dto = new WorkspaceItemDto();
        dto.Id.Should().Be(0);
        dto.LicitacionId.Should().BeNull();
        dto.LicitacionNombre.Should().BeNull();
        dto.Nombre.Should().BeEmpty();
        dto.Estado.Should().BeEmpty();
        dto.DocumentosCount.Should().Be(0);
        dto.UltimoAnalisisId.Should().BeNull();
        dto.UltimoAnalisisFecha.Should().BeNull();
    }

    [Fact]
    public void WorkspaceDetalleDto_ShouldInitializeWithDefaults()
    {
        var dto = new WorkspaceDetalleDto();
        dto.Id.Should().Be(0);
        dto.Nombre.Should().BeEmpty();
        dto.Estado.Should().BeEmpty();
        dto.DocumentosCount.Should().Be(0);
        dto.UltimoAnalisisId.Should().BeNull();
        dto.UltimoAnalisisDocumentoId.Should().BeNull();
        dto.UltimoAnalisisDocumentoNombre.Should().BeNull();
        dto.CreatedAt.Should().Be(default(DateTime));
        dto.UpdatedAt.Should().Be(default(DateTime));
    }

    [Fact]
    public void DocumentoItemDto_ShouldInitializeWithDefaults()
    {
        var dto = new DocumentoItemDto();
        dto.Id.Should().Be(0);
        dto.NombreArchivo.Should().BeEmpty();
        dto.MimeType.Should().BeEmpty();
        dto.TamanioBytes.Should().Be(0);
    }

    [Fact]
    public void DocumentoDetalleDto_ShouldInitializeRutaStorageAsEmpty()
    {
        var dto = new DocumentoDetalleDto();
        dto.RutaStorage.Should().BeEmpty();
        dto.WorkspaceId.Should().Be(0);
    }

    [Fact]
    public void ResultadoDto_ShouldInitializeWithDefaults()
    {
        var dto = new ResultadoDto();
        dto.ContenidoJson.Should().BeNull();
        dto.ModeloUsado.Should().BeEmpty();
        dto.TokensEntrada.Should().Be(0);
        dto.TokensSalida.Should().Be(0);
    }

    [Fact]
    public void ChatMensajeDto_ShouldInitializeWithDefaults()
    {
        var dto = new ChatMensajeDto();
        dto.Rol.Should().BeEmpty();
        dto.Contenido.Should().BeEmpty();
    }

    [Fact]
    public void ChatResponseDto_ShouldInitializeWithEmptyMensajes()
    {
        var dto = new ChatResponseDto();
        dto.Respuesta.Should().BeEmpty();
        dto.ConversacionId.Should().Be(0);
        dto.Mensajes.Should().NotBeNull();
        dto.Mensajes.Should().BeEmpty();
    }

    [Fact]
    public void ChatHistorialDto_ShouldInitializeWithEmptyMensajes()
    {
        var dto = new ChatHistorialDto();
        dto.ConversacionId.Should().Be(0);
        dto.Mensajes.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void CrearWorkspaceRequest_NombreIsEmptyByDefault()
    {
        var req = new CrearWorkspaceRequest();
        req.LicitacionId.Should().BeNull();
        req.Nombre.Should().BeEmpty();
    }

    [Fact]
    public void AnalizarRequest_DocumentoIdIsNullable()
    {
        var req = new AnalizarRequest();
        req.DocumentoId.Should().BeNull();
    }

    [Fact]
    public void ChatRequest_MensajeIsEmptyByDefault()
    {
        var req = new ChatRequest();
        req.Mensaje.Should().BeEmpty();
    }

    [Fact]
    public void PaginatedResult_ShouldInitializeWithEmptyItems()
    {
        var p = new PaginatedResult<WorkspaceItemDto>();
        p.Items.Should().NotBeNull().And.BeEmpty();
        p.Page.Should().Be(0);
        p.PageSize.Should().Be(0);
        p.TotalRecords.Should().Be(0);
        p.TotalPages.Should().Be(0);
    }

    [Fact]
    public void PaginatedResult_AllowsSettingProperties()
    {
        var p = new PaginatedResult<WorkspaceItemDto>
        {
            Page = 1,
            PageSize = 20,
            TotalRecords = 45,
            TotalPages = 3
        };
        p.Page.Should().Be(1);
        p.PageSize.Should().Be(20);
        p.TotalRecords.Should().Be(45);
        p.TotalPages.Should().Be(3);
    }

    [Fact]
    public void AnalisisResumenDto_EstadoIsEmptyByDefault()
    {
        var dto = new AnalisisResumenDto();
        dto.Estado.Should().BeEmpty();
        dto.ModeloUsado.Should().BeNull();
        dto.TokensEntrada.Should().BeNull();
        dto.TokensSalida.Should().BeNull();
    }
}
