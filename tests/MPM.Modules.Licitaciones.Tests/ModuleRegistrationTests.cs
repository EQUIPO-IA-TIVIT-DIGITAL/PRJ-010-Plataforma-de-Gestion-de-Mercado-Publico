using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FluentAssertions;
using MPM.Modules.Licitaciones;
using Xunit;

namespace MPM.Modules.Licitaciones.Tests;

/// <summary>
/// Cubre QA BUG-004: SyncEngineService/ScraperBackgroundService/AclaracionMonitorService se
/// registraban como IHostedService incondicionalmente, duplicando el trabajo de los Cloud Run
/// Jobs dedicados. Inspecciona los ServiceDescriptor directamente (sin construir el provider)
/// para no arrastrar el resto de la composición de dependencias (DbConnectionFactory,
/// IStorageService, etc. que solo se registran en Program.cs).
/// </summary>
public class ModuleRegistrationTests
{
    private static IConfiguration BuildConfig(bool? runInProcessWorkers)
    {
        var dict = new Dictionary<string, string?>();
        if (runInProcessWorkers.HasValue)
            dict["RUN_INPROCESS_WORKERS"] = runInProcessWorkers.Value.ToString();

        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void AddLicitacionModule_ConRunInProcessWorkersFalse_NoRegistraHostedServices()
    {
        var services = new ServiceCollection();
        var config = BuildConfig(runInProcessWorkers: false);

        services.AddLicitacionModule(config);

        services.Count(sd => sd.ServiceType == typeof(IHostedService)).Should().Be(0,
            "con RUN_INPROCESS_WORKERS=false el servicio web no debe correr sync/scraper/aclaraciones in-process (duplica los Cloud Run Jobs)");
    }

    [Fact]
    public void AddLicitacionModule_ConRunInProcessWorkersTrue_RegistraLosTresHostedServices()
    {
        var services = new ServiceCollection();
        var config = BuildConfig(runInProcessWorkers: true);

        services.AddLicitacionModule(config);

        services.Count(sd => sd.ServiceType == typeof(IHostedService)).Should().Be(3,
            "SyncEngineService, ScraperBackgroundService y AclaracionMonitorService deben registrarse cuando el gate está explícitamente en true");
    }

    [Fact]
    public void AddLicitacionModule_SinConfigurarLaVariable_RegistraLosTresHostedServices()
    {
        // Default seguro: Docker Compose local no setea RUN_INPROCESS_WORKERS, y el
        // comportamiento actual (workers in-process) no debe romperse por default.
        var services = new ServiceCollection();
        var config = BuildConfig(runInProcessWorkers: null);

        services.AddLicitacionModule(config);

        services.Count(sd => sd.ServiceType == typeof(IHostedService)).Should().Be(3,
            "sin la variable configurada, el default debe preservar el comportamiento actual de Docker Compose local");
    }
}
