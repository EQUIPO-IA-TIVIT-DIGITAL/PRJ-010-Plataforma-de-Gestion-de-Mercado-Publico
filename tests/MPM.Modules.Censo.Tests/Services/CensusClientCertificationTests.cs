using FluentAssertions;
using MPM.Modules.Censo.Services;
using Xunit;

namespace MPM.Modules.Censo.Tests.Services;

public class CensusClientCertificationTests
{
    [Fact]
    public void ParseUserCertifications_MapsCertificationFileAndIdsWithoutPersonProfile()
    {
        const string json = "[{\"certificationTypeName\":\"ISO/IEC 27001\",\"file\":{\"fileId\":\"file-1\"},\"institutionName\":\"BSI\",\"validity\":\"2024-2027\",\"userId\":\"user-1\",\"corporateId\":\"corp-1\"}]";

        var result = CensusClient.ParseUserCertifications(json);

        result.Should().ContainSingle();
        result[0].CertificationTypeName.Should().Be("ISO/IEC 27001");
        result[0].FileId.Should().Be("file-1");
        result[0].Institution.Should().Be("BSI");
        result[0].Validity.Should().Be("2024-2027");
        result[0].UserId.Should().Be("user-1");
        result[0].CorporateId.Should().Be("corp-1");
    }
}
