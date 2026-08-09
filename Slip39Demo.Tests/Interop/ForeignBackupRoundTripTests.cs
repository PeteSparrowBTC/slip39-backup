using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Slip39Demo.Core.Age;
using Slip39Demo.Core.Slip39;
using Xunit;

namespace Slip39Demo.Tests.Interop;

// The OTHER direction of the cross-implementation gate.
//
// Owner mode already refuses to release a bundle unless slip39-js and typage can
// read what the C# stack just wrote. That proves C# writes something the world
// can read. It says nothing about whether C# can read what the world writes, and
// that is exactly what Recoverer mode does: an heir turns up with a payload.age
// and mnemonics from wherever they ended up, possibly re-encrypted with the Go
// age CLI or re-emitted by a different SLIP-39 tool along the way.
//
// A parsing divergence in that direction surfaces at recovery time, on an
// airgapped machine, with no author around to debug it. So the artifacts here
// were produced entirely by the JS implementations
// (tools/independent-verify/make-foreign-fixtures.mjs) and committed, and this
// test recovers them from cold with no shared state and no shortcut: the 32-byte
// key is deliberately absent from the fixture, so Xecrets has to reconstruct it
// from the mnemonics before AgeSharp gets a chance to decrypt anything.
//
// NOTE ON EXTENDABLE SHARES. The corpus covers both values of the SLIP-39
// extendable-backup flag, verified by decoding the bit out of each fixture's
// share header rather than trusting the generator. This matters because the flag
// selects the PBKDF2 salt that encrypts the master secret, so the two values are
// genuinely different code paths through the Feistel network. This tool only
// ever GENERATES extendable shares (Slip39Wrapping refuses the other, because
// Xecrets had a defect there, xecrets/xecrets-slip39#28), but a recoverer does
// not get to choose what arrives, and combining is a separate path from
// splitting. The non-extendable fixtures are the ones that exercise the
// previously-defective path.
public class ForeignBackupRoundTripTests
{
    sealed record Fixture(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("extendable")] bool Extendable,
        [property: JsonPropertyName("groupThreshold")] int GroupThreshold,
        [property: JsonPropertyName("groups")] IReadOnlyList<FixtureGroup> Groups,
        [property: JsonPropertyName("mnemonics")] IReadOnlyList<string> Mnemonics,
        [property: JsonPropertyName("payloadAgeB64")] string PayloadAgeB64,
        [property: JsonPropertyName("expectedPayloadText")] string ExpectedPayloadText);

    sealed record FixtureGroup(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("threshold")] int Threshold,
        [property: JsonPropertyName("count")] int Count);

    sealed record FixtureFile(
        [property: JsonPropertyName("fixtures")] IReadOnlyList<Fixture> Fixtures);

    static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "Interop", "foreign-backups.json");

    static IReadOnlyList<Fixture> Load() =>
        JsonSerializer.Deserialize<FixtureFile>(File.ReadAllText(FixturePath))!.Fixtures;

    public static IEnumerable<object[]> Fixtures() =>
        Load().Select((f, i) => new object[] { f.Name, i });

    // Same guard as the CCTV corpus: an empty MemberData source makes a [Theory]
    // report success, so assert the fixtures are actually there.
    [Fact]
    public void FixtureCorpus_IsPresent() =>
        Load().Should().HaveCount(4);

    // The corpus is only meaningful if it really carries both flag values, and
    // the flag is a property of the share bytes rather than of the generator's
    // intentions. Read it back out of the parsed share, which also confirms that
    // Xecrets decodes the bit the way slip39-js encoded it. A regenerated corpus
    // that silently lost one of the two values would leave a whole code path
    // uncovered while still looking green.
    [Fact]
    public void FixtureCorpus_CoversBothValuesOfTheExtendableFlag()
    {
        var fixtures = Load();

        fixtures.Select(f => ExtendableFlagOf(f.Mnemonics[0])).Distinct()
            .Should().BeEquivalentTo([true, false]);
        fixtures.Should().OnlyContain(f => ExtendableFlagOf(f.Mnemonics[0]) == f.Extendable,
            "the flag encoded in the share must match what the fixture declares");
    }

    static bool ExtendableFlagOf(string mnemonic) =>
        Xecrets.Slip39.Share.Parse(mnemonic).Prefix.Extendable;

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void ForeignProducedBackup_IsRecoverableByOurStack(string name, int index)
    {
        _ = name; // shown in the runner; the fixture index selects the case
        var fixture = Load()[index];

        // Step 1: Xecrets must combine mnemonics it did not generate. Hand it
        // every share, exactly as a recoverer would, and let SelectMinimalSubset
        // reduce to a combinable set.
        var key = Slip39Wrapping.CombineMnemonics(fixture.Mnemonics);
        key.IsSuccess.Should().BeTrue($"foreign shares for '{fixture.Name}' must combine: {Why(key)}");
        key.Value.Should().HaveCount(32);

        // Step 2: AgeSharp must decrypt a blob typage wrote, using that key as the
        // hex passphrase. Failure here means either the key came back wrong (a
        // SLIP-39 divergence that combine did not report) or the age parsing
        // diverges; the assertion on the plaintext distinguishes them.
        var plaintext = AgePassphrase.Decrypt(Convert.FromBase64String(fixture.PayloadAgeB64), key.Value);
        plaintext.IsSuccess.Should().BeTrue($"foreign payload.age for '{fixture.Name}' must decrypt: {Why(plaintext)}");
        Encoding.UTF8.GetString(plaintext.Value).Should().Be(fixture.ExpectedPayloadText);
    }

    // Recovery from a subset is the realistic case: the heir gathers threshold
    // many, not all. Exercise the first group's threshold rather than the whole
    // pile, for the single-group fixtures where that alone satisfies the policy.
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void ForeignProducedBackup_IsRecoverableFromAThresholdSubset(string name, int index)
    {
        _ = name;
        var fixture = Load()[index];

        // Take exactly the member threshold from each of the first groupThreshold
        // groups, walking the flat list the way the fixture laid it out.
        var subset = new List<string>();
        var offset = 0;
        foreach (var g in fixture.Groups)
        {
            if (subset.Count / Math.Max(1, g.Threshold) < fixture.GroupThreshold)
                subset.AddRange(fixture.Mnemonics.Skip(offset).Take(g.Threshold));
            offset += g.Count;
        }

        var key = Slip39Wrapping.CombineMnemonics(subset);
        key.IsSuccess.Should().BeTrue($"threshold subset for '{fixture.Name}' must combine: {Why(key)}");

        var plaintext = AgePassphrase.Decrypt(Convert.FromBase64String(fixture.PayloadAgeB64), key.Value);
        plaintext.IsSuccess.Should().BeTrue($"decrypt from threshold subset for '{fixture.Name}': {Why(plaintext)}");
        Encoding.UTF8.GetString(plaintext.Value).Should().Be(fixture.ExpectedPayloadText);
    }

    // Result.Error throws when the result is a success, and FluentAssertions
    // evaluates the "because" message eagerly, so never touch .Error directly in
    // an assertion message.
    static string Why<T>(CSharpFunctionalExtensions.Result<T> r) => r.IsFailure ? r.Error : "(succeeded)";

    // A share set below threshold must fail cleanly rather than returning
    // something that looks like a key. Recoverer's error messages depend on this
    // distinction being reliable.
    [Fact]
    public void ForeignShares_BelowThreshold_FailCleanly()
    {
        var fixture = Load()[0]; // 3-of-5
        var tooFew = fixture.Mnemonics.Take(2).ToList();

        Slip39Wrapping.CombineMnemonics(tooFew).IsFailure.Should().BeTrue();
    }
}
