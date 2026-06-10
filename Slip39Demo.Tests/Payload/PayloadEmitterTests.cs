using FluentAssertions;
using Slip39Demo.Core.Payload;
using Xunit;

namespace Slip39Demo.Tests.Payload;

public class PayloadEmitterTests
{
    [Fact]
    public void Emit_SingleSig_NoPassphrase_ProducesCanonicalText()
    {
        var p = new PayloadV1_1(
            SchemaVersion: "1.1",
            Created: "2026-05-21T14:32:00Z",
            Label: "Main wallet",
            TopLevelSeedWords: "abandon ability able about above absent absorb abstract absurd abuse access accident",
            Cosigners: [new Cosigner(
                Id: "main",
                WalletType: "bip39",
                Passphrase: null,
                DerivationPath: "m/84'/0'/0'",
                SeedWords: null,
                XpubFingerprint: null)],
            Descriptor: null,
            Threshold: "3-of-5",
            Slip39Extendable: true,
            Notes: null);

        var emitted = PayloadEmitter.Emit(p);

        emitted.Should().Be(
            "schema_version: 1.1\n" +
            "created: 2026-05-21T14:32:00Z\n" +
            "label: \"Main wallet\"\n" +
            "\n" +
            "seed_words: abandon ability able about above absent absorb abstract absurd abuse access accident\n" +
            "\n" +
            "cosigners:\n" +
            "  - id: main\n" +
            "    wallet_type: bip39\n" +
            "    derivation_path: m/84'/0'/0'\n" +
            "\n" +
            "threshold: 3-of-5\n" +
            "slip39_extendable: true\n");
    }

    [Fact]
    public void Emit_SharedSeedMultisig_WithPassphrasesAndDescriptor()
    {
        var p = new PayloadV1_1(
            SchemaVersion: "1.1",
            Created: "2026-05-21T14:32:00Z",
            Label: "2-of-2 shared-seed multisig",
            TopLevelSeedWords: "abandon ability able about above absent absorb abstract absurd abuse access accident",
            Cosigners: [
                new Cosigner("cosigner_a", "bip39", "passphrase-a", "m/48'/0'/0'/2'", null, "7a3f9c2d"),
                new Cosigner("cosigner_b", "bip39", "passphrase-b", "m/48'/0'/0'/2'", null, "4e5cb619")],
            Descriptor: "wsh(sortedmulti(2, [7a3f9c2d/48'/0'/2']xpub_A/*, [4e5cb619/48'/0'/2']xpub_B/*))",
            Threshold: "3-of-5",
            Slip39Extendable: true,
            Notes: null);

        var emitted = PayloadEmitter.Emit(p);

        emitted.Should().Contain("seed_words: abandon ability");
        emitted.Should().Contain("- id: cosigner_a");
        emitted.Should().Contain("- id: cosigner_b");
        emitted.Should().Contain("passphrase: passphrase-a");
        emitted.Should().Contain("passphrase: passphrase-b");
        emitted.Should().Contain("descriptor: wsh(sortedmulti(");
    }

    [Fact]
    public void Emit_MultivendorMultisig_PerCosignerSeedNoTopLevelSeed()
    {
        var p = new PayloadV1_1(
            SchemaVersion: "1.1",
            Created: "2026-05-21T14:32:00Z",
            Label: "2-of-3 multivendor",
            TopLevelSeedWords: null,
            Cosigners: [
                new Cosigner("trezor", "bip39", null, "m/48'/0'/0'/2'", "abandon ability able about above absent absorb abstract absurd abuse access accident", "7a3f9c2d"),
                new Cosigner("coldcard", "bip39", null, "m/48'/0'/0'/2'", "about above absent absorb abstract absurd abuse access accident acid acoustic acquire", "4e5cb619"),
                new Cosigner("jade", "bip39", null, "m/48'/0'/0'/2'", "acquire across action actor actress actual adapt add addict address adjust admit", "9c1e3b58")],
            Descriptor: "wsh(sortedmulti(2,...))",
            Threshold: "3-of-5",
            Slip39Extendable: true,
            Notes: null);

        var emitted = PayloadEmitter.Emit(p);

        emitted.Should().NotContain("\nseed_words:");
        emitted.Should().Contain("    seed_words: abandon");
        emitted.Should().Contain("    seed_words: about");
        emitted.Should().Contain("    seed_words: acquire");
    }

    [Fact]
    public void Emit_NotesBlock_IndentedCorrectly()
    {
        var p = new PayloadV1_1(
            SchemaVersion: "1.1",
            Created: "2026-05-21T14:32:00Z",
            Label: null,
            TopLevelSeedWords: "abandon ability able",
            Cosigners: [new Cosigner("main", "bip39", null, "m/84'/0'/0'", null, null)],
            Descriptor: null,
            Threshold: "3-of-5",
            Slip39Extendable: true,
            Notes: "Set up on 2026-05-21.\nWallet imported into Sparrow.");

        var emitted = PayloadEmitter.Emit(p);

        emitted.Should().Contain("notes: |\n  Set up on 2026-05-21.\n  Wallet imported into Sparrow.\n");
    }
}
