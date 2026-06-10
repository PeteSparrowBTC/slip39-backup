using FluentAssertions;
using Slip39Demo.Core.Slip39;
using Xunit;

namespace Slip39Demo.Tests.Slip39;

// Round-trip and validation tests for Slip39Wrapping. The wrapping presents
// a clean Result<T> API on top of Xecrets.Slip39 and — critically — always uses
// the empty SLIP-39 passphrase (the redesign deletes the SLIP-39 passphrase
// concept; secret material lives in payload.age). These tests therefore call
// SplitKey and CombineMnemonics without any passphrase argument.
public class Slip39WrappingTests
{
    // A deterministic 32-byte K for assertions: bytes 0,7,14,21,...,217 (mod 256).
    // We never use any non-trivial passphrase, so a fixed K is sufficient — the
    // SLIP-39 share id is randomised internally by Xecrets, so each test run
    // produces fresh mnemonics, but the recovered bytes must always equal K.
    static readonly byte[] FixedK = Enumerable.Range(0, 32).Select(i => (byte)(i * 7 & 0xff)).ToArray();

    [Fact]
    public void SplitThenCombine_SingleGroup_3of5_Extendable_RecoversK()
    {
        var cfg = new GroupConfig(
            GroupThreshold: 1,
            Groups: [new ShareGroup("only", Threshold: 3, Count: 5)],
            Extendable: true);

        var split = Slip39Wrapping.SplitKey(FixedK, cfg);
        split.IsSuccess.Should().BeTrue();
        split.Value.Should().HaveCount(5);

        var anyThree = split.Value.Take(3).ToList();
        var combined = Slip39Wrapping.CombineMnemonics(anyThree);

        combined.IsSuccess.Should().BeTrue();
        combined.Value.Should().Equal(FixedK);
    }

    [Fact]
    public void SplitKey_NonExtendable_IsRejectedByWrapper()
    {
        // Xecrets.Slip39 2.3.1315's Feistel cipher is broken on the
        // extendable=false path — stress testing shows ~25% of generated share
        // sets fail to round-trip (recovered bytes != original) and ~50% throw
        // OverflowException during generation. Slip39Wrapping therefore refuses
        // non-extendable mode outright; this test pins that behaviour so any
        // future re-enablement is intentional and accompanied by a working
        // Xecrets release.
        var cfg = new GroupConfig(1, [new ShareGroup("only", 2, 3)], Extendable: false);

        var split = Slip39Wrapping.SplitKey(FixedK, cfg);

        split.IsFailure.Should().BeTrue();
        split.Error.Should().Be(Slip39Wrapping.NonExtendableUnsupportedReason);
    }

    [Fact]
    public void SplitThenCombine_MultiGroup_RecoversK()
    {
        // Groups in flat order: personal-1 (idx 0, 1 share), personal-2 (idx 1, 1 share),
        // friends (idx 2..6, 5 shares), family (idx 7..12, 6 shares). Total: 13 shares.
        var cfg = new GroupConfig(
            GroupThreshold: 2,
            Groups: [
                new ShareGroup("personal-1", 1, 1),
                new ShareGroup("personal-2", 1, 1),
                new ShareGroup("friends",    3, 5),
                new ShareGroup("family",     2, 6)],
            Extendable: true);

        var split = Slip39Wrapping.SplitKey(FixedK, cfg).Value;
        split.Should().HaveCount(1 + 1 + 5 + 6);

        // To satisfy group-threshold=2 we need two *fully-satisfied* groups. Pick
        // personal-1 (the single 1-of-1 share at index 0) and three friends shares
        // (indices 2, 3, 4 — the first three of the 5 friends shares, satisfying 3-of-5).
        var combinedShares = new[] { split[0], split[2], split[3], split[4] };
        var combined = Slip39Wrapping.CombineMnemonics(combinedShares);

        combined.IsSuccess.Should().BeTrue();
        combined.Value.Should().Equal(FixedK);
    }

    [Fact]
    public void Combine_BelowThreshold_ReturnsFailure()
    {
        var cfg = new GroupConfig(1, [new ShareGroup("only", 3, 5)], true);
        var split = Slip39Wrapping.SplitKey(FixedK, cfg).Value;

        // Only 2 shares for a 3-of-5 group: Xecrets.Slip39 throws Slip39Exception,
        // which our wrapper catches and surfaces as Result.Failure.
        var combined = Slip39Wrapping.CombineMnemonics(split.Take(2));

        combined.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SplitKey_WrongLength_ReturnsFailure()
    {
        var cfg = new GroupConfig(1, [new ShareGroup("only", 2, 3)], true);
        var split = Slip39Wrapping.SplitKey(new byte[31], cfg);
        split.IsFailure.Should().BeTrue();
    }
}
