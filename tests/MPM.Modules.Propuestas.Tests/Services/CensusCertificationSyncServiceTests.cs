using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPM.Core.Data;
using MPM.Modules.Censo.Models;
using MPM.Modules.Censo.Services;
using MPM.Modules.Propuestas.Data;
using MPM.Modules.Propuestas.Services;
using Xunit;

namespace MPM.Modules.Propuestas.Tests.Services;

public class CensusCertificationSyncServiceTests
{
    [Fact]
    public async Task SincronizarAsync_MultipleUsersSameCertification_UpsertsOneCatalogNameAndKeepsFirstFile()
    {
        var handler = new Mock<PropuestasHandler>(new DbConnectionFactory("Host=unused"));
        handler.Setup(h => h.SincronizarCertificacionesAsync(It.IsAny<IReadOnlyCollection<CertificationSyncItem>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CensusSyncMutationResult { Insertadas = 1, Actualizadas = 0, SinArchivo = 0 });
        var census = new Mock<CensusClient>(
            new HttpClient(), new ConfigurationBuilder().Build(), new CensusTokenManager(),
            NullLogger<CensusClient>.Instance);
        census.Setup(c => c.GetUserCertificationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CensusCertificationRecord>
            {
                new("ISO/IEC 27001", "file-first", "BSI", "vigente", "user-1", "corp-1"),
                new("27001", "file-second", "BSI", null, "user-2", "corp-2"),
            });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Propuestas:CensusSync:MaxRecords"] = "10",
        }).Build();
        var service = new CensusCertificationSyncService(census.Object, handler.Object, config, NullLogger<CensusCertificationSyncService>.Instance);

        var result = await service.SincronizarAsync();

        result.Procesadas.Should().Be(2);
        result.Insertadas.Should().Be(1);
        handler.Verify(h => h.SincronizarCertificacionesAsync(
            It.Is<IReadOnlyCollection<CertificationSyncItem>>(items =>
                items.Count == 1 && items.Single().NombreNormalizado == "iso 27001" && items.Single().FileIdCensus == "file-first"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
