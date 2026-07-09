using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MPM.Tests;

/// <summary>Cubre QA BUG-011: CORS abierto a cualquier origen con credenciales, y secreto JWT
/// por defecto embebido si faltaba la configuración.</summary>
public class CorsAndJwtStartupTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CorsAndJwtStartupTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PreflightRequest_DesdeOrigenNoAutorizado_NoDevuelveAccessControlAllowOrigin()
    {
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/licitaciones");
        request.Headers.Add("Origin", "https://evil-site.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse(
            "un origen fuera de la allow-list no debe recibir el header que habilita CORS");
    }

    [Fact]
    public async Task PreflightRequest_DesdeOrigenAutorizado_DevuelveAccessControlAllowOrigin()
    {
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/licitaciones");
        request.Headers.Add("Origin", "http://localhost:8181"); // default de Program.cs si Cors:AllowedOrigins no está configurado
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain("http://localhost:8181");
    }

    [Fact]
    public void SourceCode_ProgramCs_YaNoTieneFallbackDeJwtSecretHardcodeado()
    {
        // Program.cs usa top-level statements: no hay un método invocable de forma aislada
        // para probar "arranca sin JWT:Secret" sin la complejidad/fragilidad de interceptar
        // WebApplicationFactory a nivel de reflection sobre el entry point real. Se verifica
        // en el código fuente que el fallback inseguro (QA BUG-011) fue removido y reemplazado
        // por una validación que aborta el arranque; BUG-001 (misma clase de fix: throw antes
        // de app.Run()) ya se validó de punta a punta contra el contenedor real.
        var source = File.ReadAllText(FindSourceFile("Program.cs", mustContain: "AddLicitacionModule"));

        source.Should().NotContain("default-secret-change-this-in-production",
            "el fallback embebido en el binario debía eliminarse (QA BUG-011)");
        source.Should().Contain("throw new InvalidOperationException",
            "el arranque debe fallar de forma visible si JWT:Secret no está configurado");
        source.Should().Contain("jwtSecret.Length < 32");
    }

    private static string FindSourceFile(string fileName, string mustContain)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MPM.sln")))
            dir = dir.Parent;

        if (dir == null) throw new FileNotFoundException("No se encontró MPM.sln subiendo desde el directorio de test.");

        var candidates = Directory.GetFiles(dir.FullName, fileName, SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        return candidates.Single(p => File.ReadAllText(p).Contains(mustContain));
    }
}
