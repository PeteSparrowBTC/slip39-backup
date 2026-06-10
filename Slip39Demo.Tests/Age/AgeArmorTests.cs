using FluentAssertions;
using Slip39Demo.Core.Age;
using Xunit;

namespace Slip39Demo.Tests.Age;

public class AgeArmorTests
{
    [Fact]
    public void Encode_WrapsWithFencesAndBase64WrappedAt64()
    {
        var bytes = Enumerable.Range(0, 200).Select(i => (byte)(i & 0xff)).ToArray();

        var armored = AgeArmor.Encode(bytes);

        armored.Should().StartWith("-----BEGIN AGE ENCRYPTED FILE-----\n");
        armored.Should().EndWith("-----END AGE ENCRYPTED FILE-----\n");
        var bodyLines = armored.Split('\n')
            .Where((_, i) => i > 0)
            .TakeWhile(l => !l.StartsWith("-----END"))
            .ToList();
        bodyLines.Should().OnlyContain(l => l.Length <= 64);
    }

    [Fact]
    public void Decode_RoundTripsBinary()
    {
        var bytes = Enumerable.Range(0, 500).Select(i => (byte)(i * 31 & 0xff)).ToArray();
        var armored = AgeArmor.Encode(bytes);

        var decoded = AgeArmor.Decode(armored);

        decoded.IsSuccess.Should().BeTrue();
        decoded.Value.Should().Equal(bytes);
    }

    [Fact]
    public void Decode_MissingHeader_ReturnsFailure()
    {
        var result = AgeArmor.Decode("not an armored file");
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Decode_TruncatedBody_ReturnsFailure()
    {
        var truncated = "-----BEGIN AGE ENCRYPTED FILE-----\n!!!notbase64!!!\n-----END AGE ENCRYPTED FILE-----\n";
        var result = AgeArmor.Decode(truncated);
        result.IsFailure.Should().BeTrue();
    }
}
