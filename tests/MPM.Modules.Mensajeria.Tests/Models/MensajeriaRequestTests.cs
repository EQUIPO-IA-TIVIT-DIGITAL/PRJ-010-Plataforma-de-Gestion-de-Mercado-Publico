using MPM.Modules.Mensajeria.Controllers;
using FluentAssertions;
using Xunit;

namespace MPM.Modules.Mensajeria.Tests.Models;

public class MensajeriaRequestTests
{
    [Fact]
    public void CrearConversacionRequest_Defaults()
    {
        var req = new CrearConversacionRequest();
        req.Tipo.Should().BeEmpty();
        req.Asunto.Should().BeNull();
        req.LicitacionId.Should().BeNull();
        req.ParticipanteIds.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void CrearConversacionRequest_WithValues()
    {
        var req = new CrearConversacionRequest
        {
            Tipo = "directo",
            Asunto = "Re: Licitación 12345",
            LicitacionId = 12345,
            ParticipanteIds = ["user-2", "user-3"]
        };
        req.Tipo.Should().Be("directo");
        req.Asunto.Should().Be("Re: Licitación 12345");
        req.LicitacionId.Should().Be(12345);
        req.ParticipanteIds.Should().HaveCount(2);
    }

    [Fact]
    public void ActualizarConversacionRequest_Defaults()
    {
        var req = new ActualizarConversacionRequest();
        req.Asunto.Should().BeEmpty();
    }

    [Fact]
    public void AgregarParticipanteRequest_Defaults()
    {
        var req = new AgregarParticipanteRequest();
        req.UserId.Should().BeEmpty();
        req.Rol.Should().Be("miembro");
    }

    [Fact]
    public void EnviarMensajeRequest_Defaults()
    {
        var req = new EnviarMensajeRequest();
        req.Tipo.Should().Be("texto");
        req.Contenido.Should().BeNull();
        req.ReplyToId.Should().BeNull();
    }

    [Fact]
    public void EnviarMensajeRequest_WithValues()
    {
        var req = new EnviarMensajeRequest
        {
            Tipo = "texto",
            Contenido = "Hola mundo",
            ReplyToId = 42
        };
        req.Tipo.Should().Be("texto");
        req.Contenido.Should().Be("Hola mundo");
        req.ReplyToId.Should().Be(42);
    }

    [Fact]
    public void EditarMensajeRequest_Defaults()
    {
        var req = new EditarMensajeRequest();
        req.Contenido.Should().BeEmpty();
    }

    [Fact]
    public void TypingRequest_Defaults()
    {
        var req = new TypingRequest();
        req.ConversacionId.Should().Be(0);
        req.Escribiendo.Should().BeFalse();
    }

    [Theory]
    [InlineData("directo", true)]
    [InlineData("grupal", true)]
    [InlineData("", false)]
    public void CrearConversacionRequest_TipoValidation(string tipo, bool expectedValid)
    {
        var req = new CrearConversacionRequest { Tipo = tipo };
        var isValid = !string.IsNullOrEmpty(req.Tipo);
        isValid.Should().Be(expectedValid);
    }
}