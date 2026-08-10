using FluentAssertions;
using Slip39Demo.Core.Slip39;
using Xunit;

namespace Slip39Demo.Tests.Bip39;

// The pasted backup key: its format, its check code, and the rule that stops one dice
// roll log becoming both the wallet's seed and the key that is supposed to protect it.
//
// The k and check-code pairs below are pinned from dice-to-seed's definition
// (k = SHA-256(rolls), check = first 4 hex characters of SHA-256 of the lowercase hex
// STRING) and were computed outside this repository:
//
//   printf '%s' "$ROLLS" | sha256sum
//   printf '%s' "$K_HEX" | sha256sum | cut -c1-4
//
// They are not values this code produced. That distinction is the whole point: a check
// code this repository both computes and verifies would prove only that it agrees with
// itself, and the transcription it guards happens between two different programs.
public class BackupKeyEntryTests
{
    // rolls = "1" x 50
    const string KeyOfFiftyOnes = "3dac51a65ec9fcfc409a1b5f1defe92ba723843118ea511971ab46b36859495f";
    const string CheckOfFiftyOnes = "86bb";

    // rolls = "1" x 60
    const string KeyOfSixtyOnes = "70d36dedb311176c76ecd7f78d72340dbbaa364d23923239c8fda5b8c6ead201";
    const string CheckOfSixtyOnes = "310c";

    // The all-zeros published mnemonics. Their BIP-39 entropy is a run of zero bytes, so
    // the key that collides with each one is written out directly.
    const string TwelveWordZeroSeed =
        "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

    const string TwentyFourWordZeroSeed =
        "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon "
        + "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon art";

    static readonly string[] NoSeeds = [];

    [Theory]
    [InlineData(KeyOfFiftyOnes, CheckOfFiftyOnes)]
    [InlineData(KeyOfSixtyOnes, CheckOfSixtyOnes)]
    public void The_check_code_matches_the_pinned_vectors(string keyHex, string expectedCheck) =>
        BackupKeyEntry.CheckCodeFor(keyHex).Should().Be(expectedCheck);

    [Theory]
    [InlineData(KeyOfFiftyOnes, CheckOfFiftyOnes)]
    [InlineData(KeyOfSixtyOnes, CheckOfSixtyOnes)]
    public void A_pinned_vector_with_its_check_code_is_accepted(string keyHex, string check)
    {
        var key = BackupKeyEntry.Read(keyHex, check);

        key.IsSuccess.Should().BeTrue(key.IsFailure ? key.Error : "");
        Convert.ToHexStringLower(key.Value).Should().Be(keyHex);
    }

    // dice-to-seed prints k in groups of four characters, so a hand transcription
    // legitimately arrives with spaces in it, and an uppercase paste is the same key. Both
    // fold to the canonical lowercase string the check code is defined over.
    [Theory]
    [InlineData("3dac 51a6 5ec9 fcfc 409a 1b5f 1def e92b a723 8431 18ea 5119 71ab 46b3 6859 495f", "86bb")]
    [InlineData("3DAC51A65EC9FCFC409A1B5F1DEFE92BA723843118EA511971AB46B36859495F", "86BB")]
    [InlineData("\n3dac51a65ec9fcfc409a1b5f1defe92b\na723843118ea511971ab46b36859495f\n", " 86bb ")]
    public void Grouping_case_and_stray_whitespace_do_not_change_the_key(string keyHex, string check)
    {
        var key = BackupKeyEntry.Read(keyHex, check);

        key.IsSuccess.Should().BeTrue(key.IsFailure ? key.Error : "");
        Convert.ToHexStringLower(key.Value).Should().Be(KeyOfFiftyOnes);
    }

    [Fact]
    public void A_wrong_check_code_is_refused_and_the_reason_is_named()
    {
        var key = BackupKeyEntry.Read(KeyOfFiftyOnes, "0000");

        key.IsFailure.Should().BeTrue();
        key.Error.Should().Contain("REFUSED").And.Contain("check code does not match");
        // The refusal must not hand back the correct code: that would turn the typo
        // detector into a fill-in-the-blank prompt.
        key.Error.Should().NotContain(CheckOfFiftyOnes);
    }

