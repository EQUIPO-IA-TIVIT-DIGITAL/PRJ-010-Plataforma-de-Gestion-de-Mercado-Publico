using FluentAssertions;
using MPM.Modules.Propuestas.Services;
using Xunit;

namespace MPM.Modules.Propuestas.Tests.Services;

public class CertificationNameNormalizerTests
{
    [Theory]
    [InlineData("ISO/IEC 27001", "ISO 27001")]
    [InlineData("ISO 27001", "27001")]
    public void NormalizeKey_EquivalentCertificationNames_ReturnsSameKey(string first, string second)
    {
        CertificationNameNormalizer.NormalizeKey(first)
            .Should().Be(CertificationNameNormalizer.NormalizeKey(second));
    }
}
