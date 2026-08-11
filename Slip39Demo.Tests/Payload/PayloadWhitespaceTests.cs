using FluentAssertions;
using Slip39Demo.Core.Bip32;
using Slip39Demo.Core.Payload;
using Xunit;

namespace Slip39Demo.Tests.Payload;

// The leading-whitespace trap and the gate that now backs it up.
//
// THE BUG THIS FILE EXISTS FOR
// PayloadParser used to TrimStart the value after "key: ", and one of those values is a
// BIP-39 passphrase. A passphrase of " hunter2" was written correctly and read back as
// "hunter2", which is a different wallet: valid, derivable, and empty. Nothing in the
// pipeline could see it. The age file was well formed, the independent verifier compared
// the ciphertext against the same text the emitter had produced, and the master
// fingerprint in the verification record was computed from the form rather than from the
// payload, so every check agreed. The loss surfaced only at recovery, years later,
// looking like "the backup is broken" with no way to tell what had gone wrong.
//
// It ranked second on the risk list in docs/decisions, above key generation and supply
// chain, precisely because it was silent, and because a passphrase is exactly the kind of
// field people paste rather than type.
public class PayloadWhitespaceTests
{
    static PayloadV1_1 WithPassphrase(string? passphrase) =>
        new("1.1", "2026-08-11T00:00:00Z", "Main wallet",
            "abandon ability able about above absent absorb abstract absurd abuse access accident",
            [new Cosigner("main", "bip39", passphrase, "m/84'/0'/0'", null, "73c5da0a")],
            null, "3-of-5", true, null);

    // The three shapes that used to be destroyed, and the one that was not: trailing
    // whitespace survived even before the fix, which is worth pinning so a future
    // "tidy up the parser" does not quietly take it away.
    [Theory]
    [InlineData(" hunter2")]
    [InlineData("  hunter2")]
    [InlineData("hunter2 ")]
    [InlineData(" hunter2 ")]
    [InlineData(" ")]
    [InlineData("\thunter2")]
    public void A_passphrase_with_edge_whitespace_survives_the_round_trip(string passphrase)
    {
        var parsed = PayloadParser.Parse(PayloadEmitter.Emit(WithPassphrase(passphrase)));

        parsed.IsSuccess.Should().BeTrue(parsed.IsFailure ? parsed.Error : "");
        parsed.Value.Cosigners[0].Passphrase.Should().Be(passphrase,
            "a BIP-39 passphrase is any UTF-8 string, and every byte of it selects the wallet");
    }

    // Why the above matters, stated as the consequence rather than as an equality. If
    // these two fingerprints were the same, losing the space would be harmless.
    [Fact]
    public void A_leading_space_in_a_passphrase_selects_a_different_wallet()
    {
        const string seed =
            "abandon ability able about above absent absorb abstract absurd abuse access accident";

        Bip32MasterFingerprint.Compute(seed, " hunter2").ToString()
            .Should().NotBe(Bip32MasterFingerprint.Compute(seed, "hunter2").ToString(),
                "which is what made the trim a wrong-wallet bug rather than a cosmetic one");
    }

    // Reading exactly one separator space instead of trimming also repairs backups already
    // written: the file carried the intended value all along, the read side discarded it.
    // Hand-built text here, not the emitter, because the point is what an existing file on
    // an existing USB stick contains.
    [Fact]
    public void An_existing_backup_file_now_yields_the_passphrase_it_actually_recorded()
    {
        var text =
            "schema_version: 1.1\n" +
            "created: 2026-01-01T00:00:00Z\n" +
            "\ncosigners:\n" +
            "  - id: main\n" +
            "    wallet_type: bip39\n" +
            "    passphrase:  hunter2\n" +          // one separator space, then the value
            "    derivation_path: m/84'/0'/0'\n" +
            "\nthreshold: 3-of-5\n" +
            "slip39_extendable: true\n";

        var parsed = PayloadParser.Parse(text);

        parsed.IsSuccess.Should().BeTrue(parsed.IsFailure ? parsed.Error : "");
        parsed.Value.Cosigners[0].Passphrase.Should().Be(" hunter2");
    }

    // The ordinary case, unchanged: one separator space, no whitespace in the value.
    [Fact]
    public void An_ordinary_passphrase_is_unaffected()
    {
        var parsed = PayloadParser.Parse(PayloadEmitter.Emit(WithPassphrase("hunter2")));

        parsed.Value.Cosigners[0].Passphrase.Should().Be("hunter2");
    }