    [Theory]
    [InlineData("3dac51a65ec9fcfc409a1b5f1defe92ba723843118ea511971ab46b36859495", "63 were read")]   // 63 chars
    [InlineData("3dac51a65ec9fcfc409a1b5f1defe92ba723843118ea511971ab46b36859495f0", "65 were read")] // 65 chars
    [InlineData("", "no key was entered")]
    public void A_key_that_is_not_sixty_four_hex_characters_is_refused(string keyHex, string expected)
    {
        var key = BackupKeyEntry.Read(keyHex, CheckOfFiftyOnes);

        key.IsFailure.Should().BeTrue();
        key.Error.Should().Contain("REFUSED").And.Contain(expected);
    }

    [Fact]
    public void A_non_hexadecimal_character_is_refused_by_position_without_echoing_it()
    {
        // Character 5 replaced by 'z'. The message locates the slip; it does not repeat
        // any of the key back at the user.
        var key = BackupKeyEntry.Read("3dacz1a65ec9fcfc409a1b5f1defe92ba723843118ea511971ab46b36859495f", CheckOfFiftyOnes);

        key.IsFailure.Should().BeTrue();
        key.Error.Should().Contain("hexadecimal").And.Contain("Character 5");
        key.Error.Should().NotContain("3dac");
    }

    // A key with no check code has nothing catching a transcription slip, and a mistyped
    // key produces a backup nobody can recover. Refused rather than accepted with a note.
    [Fact]
    public void A_key_without_a_check_code_is_refused()
    {
        var key = BackupKeyEntry.Read(KeyOfFiftyOnes, "");

        key.IsFailure.Should().BeTrue();
        key.Error.Should().Contain("check code is required");
    }

    [Theory]
    [InlineData("86b")]
    [InlineData("86bbb")]
    [InlineData("86bz")]
    public void A_check_code_that_is_not_four_hex_characters_is_refused(string check)
    {
        var key = BackupKeyEntry.Read(KeyOfFiftyOnes, check);

        key.IsFailure.Should().BeTrue();
        key.Error.Should().Contain("REFUSED");
    }

