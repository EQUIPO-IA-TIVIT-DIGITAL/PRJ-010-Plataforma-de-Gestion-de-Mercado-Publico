using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MPM.Modules.Alertas.Controllers;
using MPM.Modules.Alertas.Services;
using Xunit;

namespace MPM.Modules.Alertas.Tests.Controllers;

/// <summary>Cubre QA BUG-009: el webhook de Telegram era fail-open si el secret no estaba
/// configurado. La ruta feliz (secret correcto, mensaje válido) requiere AlertasService real
/// (DB); se deja fuera de alcance de este test unitario — el fail-closed no necesita llegar
/// a tocar `service` porque debe cortar antes.</summary>
public class TelegramWebhookControllerTests
{
    private static TelegramWebhookController CreateController(string? secretConfigurado, string? secretRecibido)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Telegram:WebhookSecret"] = secretConfigurado,
        }).Build();

        var controller = new TelegramWebhookController(null!, null!, null!, config, NullLogger<TelegramWebhookController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        if (secretRecibido != null)
            controller.ControllerContext.HttpContext.Request.Headers["X-Telegram-Bot-Api-Secret-Token"] = secretRecibido;

        return controller;
    }

    [Fact]
    public async Task Webhook_SinSecretConfigurado_RechazaAunqueNoVengaCabecera()
    {
        var controller = CreateController(secretConfigurado: null, secretRecibido: null);

        var result = await controller.Webhook(JsonDocument.Parse("{}").RootElement);

        result.Should().BeOfType<UnauthorizedResult>("un secret sin configurar debe rechazar, no omitir la validación (QA BUG-009)");
    }

    [Fact]
    public async Task Webhook_ConSecretConfiguradoYSinCabecera_Rechaza()
    {
        var controller = CreateController(secretConfigurado: "el-secreto-real", secretRecibido: null);

        var result = await controller.Webhook(JsonDocument.Parse("{}").RootElement);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Webhook_ConCabeceraIncorrecta_Rechaza()
    {
        var controller = CreateController(secretConfigurado: "el-secreto-real", secretRecibido: "otro-valor");

        var result = await controller.Webhook(JsonDocument.Parse("{}").RootElement);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Webhook_ConCabeceraCorrectaYSinMensaje_NoRechaza()
    {
        var controller = CreateController(secretConfigurado: "el-secreto-real", secretRecibido: "el-secreto-real");

        var result = await controller.Webhook(JsonDocument.Parse("{}").RootElement);

        result.Should().BeOfType<OkResult>("con la credencial correcta, un update sin 'message' se ignora con 200 (contrato de Telegram), no se rechaza");
    }

    /// <summary>024-inteligencia-competencia-alertas / US2: el fail-closed de BUG-009 debe cubrir
    /// también los updates de tipo callback_query (botón "Me interesa"), no solo los mensajes de texto.</summary>
    [Fact]
    public async Task Webhook_CallbackQuery_ConCabeceraIncorrecta_Rechaza()
    {
        var controller = CreateController(secretConfigurado: "el-secreto-real", secretRecibido: "otro-valor");

        var payload = JsonDocument.Parse("""{"callback_query":{"data":"interesa:1","message":{"chat":{"id":123}}}}""").RootElement;
        var result = await controller.Webhook(payload);

        result.Should().BeOfType<UnauthorizedResult>("el fail-closed de BUG-009 debe aplicar por igual a callback_query, no solo a mensajes de texto");
    }
}
