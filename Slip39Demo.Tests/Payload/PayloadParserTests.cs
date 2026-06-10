using FluentAssertions;
using Slip39Demo.Core.Payload;
using Xunit;

namespace Slip39Demo.Tests.Payload;

public class PayloadParserTests
{
    [Fact]
    public void Parse_RoundTripsSingleSigPayload()
    {
        var original = new PayloadV1_1(
            SchemaVersion: "1.1",
            Created: "2026-05-21T14:32:00Z",
            Label: "Main wallet",
            TopLevelSeedWords: "abandon ability able about above absent absorb abstract absurd abuse access accident",
            Cosigners: [new Cosigner("main", "bip39", "my-passphrase", "m/84'/0'/0'", null, null)],
            Descriptor: null,
            Threshold: "3-of-5",
            Slip39Extendable: true,
            Notes: null);

        var emitted = PayloadEmitter.Emit(original);
        var parsed = PayloadParser.Parse(emitted);

        parsed.IsSuccess.Should().BeTrue();
        parsed.Value.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void Parse_MultivendorRoundTrips()
    {
        var original = new PayloadV1_1(
            "1.1", "2026-05-21T14:32:00Z", "multivendor", null,
            [new Cosigner("trezor", "bip39", null, "m/48'/0'/0'/2'", "word1 word2 word3", "7a3f9c2d"),
             new Cosigner("coldcard", "bip39", null, "m/48'/0'/0'/2'", "word4 word5 word6", "4e5cb619")],
            "wsh(...)", "3-of-5", true, null);

        var parsed = PayloadParser.Parse(PayloadEmitter.Emit(original));

        parsed.IsSuccess.Should().BeTrue();
        parsed.Value.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void Parse_UnsupportedSchemaVersion_FailsWithClearError()
    {
        var text =
            "schema_version: 1.0\n" +
            "created: 2026-01-01T00:00:00Z\n" +
            "threshold: 2-of-3\n";

        var result = PayloadParser.Parse(text);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("1.0");
        result.Error.Should().Contain("1.1");
    }

    [Fact]
    public void Parse_MissingSchemaVersion_FailsWithDescriptiveError()
    {
        var result = PayloadParser.Parse("created: 2026-05-21T00:00:00Z\nthreshold: 3-of-5\n");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("schema_version");
    }

    [Fact]
    public void Parse_UnknownKey_FailsWithLineNumber()
    {
        var result = PayloadParser.Parse(
            "schema_version: 1.1\n" +
            "created: x\n" +
            "this_is_not_a_field: 42\n" +
            "threshold: 3-of-5\n");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("line 3");
        result.Error.Should().Contain("this_is_not_a_field");
    }

    [Fact]
    public void Parse_NotesBlock_ConcatsMultiline()
    {
        var text =
            "schema_version: 1.1\n" +
            "created: 2026-05-21T00:00:00Z\n" +
            "seed_words: abandon\n" +
            "cosigners:\n" +
            "  - id: main\n" +
            "    wallet_type: bip39\n" +
            "    derivation_path: m/84'/0'/0'\n" +
            "threshold: 3-of-5\n" +
            "slip39_extendable: true\n" +
            "notes: |\n" +
            "  line one\n" +
            "  line two\n";

        var parsed = PayloadParser.Parse(text);

        parsed.IsSuccess.Should().BeTrue();
        parsed.Value.Notes.Should().Be("line one\nline two");
    }
}
