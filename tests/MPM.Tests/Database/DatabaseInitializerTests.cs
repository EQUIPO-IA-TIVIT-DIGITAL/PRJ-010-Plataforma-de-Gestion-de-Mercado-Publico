using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MPM.Api.Database;
using MPM.Core.Data;
using Xunit;

namespace MPM.Tests.Database;

/// <summary>
/// Cubre QA BUG-001: DatabaseInitializer debía "tragarse" el fallo de una migración y arrancar
/// igual con el esquema a medias, y no coordinaba instancias concurrentes. Corre contra el
/// Postgres real de docker-compose (localhost:5433) — no usa mocks para DbConnectionFactory
/// porque envuelve NpgsqlConnection directamente, sin interfaz.
/// </summary>
public class DatabaseInitializerTests
{
    private const string TestConnectionString =
        "Host=localhost;Port=5433;Database=mpm;Username=mpm;Password=mpm_password";

    private static DatabaseInitializer CreateInitializer() =>
        new(NullLogger<DatabaseInitializer>.Instance, new DbConnectionFactory(TestConnectionString));

    [Fact]
    public async Task InitializeAsync_ConMigracionesYaAplicadas_NoLanzaExcepcion()
    {
        // Regresión de comportamiento normal: con todas las migraciones reales ya aplicadas
        // (V001-V091 en este entorno), el nuevo pg_advisory_lock + throw no debe romper el
        // arranque de todos los días.
        var initializer = CreateInitializer();

        var act = async () => await initializer.InitializeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InitializeAsync_DosInstanciasConcurrentes_NoSeBloqueanNiFallan()
    {
        // Verifica el pg_advisory_lock agregado para BUG-001: si dos instancias arrancan a la
        // vez (Cloud Run con min-instances>1), pg_advisory_lock serializa el acceso al ciclo de
        // migraciones en vez de dejar que compitan sin coordinación. Con todas las migraciones
        // ya aplicadas no hay nada que aplicar dos veces, pero si el lock/unlock estuviera mal
        // implementado (p. ej. adquirido en una conexión y liberado en otra) esto se colgaría
        // hasta el timeout del test en vez de completar.
        var initializerA = CreateInitializer();
        var initializerB = CreateInitializer();

        var act = async () => await Task.WhenAll(
            initializerA.InitializeAsync(),
            initializerB.InitializeAsync());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void SourceCode_CatchDeMigracionFallida_PropagaLaExcepcion()
    {
        // Guarda de regresión para BUG-001: no se puede forzar una migración inválida a través
        // del mecanismo real (los scripts se compilan como recursos embebidos desde
        // Database/Scripts/, y agregar uno inválido ahí contaminaría las migraciones de
        // producción) — se verifica en el código fuente que el catch del ciclo de migraciones
        // ya no se traga la excepción en silencio, sino que hace throw.
        var sourcePath = FindSourceFile("DatabaseInitializer.cs");
        // Normaliza saltos de línea: git puede reescribir CRLF/LF entre checkouts (core.autocrlf),
        // y buscar una secuencia con \n literal es frágil ante eso.
        var source = File.ReadAllText(sourcePath).Replace("\r\n", "\n");

        source.Should().Contain("pg_advisory_lock", "el arranque debe coordinar instancias concurrentes");
        source.Should().Contain("pg_advisory_unlock", "el lock debe liberarse explícitamente");

        var catchIndex = source.IndexOf("logger.LogError(ex, \"Migration {Version} failed.\", version);", StringComparison.Ordinal);
        catchIndex.Should().BeGreaterThanOrEqualTo(0, "debe existir el log de fallo de migración con el mensaje esperado");

        var afterCatch = source[catchIndex..(catchIndex + 400)];
        afterCatch.Should().Contain("throw;", "el catch de una migración fallida ya no debe tragarse la excepción");
    }

    private static string FindSourceFile(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MPM.sln")))
            dir = dir.Parent;

        if (dir == null) throw new FileNotFoundException("No se encontró MPM.sln subiendo desde el directorio de test.");

        var matches = Directory.GetFiles(dir.FullName, fileName, SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();

        return matches.Single();
    }
}
