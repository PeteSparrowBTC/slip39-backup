using FluentAssertions;
using Slip39Demo.Core.Bip32;
using Slip39Demo.Core.Verification;
using Xunit;

namespace Slip39Demo.Tests.Verification;

public class VerificationRecordTests
{
    [Fact]
    public void Build_ContainsAllRequiredSections()
    {
        var mnemonics = new[]
        {
            "abandon ability able about above absent absorb abstract absurd abuse",
            "across action actor actress actual adapt add addict address adjust",
            "admit adult advance advice aerobic affair afford afraid again age",
        };
        var payloadBytes = "fake-ciphertext-content"u8.ToArray();
        var masterFp = Bip32Fingerprint.From(new byte[] { 0x7a, 0x3f, 0x9c, 0x2d });

        var rec = VerificationRecord.Build(
            createdDate: "2026-05-21",
            toolVersion: "2.0.0",
            label: "Main wallet 2026",
            mnemonicsInOrder: mnemonics,
            payloadAgeBytes: payloadBytes,
            walletMasterFingerprint: masterFp);

        rec.Should().Contain("slip39-backup Verification Record");
        rec.Should().Contain("Created:       2026-05-21");
        rec.Should().Contain("Tool version:  2.0.0");
        rec.Should().Contain("Label:         Main wallet 2026");
        rec.Should().Contain("Wallet master fingerprint (BIP-32):  7a3f9c2d");
        rec.Should().Contain("share-1-of-3:");
        rec.Should().Contain("share-2-of-3:");
        rec.Should().Contain("share-3-of-3:");
        rec.Should().Contain("Payload integrity (SHA256 of payload.age):");
        rec.Should().Contain("DO NOT distribute to share-holders");
    }

    [Fact]
    public void ShareFingerprint_IsTruncatedSha256OfMnemonic_8HexChars()
    {
        var fp = ShareFingerprint.Compute("abandon ability able");

        fp.Should().HaveLength(8);
        fp.Should().MatchRegex("^[0-9a-f]{8}$");
    }

    [Fact]
    public void Build_PayloadSha256_MatchesExpected()
    {
        var bytes = "hello"u8.ToArray();
        var rec = VerificationRecord.Build(
            "2026-05-21", "2.0.0", "x",
            ["a b c"], bytes,
            Bip32Fingerprint.From(new byte[] { 0, 0, 0, 0 }));

        // Sha256("hello") = 2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824
        rec.Should().Contain("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824");
    }
}
