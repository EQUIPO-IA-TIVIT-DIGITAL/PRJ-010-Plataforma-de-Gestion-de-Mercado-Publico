using MPM.Shared.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using Xunit;

namespace MPM.Shared.Tests.Services;

public class SmtpEmailServiceTests
{
    private static SmtpEmailService CreateService(Dictionary<string, string?>? configOverrides = null)
    {
        var configDict = new Dictionary<string, string?>
        {
            ["Smtp:Host"] = null,
            ["Smtp:Port"] = "587",
            ["Smtp:Username"] = null,
            ["Smtp:Password"] = null,
            ["Smtp:FromEmail"] = "noreply@mpm.cl",
            ["Smtp:FromName"] = "MPM Test",
            ["Smtp:EnableSsl"] = "true"
        };

        if (configOverrides != null)
        {
            foreach (var kv in configOverrides)
                configDict[kv.Key] = kv.Value;
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict!)
            .Build();

        var logger = new Mock<ILogger<SmtpEmailService>>();
        return new SmtpEmailService(config, logger.Object);
    }

    [Fact]
    public async Task SendPasswordResetEmail_NoSmtpHost_DoesNotThrow()
    {
        var service = CreateService();
        var act = async () => await service.SendPasswordResetEmailAsync("test@example.com", "https://localhost/reset/abc", "Test User");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendEmailAsync_NoSmtpHost_DoesNotThrow()
    {
        var service = CreateService();
        var act = async () => await service.SendEmailAsync("test@example.com", "Test Subject", "<h1>Test</h1>");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendPasswordResetEmail_NoSmtpHost_LogsWarning()
    {
        var logger = new Mock<ILogger<SmtpEmailService>>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smtp:Host"] = null,
                ["Smtp:FromEmail"] = "noreply@mpm.cl"
            })
            .Build();

        var service = new SmtpEmailService(config, logger.Object);
        await service.SendPasswordResetEmailAsync("user@test.cl", "https://localhost/reset/token123");

        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("SMTP no configurado")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPasswordResetEmail_WithUserName_ContainsDisplayName()
    {
        var service = CreateService();
        var act = async () => await service.SendPasswordResetEmailAsync("user@test.cl", "https://localhost/reset/t", "Maria");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendPasswordResetEmail_WithoutUserName_ContainsEmail()
    {
        var service = CreateService();
        var act = async () => await service.SendPasswordResetEmailAsync("user@test.cl", "https://localhost/reset/t");
        await act.Should().NotThrowAsync();
    }
}