    // A check code typed on its own means somebody is halfway through transcribing. The
    // generator must not quietly step in at that moment: that is the invisible substitution
    // this class exists to prevent.
    [Fact]
    public void A_check_code_with_no_key_refuses_rather_than_generating()
    {
        var choice = BackupKeyEntry.Resolve(null, CheckOfFiftyOnes, NoSeeds);

        choice.IsFailure.Should().BeTrue();
        choice.Error.Should().Contain("no key was entered");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "\t")]
    public void Nothing_supplied_falls_through_to_the_generator(string? keyHex, string? check)
    {
        var choice = BackupKeyEntry.Resolve(keyHex, check, [TwentyFourWordZeroSeed]);

        choice.IsSuccess.Should().BeTrue(choice.IsFailure ? choice.Error : "");
        choice.Value.Source.Should().Be(BackupKeySource.Generated);
        choice.Value.Key.Should().HaveCount(32);
    }

    [Fact]
    public void The_generator_path_produces_a_different_key_every_time()
    {
        var first = BackupKeyEntry.Resolve(null, null, NoSeeds).Value.Key;
        var second = BackupKeyEntry.Resolve(null, null, NoSeeds).Value.Key;

        first.Should().NotEqual(second);
    }

    [Fact]
    public void A_good_key_that_does_not_collide_is_accepted_and_marked_pasted()
    {
        var choice = BackupKeyEntry.Resolve(KeyOfFiftyOnes, CheckOfFiftyOnes, [TwentyFourWordZeroSeed]);

        choice.IsSuccess.Should().BeTrue(choice.IsFailure ? choice.Error : "");
        choice.Value.Source.Should().Be(BackupKeySource.Pasted);
        Convert.ToHexStringLower(choice.Value.Key).Should().Be(KeyOfFiftyOnes);
    }

    // THE RULE THIS FILE EXISTS FOR. dice-to-seed derives a seed's BIP-39 entropy with the
    // same SHA-256 over the same roll log, so a reused log makes k equal to the wallet's own
    // entropy: 32 bytes of it for a 24-word seed, the first 16 for a 12-word seed. Both
    // cases are the same comparison over k's first entropy.Length bytes.
    [Theory]
    // 24 words of zero entropy against the key that entropy would be: all 32 bytes match.
    [InlineData(TwentyFourWordZeroSeed,
        "0000000000000000000000000000000000000000000000000000000000000000", "60e0", 32)]
    // 12 words of zero entropy: only the first 16 bytes of k are the entropy, and that is
    // still a total break, so the tail is deliberately non-zero here.
    [InlineData(TwelveWordZeroSeed,
        "00000000000000000000000000000000ffffffffffffffffffffffffffffffff", "b052", 16)]
    public void A_key_that_is_the_seeds_own_entropy_is_refused(
        string seedWords, string keyHex, string check, int matchingBytes)
    {
        // The check code is correct for each key here, so the refusal can only come from
        // the collision rule and not from a format complaint.
        BackupKeyEntry.CheckCodeFor(keyHex).Should().Be(check);

        var choice = BackupKeyEntry.Resolve(keyHex, check, [seedWords]);

        choice.IsFailure.Should().BeTrue();
        choice.Error.Should().Contain("REFUSED")
            .And.Contain($"first {matchingBytes} bytes")
            .And.Contain("Nothing was generated");
    }

    // Every seed in the form is compared, not just the first: the collision is just as
    // total when the reused log belongs to the third cosigner.
    [Fact]
    public void A_collision_with_any_seed_in_the_form_is_refused()
    {
        var choice = BackupKeyEntry.Resolve(
            "0000000000000000000000000000000000000000000000000000000000000000",
            "60e0",
            ["ozone drill grab fiber curtain grace pudding thank cruise elder eight picnic",
             null,
             TwentyFourWordZeroSeed]);

        choice.IsFailure.Should().BeTrue();
        choice.Error.Should().Contain("REFUSED");
    }

    [Fact]
    public void A_seed_that_shares_no_prefix_with_the_key_does_not_collide()
    {
        var scan = BackupKeyEntry.ScanForSeedCollision(
            Convert.FromHexString(KeyOfFiftyOnes),
            [TwelveWordZeroSeed, TwentyFourWordZeroSeed]);

        scan.IsSuccess.Should().BeTrue(scan.IsFailure ? scan.Error : "");
        scan.Value.Compared.Should().Be(2);
        scan.Value.Unreadable.Should().Be(0);
    }

    // A seed the form holds but BIP-39 cannot read (a word missing, a bad checksum, a
    // non-standard length) cannot be compared, so it is counted and reported rather than
    // treated as if it had passed. Not a refusal: this tool has never required the seed
    // words in the form to be valid BIP-39, and starting to would change what generation
    // accepts for every user who pastes no key at all.
    [Fact]
    public void A_seed_that_cannot_be_read_as_bip39_is_counted_as_uncompared()
    {
        var scan = BackupKeyEntry.ScanForSeedCollision(
            Convert.FromHexString(KeyOfFiftyOnes),
            ["not a real mnemonic at all",
             "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon",
             TwentyFourWordZeroSeed]);

        scan.IsSuccess.Should().BeTrue(scan.IsFailure ? scan.Error : "");
        scan.Value.Compared.Should().Be(1);
        scan.Value.Unreadable.Should().Be(2);
    }

    // The same seed typed into the top-level field and copied into a cosigner is one
    // mnemonic, and the scan should say so rather than count it twice. Spacing and case
    // differences are the same mnemonic too.
    [Fact]
    public void Identical_seeds_are_compared_once()
    {
        var scan = BackupKeyEntry.ScanForSeedCollision(
            Convert.FromHexString(KeyOfFiftyOnes),
            [TwelveWordZeroSeed, "  " + TwelveWordZeroSeed.ToUpperInvariant() + "  ", TwelveWordZeroSeed]);

        scan.IsSuccess.Should().BeTrue(scan.IsFailure ? scan.Error : "");
        scan.Value.Compared.Should().Be(1);
    }

    [Fact]
    public void The_key_length_constants_describe_a_thirty_two_byte_key()
    {
        BackupKeyEntry.KeyByteLength.Should().Be(32);
        BackupKeyEntry.KeyHexLength.Should().Be(64);
        BackupKeyEntry.CheckCodeLength.Should().Be(4);
    }
}
