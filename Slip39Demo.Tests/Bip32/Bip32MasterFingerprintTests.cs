using FluentAssertions;
using Slip39Demo.Core.Bip32;
using Xunit;

namespace Slip39Demo.Tests.Bip32;

public class Bip32MasterFingerprintTests
{
    // The all-"abandon"/"about" mnemonic with no passphrase is the canonical
    // BIP-39 test seed. Its BIP-32 master fingerprint is 73c5da0a — verified
    // against NBitcoin, iancoleman.io, and Sparrow Wallet. Any deviation here
    // is a bug in the crypto chain, not a test data error.
    [Fact]
    public void Compute_AbandonSeed_NoPassphrase_MatchesKnownFingerprint()
    {
        const string seedWords = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

        var fp = Bip32MasterFingerprint.Compute(seedWords, passphrase: null);

        fp.ToString().Should().Be("73c5da0a");
    }

    [Fact]
    public void Compute_AbandonSeed_WithPassphrase_DiffersFromNoPassphrase()
    {
        const string seedWords = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

        var withoutPp = Bip32MasterFingerprint.Compute(seedWords, passphrase: null);
        var withPp    = Bip32MasterFingerprint.Compute(seedWords, passphrase: "TREZOR");

        withPp.Should().NotBe(withoutPp);
    }

    [Fact]
    public void Bip32Fingerprint_From_RejectsWrongLength()
    {
        var act = () => Bip32Fingerprint.From(new byte[3]);
        act.Should().Throw<ArgumentException>().WithMessage("*4 bytes*");
    }
}
