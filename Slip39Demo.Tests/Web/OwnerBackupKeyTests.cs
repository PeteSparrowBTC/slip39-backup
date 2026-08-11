using System.IO.Compression;
using Bunit;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Slip39Demo.Core.Age;
using Slip39Demo.Core.Bundle;
using Slip39Demo.Core.Pgp;
using Slip39Demo.Core.Slip39;
using Slip39Demo.UI.Pages;
using Slip39Demo.UI.Services;
using Slip39Demo.Web.Services;
using Xunit;

namespace Slip39Demo.Tests.Web;

// The Owner page with a backup key entered by hand. The unit tests in
// Slip39Demo.Tests.Bip39 pin the rules; these prove the page is wired to them, that a
// refusal really stops the download, and that a key which is accepted is the key the
// backup was actually encrypted under rather than a generated one substituted quietly.
//
// Same test doubles as OwnerFormValidationTests: verification passes, the machine reads
// as offline, and the in-process encryptor is used so nothing depends on an age binary.
public class OwnerBackupKeyTests : TestContext
{
    // Owner's own top-level seed field, selected by placeholder rather than class, for
    // the reason spelled out in OwnerFormValidationTests.
    const string TopLevelSeedSelector =
        "input[placeholder='abandon ability able about above absent absorb abstract absurd abuse access accident']";

    const string KeyFieldSelector =
        "input[placeholder='64 hex characters; spaces and line breaks are ignored']";

    const string CheckFieldSelector = "input[placeholder='4 hex characters']";

    // rolls = "1" x 50, pinned from dice-to-seed's definition and computed outside this
    // repository. Its entropy is nothing like either seed used below.
    const string DiceKeyHex = "3dac51a65ec9fcfc409a1b5f1defe92ba723843118ea511971ab46b36859495f";
    const string DiceCheckCode = "86bb";

    const string TwelveWordZeroSeed =
        "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

    // The published all-zeros 24-word mnemonic. Its BIP-39 entropy is 32 zero bytes, so
    // the key that collides with it is 64 zeros, whose check code is 60e0.
    const string TwentyFourWordZeroSeed =
        "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon "
        + "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon art";

    const string CollidingKeyHex = "0000000000000000000000000000000000000000000000000000000000000000";
    const string CollidingCheckCode = "60e0";

    public OwnerBackupKeyTests()
    {
        Services.AddSingleton<IIndependentVerifier>(new FakeVerifier());
        Services.AddSingleton<IConnectivityProbe>(new FakeProbe());
        Services.AddSingleton<IPayloadEncryptor>(new AgeSharpPayloadEncryptor());
        // A default so a test that only inspects markup does not have to register one.
        // Tests that assert on downloads register their own, which wins: the last
        // registration is the one resolved.
        Services.AddSingleton<IFileDownloader>(new NoopDownloader());
    }

    sealed class FakeProbe : IConnectivityProbe
    {
        public Task<bool> IsOnlineAsync() => Task.FromResult(false);
    }

    sealed class FakeVerifier : IIndependentVerifier
    {
        public Task<Result> VerifyAsync(
            IReadOnlyList<IReadOnlyList<string>> subsets, byte[] payloadAge, string expectedPayloadText) =>
            Task.FromResult(Result.Success());

        public Task<Result> VerifyForeignReadableAsync() => Task.FromResult(Result.Success());
    }

    sealed class NoopDownloader : IFileDownloader
    {
        public List<(string Filename, byte[] Bytes, string Mime)> Calls { get; } = new();

        // Every test in this file exercises the download happening, not the user
        // cancelling it, so this fake always reports success. The cancel path has
        // its own coverage in OwnerFormValidationTests.
        public ValueTask<bool> DownloadAsync(string filename, byte[] bytes, string mimeType)
        {
            Calls.Add((filename, bytes, mimeType));
            return ValueTask.FromResult(true);
        }
    }

    NoopDownloader RenderWith(out IRenderedComponent<Owner> cut, string seed, string? keyHex, string? check)
    {
        var downloader = new NoopDownloader();
        Services.AddSingleton<IFileDownloader>(downloader);

        cut = RenderComponent<Owner>();
        cut.FindAll(TopLevelSeedSelector).First().Change(seed);
        if (keyHex is not null)
            cut.FindAll(KeyFieldSelector).First().Change(keyHex);
        if (check is not null)
            cut.FindAll(CheckFieldSelector).First().Change(check);

        return downloader;
    }

