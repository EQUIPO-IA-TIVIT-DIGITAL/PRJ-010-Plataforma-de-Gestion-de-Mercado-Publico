using FluentAssertions;
using MPM.Modules.Alertas.Services;
using Xunit;

namespace MPM.Modules.Alertas.Tests.Services;

/// <summary>Cubre QA BUG-013: nombres de licitación con caracteres reservados de MarkdownV2
/// (`_`, `*`, etc.) hacían que Telegram respondiera 400 y el mensaje nunca se entregara.</summary>
public class TelegramNotificationServiceTests
{
    [Theory]
    [InlineData("Compra_de_equipos", "Compra\\_de\\_equipos")]
    [InlineData("Proyecto * urgente", "Proyecto \\* urgente")]
    [InlineData("Fase 1.2 (final)", "Fase 1\\.2 \\(final\\)")]
    [InlineData("Sin caracteres especiales", "Sin caracteres especiales")]
    public void EscaparMarkdownV2_EscapaCaracteresReservados(string entrada, string esperado)
    {
        TelegramNotificationService.EscaparMarkdownV2(entrada).Should().Be(esperado);
    }

    [Fact]
    public void FormatearMensaje_ConNombreYCodigoConCaracteresEspeciales_NoRompeElFormato()
    {
        var mensaje = TelegramNotificationService.FormatearMensaje(
            keyword: "cloud",
            nombreLicitacion: "Compra_de_equipos * urgente",
            codigoExterno: "750301-5-L124",
            resumen: null);

        mensaje.Should().Contain("Compra\\_de\\_equipos \\* urgente");
        mensaje.Should().Contain("\\(750301\\-5\\-L124\\)");
    }

    [Fact]
    public void SourceCode_TelegramNotificationService_UsaMarkdownV2()
    {
        var source = File.ReadAllText(FindSourceFile("TelegramNotificationService.cs"));
        source.Should().Contain("\"MarkdownV2\"");
        source.Should().NotContain("parse_mode = \"Markdown\"}",
            "no debe quedar el parse_mode viejo (sin escape) en ningún payload");
    }

    [Fact]
    public void SourceCode_HttpClientDeTelegram_TieneTimeoutExplicito()
    {
        var source = File.ReadAllText(FindSourceFile("ModuleRegistration.cs", mustContain: "TelegramNotificationService"));
        source.Should().Contain("c.Timeout = TimeSpan.FromSeconds(10)",
            "el HttpClient de Telegram no debe depender del timeout default de 100s (QA BUG-013)");
    }

    private static string FindSourceFile(string fileName, string? mustContain = null)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MPM.sln")))
            dir = dir.Parent;

        if (dir == null) throw new FileNotFoundException("No se encontró MPM.sln subiendo desde el directorio de test.");

        var candidates = Directory.GetFiles(dir.FullName, fileName, SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && p.Contains("Alertas"));

        return mustContain == null
            ? candidates.Single()
            : candidates.Single(p => File.ReadAllText(p).Contains(mustContain));
    }
}