    // Every other single-line field runs through the same code, so the property is stated
    // for the ones a user can put whitespace into.
    [Fact]
    public void Labels_seeds_and_descriptors_keep_their_edge_whitespace()
    {
        var payload = new PayloadV1_1(
            "1.1", "2026-08-11T00:00:00Z", " Main wallet ", " abandon about ",
            [new Cosigner("main", "bip39", null, " m/84'/0'/0' ", " abandon about ", null)],
            " wsh(...) ", "3-of-5", true, null);

        var parsed = PayloadParser.Parse(PayloadEmitter.Emit(payload));

        parsed.IsSuccess.Should().BeTrue(parsed.IsFailure ? parsed.Error : "");
        parsed.Value.Label.Should().Be(" Main wallet ");
        parsed.Value.TopLevelSeedWords.Should().Be(" abandon about ");
        parsed.Value.Descriptor.Should().Be(" wsh(...) ");
        parsed.Value.Cosigners[0].DerivationPath.Should().Be(" m/84'/0'/0' ");
        parsed.Value.Cosigners[0].SeedWords.Should().Be(" abandon about ");
    }

    [Fact]
    public void EmitChecked_returns_the_same_text_as_the_emitter_when_the_payload_is_representable()
    {
        var payload = WithPassphrase(" hunter2");

        var emitted = PayloadRoundTrip.EmitChecked(payload);

        emitted.IsSuccess.Should().BeTrue(emitted.IsFailure ? emitted.Error : "");
        emitted.Value.Should().Be(PayloadEmitter.Emit(payload));
    }

    // The residual hole, closed by refusing rather than by mangling. A line break cannot
    // be carried on a line, and the previous behaviour was to write it anyway and produce
    // a file that failed to parse at recovery, or worse, parsed into something else.
    [Fact]
    public void EmitChecked_refuses_a_passphrase_containing_a_line_break_and_names_the_cosigner()
    {
        var emitted = PayloadRoundTrip.EmitChecked(WithPassphrase("hunter2\nmore"));

        emitted.IsFailure.Should().BeTrue();
        emitted.Error.Should().Contain("passphrase");
        emitted.Error.Should().Contain("main", "a four-cosigner multisig has to be told which one");
        emitted.Error.Should().Contain("line break");
        emitted.Error.Should().Contain("Nothing was encrypted");
    }

    // The refusal message is displayed in a banner on screen. It must be readable without
    // being a disclosure: no seed words, no passphrase, not even a fragment.
    [Fact]
    public void A_refusal_never_echoes_the_value_it_is_complaining_about()
    {
        var emitted = PayloadRoundTrip.EmitChecked(WithPassphrase("hunter2\ncorrect-horse"));

        emitted.IsFailure.Should().BeTrue();
        emitted.Error.Should().NotContain("hunter2");
        emitted.Error.Should().NotContain("correct-horse");
    }

    // Same rule on the read side. A payload whose passphrase contains a line break puts
    // the remainder where a key belongs, and the parser used to quote it back.
    [Fact]
    public void A_parse_error_does_not_echo_a_value_that_landed_where_a_key_belongs()
    {
        var result = PayloadParser.Parse(
            "schema_version: 1.1\ncreated: x\ncorrect horse battery staple\nthreshold: 3-of-5\n");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("line 3");
        result.Error.Should().NotContain("correct horse battery staple");
    }

    // An unknown key that really is a key name stays quoted: the message is useless for
    // finding a typo if it will not say what the typo was.
    [Fact]
    public void A_parse_error_still_names_a_key_that_looks_like_a_key()
    {
        var result = PayloadParser.Parse(
            "schema_version: 1.1\ncreated: x\nthreshhold: 3-of-5\n");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("threshhold");
    }

    [Fact]
    public void EmitChecked_accepts_line_breaks_in_notes_which_are_written_as_a_block()
    {
        var payload = WithPassphrase("hunter2") with { Notes = "line one\nline two" };

        var emitted = PayloadRoundTrip.EmitChecked(payload);

        emitted.IsSuccess.Should().BeTrue(emitted.IsFailure ? emitted.Error : "");
        PayloadParser.Parse(emitted.Value).Value.Notes.Should().Be("line one\nline two");
    }
}
