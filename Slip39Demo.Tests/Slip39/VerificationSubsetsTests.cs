using FluentAssertions;
using Slip39Demo.Core.Slip39;
using Xunit;

namespace Slip39Demo.Tests.Slip39;

// The covering-subsets builder feeds the independent post-generation verifier.
// Two invariants matter: (1) EVERY share appears in at least one subset — an
// uncovered share could ship corrupt and unverified; (2) every subset satisfies
// the threshold structure exactly (member threshold within groups, group
// threshold across groups) because slip39-js rejects more-than-threshold sets.
public class VerificationSubsetsTests
{
    static List<string> Mnemonics(int n, string prefix = "m") =>
        Enumerable.Range(0, n).Select(i => $"{prefix}{i}").ToList();

    [Fact]
    public void SingleGroup_3of5_CoversEveryShare_WithExactSizedSubsets()
    {
        var cfg = new GroupConfig(1, [new ShareGroup("only", 3, 5)], true);
        var flat = Mnemonics(5);

        var subsets = VerificationSubsets.BuildCoveringSubsets(cfg, flat);

        subsets.Should().HaveCount(2); // ceil(5/3)
        subsets.Should().OnlyContain(s => s.Count == 3);
        subsets.SelectMany(s => s).Distinct().Should().BeEquivalentTo(flat);
    }

    [Fact]
    public void SingleGroup_1of1_SingleSubset()
    {
        var cfg = new GroupConfig(1, [new ShareGroup("only", 1, 1)], true);

        var subsets = VerificationSubsets.BuildCoveringSubsets(cfg, Mnemonics(1));

        subsets.Should().ContainSingle().Which.Should().BeEquivalentTo(["m0"]);
    }

    [Fact]
    public void SingleGroup_ExactThreshold_3of3_SingleSubset()
    {
        var cfg = new GroupConfig(1, [new ShareGroup("only", 3, 3)], true);

        var subsets = VerificationSubsets.BuildCoveringSubsets(cfg, Mnemonics(3));

        subsets.Should().ContainSingle().Which.Should().BeEquivalentTo(["m0", "m1", "m2"]);
    }

    [Fact]
    public void MultiGroup_GroupThreshold2_CoversAllShares_AndPadsWithFillerGroups()
    {
        // personal (1-of-1) at flat[0]; friends (3-of-5) at flat[1..6); family (2-of-6) at flat[6..12)
        var cfg = new GroupConfig(
            GroupThreshold: 2,
            Groups: [
                new ShareGroup("personal", 1, 1),
                new ShareGroup("friends", 3, 5),
                new ShareGroup("family", 2, 6)],
            Extendable: true);
        var flat = Mnemonics(1 + 5 + 6);

        var subsets = VerificationSubsets.BuildCoveringSubsets(cfg, flat);

        // Coverage: every one of the 12 shares appears somewhere.
        subsets.SelectMany(s => s).Distinct().Should().BeEquivalentTo(flat);

        // Structure: each subset carries chunks from exactly 2 groups (the target
        // chunk + one filler group at its member threshold). Sizes are exact:
        // target-chunk size + filler size — never more.
        foreach (var s in subsets)
            s.Count.Should().BeLessThanOrEqualTo(3 + 1); // biggest = friends chunk(3) + personal filler(1)
    }

    [Fact]
    public void EmptyInput_NoSubsets()
    {
        var cfg = new GroupConfig(1, [new ShareGroup("only", 3, 5)], true);

        // Degenerate call (shouldn't happen in practice): no mnemonics → subsets
        // still structurally produced but empty-chunked; builder must not throw.
        var act = () => VerificationSubsets.BuildCoveringSubsets(cfg, []);

        act.Should().NotThrow();
    }
}