    [Fact]
    public void With_both_fields_empty_the_page_says_the_generator_will_be_used()
    {
        var cut = RenderComponent<Owner>();

        cut.Markup.Should().Contain("Both fields empty");
        cut.Markup.Should().Contain("random number generator");
    }

    // The default path must be exactly what it was before this feature existed: a random
    // key, a clean download, and the result panel saying where the key came from.
    [Fact]
    public void With_no_key_entered_generation_still_downloads_and_reports_a_generated_key()
    {
        var downloader = RenderWith(out var cut, TwelveWordZeroSeed, null, null);

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            downloader.Calls.Should().ContainSingle();
            cut.Markup.Should().Contain("Generated here");
        }, timeout: TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void A_valid_key_with_its_check_code_reports_a_match_before_generating()
    {
        RenderWith(out var cut, TwelveWordZeroSeed, DiceKeyHex, DiceCheckCode);

        cut.Markup.Should().Contain("Check code matches");
        cut.FindAll(".banner-loud").Should().BeEmpty();
    }

    // The load-bearing test: the key that was entered is the key the payload is encrypted
    // under. Nothing else proves the absence of a silent substitution, because a backup
    // wrapped with a different key looks entirely normal until recovery.
    [Fact]
    public void A_valid_key_is_the_key_the_payload_is_actually_encrypted_under()
    {
        var downloader = RenderWith(out var cut, TwelveWordZeroSeed, DiceKeyHex, DiceCheckCode);

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            downloader.Calls.Should().ContainSingle();
            cut.Markup.Should().Contain("Entered by you");
        }, timeout: TimeSpan.FromSeconds(10));

        // Reads the artifact that actually ships, the armored OpenPGP envelope, and
        // takes both locks off with the entered key. Stronger than reading a bare
        // payload.age, which the bundle no longer contains: this proves the key is
        // right for the file the owner holds, rather than for an intermediate that
        // never leaves the process.
        var shipped = ReadBundleEntry(
            downloader.Calls[0].Bytes, $"payload/{OutputBundleBuilder.PayloadFileName}");

        var unwrapped = PgpEnvelope.Decrypt(shipped, Convert.FromHexString(DiceKeyHex));
        unwrapped.IsSuccess.Should().BeTrue(unwrapped.IsFailure ? unwrapped.Error : "");

        var decrypted = AgePassphrase.Decrypt(unwrapped.Value, Convert.FromHexString(DiceKeyHex));

        decrypted.IsSuccess.Should().BeTrue(decrypted.IsFailure ? decrypted.Error : "");
        System.Text.Encoding.UTF8.GetString(decrypted.Value).Should().Contain("schema_version");
    }

    // And the same key must come back out of the SLIP-39 shares, since those are what a
    // recoverer actually has. Two shares of the 3-of-5 default are not enough, so three.
    [Fact]
    public void The_shares_recombine_to_the_key_that_was_entered()
    {
        var downloader = RenderWith(out var cut, TwelveWordZeroSeed, DiceKeyHex, DiceCheckCode);

        cut.Find("button.btn-primary").Click();
        cut.WaitForAssertion(() => downloader.Calls.Should().ContainSingle(),
            timeout: TimeSpan.FromSeconds(10));

        var mnemonics = ShareMnemonics(downloader.Calls[0].Bytes).Take(3).ToList();
        var recovered = Slip39Wrapping.CombineMnemonics(mnemonics);

        recovered.IsSuccess.Should().BeTrue(recovered.IsFailure ? recovered.Error : "");
        Convert.ToHexStringLower(recovered.Value).Should().Be(DiceKeyHex);
    }

    [Fact]
    public void A_wrong_check_code_refuses_generation_and_downloads_nothing()
    {
        var downloader = RenderWith(out var cut, TwelveWordZeroSeed, DiceKeyHex, "0000");

        // Refused live, before the button is even pressed.
        cut.FindAll(".banner-loud").Should()
            .Contain(el => el.TextContent.Contains("check code does not match"));

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".banner-loud").Should()
                .Contain(el => el.TextContent.Contains("Backup key REFUSED"));
        }, timeout: TimeSpan.FromSeconds(10));

        downloader.Calls.Should().BeEmpty();
    }

    [Fact]
    public void A_key_that_is_not_sixty_four_hex_characters_refuses_generation()
    {
        var downloader = RenderWith(out var cut, TwelveWordZeroSeed, "3dac51a6", DiceCheckCode);

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".banner-loud").Should()
                .Contain(el => el.TextContent.Contains("must be 64 hex characters"));
        }, timeout: TimeSpan.FromSeconds(10));

        downloader.Calls.Should().BeEmpty();
    }

    // The obligation dice-to-seed hands to this app. One roll log used for both the seed
    // and the key makes the key recomputable from the wallet it protects, so this is a
    // refusal and not a warning.
    [Fact]
    public void A_key_equal_to_the_seeds_own_entropy_refuses_generation()
    {
        var downloader = RenderWith(
            out var cut, TwentyFourWordZeroSeed, CollidingKeyHex, CollidingCheckCode);

        cut.FindAll(".banner-loud").Should()
            .Contain(el => el.TextContent.Contains("first 32 bytes"));

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".banner-loud").Should()
                .Contain(el => el.TextContent.Contains("Backup key REFUSED"));
        }, timeout: TimeSpan.FromSeconds(10));

        downloader.Calls.Should().BeEmpty();
    }

    // The 12-word case, where only the first half of the key is the seed's entropy. Just
    // as total a break, and caught by the same rule.
    [Fact]
    public void A_key_whose_first_half_is_a_twelve_word_seeds_entropy_refuses_generation()
    {
        const string halfColliding = "00000000000000000000000000000000ffffffffffffffffffffffffffffffff";
        BackupKeyEntry.CheckCodeFor(halfColliding).Should().Be("b052");

        var downloader = RenderWith(out var cut, TwelveWordZeroSeed, halfColliding, "b052");

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".banner-loud").Should()
                .Contain(el => el.TextContent.Contains("first 16 bytes"));
        }, timeout: TimeSpan.FromSeconds(10));

        downloader.Calls.Should().BeEmpty();
    }

    // A cosigner's own seed words are compared too, not only the top-level field.
    [Fact]
    public void A_collision_with_a_cosigners_own_seed_refuses_generation()
    {
        var downloader = new NoopDownloader();
        Services.AddSingleton<IFileDownloader>(downloader);

        var cut = RenderComponent<Owner>();
        cut.FindAll("input[placeholder='abandon ability able about ...']").First()
            .Change(TwentyFourWordZeroSeed);
        cut.FindAll(KeyFieldSelector).First().Change(CollidingKeyHex);
        cut.FindAll(CheckFieldSelector).First().Change(CollidingCheckCode);

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".banner-loud").Should()
                .Contain(el => el.TextContent.Contains("Backup key REFUSED"));
        }, timeout: TimeSpan.FromSeconds(10));

        downloader.Calls.Should().BeEmpty();
    }

    // Seed words the tool cannot read as BIP-39 refuse a pasted key, because the comparison
    // that would have caught a reused roll log cannot run against them. Nothing changes for
    // a user who pastes no key: this only fires when a key is supplied.
    [Fact]
    public void Seed_words_that_are_not_valid_bip39_refuse_a_pasted_key()
    {
        var downloader = RenderWith(out var cut, "not a valid mnemonic", DiceKeyHex, DiceCheckCode);

        cut.FindAll(".banner-loud").Should()
            .Contain(el => el.TextContent.Contains("does not read as BIP-39"));

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".banner-loud").Should()
                .Contain(el => el.TextContent.Contains("Correct or clear those words"));
        }, timeout: TimeSpan.FromSeconds(10));

        downloader.Calls.Should().BeEmpty();
    }

    // THE CASE THAT WAS SILENTLY ACCEPTED. One word of a valid 12-word seed changed, "fit"
    // to "fix": both are on the English list, so only the checksum objects, and the words
    // look right on screen. That seed is the same roll log as the pasted key, whose first 16
    // bytes are its entropy, so the earlier build shipped a backup whose key is derivable
    // from the wallet it protects.
    [Fact]
    public void A_seed_with_one_word_mistyped_refuses_and_downloads_nothing()
    {
        var downloader = RenderWith(
            out var cut,
            "diet glad hat rural panther lawsuit act drop gallery urge where fix",
            DiceKeyHex, DiceCheckCode);

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".banner-loud").Should()
                .Contain(el => el.TextContent.Contains("Backup key REFUSED")
                               && el.TextContent.Contains("not read as BIP-39"));
        }, timeout: TimeSpan.FromSeconds(10));

        downloader.Calls.Should().BeEmpty();
    }

    // A state that blocks generation is never rendered as body text. hint-loud is how this
    // page notes a caveat; a refusal must look like every other blocking condition, which is
    // banner-loud.
    [Fact]
    public void A_refusing_state_is_never_presented_as_a_mere_note()
    {
        RenderWith(out var cut, "not a valid mnemonic", DiceKeyHex, DiceCheckCode);

        cut.FindAll(".hint-loud").Should().BeEmpty();
        cut.FindAll(".banner-loud").Should().NotBeEmpty();
    }

    // The result panel's claim that the key was checked against the seed words must not be
    // reachable without a seed having been checked. Generation with no seed at all stops at
    // the seed gate, so the panel never renders.
    [Fact]
    public void A_pasted_key_with_no_seed_at_all_stops_at_the_seed_gate()
    {
        var downloader = new NoopDownloader();
        Services.AddSingleton<IFileDownloader>(downloader);

        var cut = RenderComponent<Owner>();
        cut.FindAll(KeyFieldSelector).First().Change(DiceKeyHex);
        cut.FindAll(CheckFieldSelector).First().Change(DiceCheckCode);

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("At least one seed must be provided");
        }, timeout: TimeSpan.FromSeconds(10));

        cut.Markup.Should().NotContain("Entered by you");
        downloader.Calls.Should().BeEmpty();
    }

    // Nothing on the page may repeat the key back except the field it was typed into, and
    // the check code the page computed must never be displayed either: it is the value the
    // user is supposed to bring from the other screen.
    [Fact]
    public void The_page_never_renders_the_key_outside_the_field_it_was_typed_into()
    {
        var downloader = RenderWith(out var cut, TwelveWordZeroSeed, DiceKeyHex, DiceCheckCode);

        cut.Find("button.btn-primary").Click();
        cut.WaitForAssertion(() => downloader.Calls.Should().ContainSingle(),
            timeout: TimeSpan.FromSeconds(10));

        // The only occurrence of the key in the markup is the value attribute of the input
        // the user typed it into.
        var occurrences = cut.Markup.Split(DiceKeyHex).Length - 1;
        occurrences.Should().Be(1);
        cut.FindAll(KeyFieldSelector).First().GetAttribute("value").Should().Be(DiceKeyHex);

        // The transcript panel describes the mechanism and must not carry the key.
        cut.Markup.Should().NotContain(DiceKeyHex.ToUpperInvariant());
    }

    [Fact]
    public void A_refused_key_never_shows_the_correct_check_code()
    {
        RenderWith(out var cut, TwelveWordZeroSeed, DiceKeyHex, "0000");

        cut.Markup.Should().NotContain(DiceCheckCode);
    }

    static byte[] ReadBundleEntry(byte[] zipBytes, string entryName)
    {
        using var archive = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read);
        using var entry = archive.GetEntry(entryName)!.Open();
        using var buffer = new MemoryStream();
        entry.CopyTo(buffer);
        return buffer.ToArray();
    }

    // Pulls each share's mnemonic out of the nested share zips in the bundle. Each
    // shares/<name>.zip holds the SLIP-39 words in its own file; the mnemonic is the line
    // of 33 wordlist words, which is what CombineMnemonics wants back.
    static IReadOnlyList<string> ShareMnemonics(byte[] bundleBytes)
    {
        using var bundle = new ZipArchive(new MemoryStream(bundleBytes), ZipArchiveMode.Read);

        return bundle.Entries
            .Where(entry => entry.FullName.StartsWith("shares/") && entry.FullName.EndsWith(".zip"))
            .Select(entry =>
            {
                using var shareStream = entry.Open();
                using var shareBuffer = new MemoryStream();
                shareStream.CopyTo(shareBuffer);
                using var share = new ZipArchive(new MemoryStream(shareBuffer.ToArray()), ZipArchiveMode.Read);

                return share.Entries
                    .Select(ReadAllText)
                    .SelectMany(text => text.Split('\n'))
                    .Select(line => line.Trim())
                    .First(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 33);
            })
            .ToList();
    }

    static string ReadAllText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
