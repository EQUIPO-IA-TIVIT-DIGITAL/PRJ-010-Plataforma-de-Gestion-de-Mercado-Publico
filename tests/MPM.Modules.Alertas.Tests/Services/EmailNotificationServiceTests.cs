using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using FluentAssertions;
using MPM.Modules.Alertas.Services;
using MPM.Shared.Services;
using Xunit;

namespace MPM.Modules.Alertas.Tests.Services;

// 032-mejora-alertas-correo (US2): valida el contrato de contracts/correo-alerta-formato.md --
// cada campo enriquecido se omite prolijamente cuando no hay dato (FR-006), sin requerir un
// envio SMTP real para probarlo.
public class EmailNotificationServiceTests
{
    private static (EmailNotificationService Service, Mock<IEmailService> Mock) CrearServicio()
    {
        var mock = new Mock<IEmailService>();
        mock.Setup(m => m.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        var service = new EmailNotificationService(mock.Object, NullLogger<EmailNotificationService>.Instance);
        return (service, mock);
    }

    [Fact]
    public async Task EnviarAsync_ConTodosLosCamposDisponibles_IncluyeOrganismoFechaCierreYLink()
    {
        var (service, mock) = CrearServicio();
        string? htmlEnviado = null;
        mock.Setup(m => m.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, html) => htmlEnviado = html)
            .Returns(Task.CompletedTask);

        await service.EnviarAsync(
            "user@tivit.cl", "TI", "Servicio de soporte TI", "1234-56-LE26", "45.000.000",
            organismo: "Servicio de Impuestos Internos", fechaCierre: new DateTime(2026, 8, 15),
            link: "https://www.mercadopublico.cl/ficha/123", descripcion: "Soporte de mesa de ayuda 24/7");

        htmlEnviado.Should().Contain("Servicio de Impuestos Internos");
        htmlEnviado.Should().Contain("15-08-2026");
        htmlEnviado.Should().Contain("https://www.mercadopublico.cl/ficha/123");
        htmlEnviado.Should().Contain("45.000.000");
        htmlEnviado.Should().Contain("Soporte de mesa de ayuda 24/7");
    }

    [Fact]
    public async Task EnviarAsync_SinFechaCierreNiOrganismoNiLink_OmiteEsosBloquesSinRomperElCorreo()
    {
        var (service, mock) = CrearServicio();
        string? htmlEnviado = null;
        mock.Setup(m => m.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, html) => htmlEnviado = html)
            .Returns(Task.CompletedTask);

        var (enviada, error) = await service.EnviarAsync(
            "user@tivit.cl", "TI", "Servicio de soporte TI", "1234-56-LE26", presupuesto: null);

        enviada.Should().BeTrue();
        error.Should().BeNull();
        htmlEnviado.Should().Contain("Servicio de soporte TI");
        htmlEnviado.Should().NotContain("Organismo:");
        htmlEnviado.Should().NotContain("Cierra:");
        htmlEnviado.Should().NotContain("Presupuesto:");
        htmlEnviado.Should().NotContain("Ver ficha en Mercado Público");
    }
}
