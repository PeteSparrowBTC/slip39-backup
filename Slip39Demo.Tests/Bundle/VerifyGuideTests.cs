using FluentAssertions;
using Slip39Demo.Core.Bundle;
using Xunit;

namespace Slip39Demo.Tests.Bundle;

// VerifyGuide is the only part of the bundle that tells the owner how to check
// this tool WITHOUT this tool. It is static text, so there is nothing to
// round-trip; what these tests defend is that the load-bearing sentences do not
// quietly go missing in an edit. Each assertion below corresponds to a step
// whose absence would leave the reader unable to finish, or would leave them
// misinformed in a way that costs money.
public class VerifyGuideTests
{
    [Fact]
    public void Guide_ShipsUnderAStableFileName() =>
        VerifyGuide.FileName.Should().Be("VERIFY-THIS-BACKUP.txt");

    [Fact]
    public void Guide_TellsTheReaderToActBeforeDistributingShares() =>
        VerifyGuide.Text.Should().Contain("BEFORE you hand out any share");

    // The reader cannot decrypt anything without first turning shares back into
    // the key, and age is not on Tails, so both acquisition steps must survive.
    [Theory]
    [InlineData("github.com/FiloSottile/age/releases", "where to get the reference age implementation")]
    [InlineData("github.com/str4d/rage/releases", "where to get the independent Rust implementation")]
    [InlineData("github.com/trezor/python-shamir-mnemonic", "the SLIP-39 spec authors' own tool")]
    [InlineData("github.com/3rdIteration/slip39", "the third-party browser SLIP-39 page")]
    [InlineData("Tails does not come with any of them", "the fact that forces the USB-stick approach")]
    [InlineData("pip install --no-index -f shamir-pkgs", "installing the SLIP-39 tool with no internet")]
    [InlineData("pip download shamir-mnemonic", "fetching it on the online machine first")]
    // Pinned to a real released filename. The guide previously named a v1.2.x
    // file that no longer exists on the releases page.
    [InlineData("age-v1.3.1-linux-amd64.tar.gz", "a filename that actually exists upstream")]
    [InlineData("sha256sum age-v1.3.1-linux-amd64.tar.gz", "fingerprinting the download before the USB hop")]
    public void Guide_ExplainsHowToObtainTheTools(string fragment, string why) =>
        VerifyGuide.Text.Should().Contain(fragment, why);

    [Theory]
    [InlineData("gpg -d payload.age.gpg.asc", "taking off the outer OpenPGP lock")]
    [InlineData("age -d payload.age", "taking off the inner age lock with the same key")]
    [InlineData("sha256sum payload.age.gpg.asc", "checking the shipped file against the record")]
    [InlineData("shamir recover", "rebuilding the key from shares")]
    public void Guide_GivesTheActualCommands(string fragment, string why) =>
        VerifyGuide.Text.Should().Contain(fragment, why);

    // One artifact means there is no second encoding to diff against, so what the
    // guide has to convey instead is that the two locks are opened by two unrelated
    // programs. That agreement is what the exercise proves.
    [Fact]
    public void Guide_ShowsBothLocksOpenedByDifferentPrograms() =>
        VerifyGuide.Text.Should().Contain("written by different");

    // A reader who sees a different checksum on a second, independently produced
    // blob must not conclude something is broken. age is randomised, so two
    // encryptions of the same secret never match, and identical output would be
    // the actual defect. Getting this wrong sends people chasing a phantom.
    [Fact]
    public void Guide_ExplainsThatASecondBlobHasADifferentChecksumByDesign()
    {
        VerifyGuide.Text.Should().Contain("Expect a DIFFERENT sha256");
        VerifyGuide.Text.Should().Contain("Identical output would be the");
    }

    // The old iancoleman page rejects this tool's extendable shares. Someone
    // verifying a perfectly good backup there would conclude it is corrupt.
    [Fact]
    public void Guide_WarnsAboutTheSlip39PageThatRejectsValidShares()
    {
        VerifyGuide.Text.Should().Contain("Do NOT use the older page at iancoleman.io/slip39");
        VerifyGuide.Text.Should().Contain("extendable-backup flag");
    }

    // The procedure writes the wallet in the clear. Saying so, and saying to
    // clean up, is not optional.
    [Fact]
    public void Guide_WarnsThatItWritesPlaintextAndSaysHowToCleanUp()
    {
        VerifyGuide.Text.Should().Contain("decrypts your wallet to a plain file");
        VerifyGuide.Text.Should().Contain("rm check.txt");
    }

    // The passphrase whitespace trap: PayloadParser trims leading whitespace off
    // values, so a passphrase entered with a leading space recovers a different
    // wallet while every automated check still passes. Reading the decrypted
    // text carefully is the only defence, so the guide must ask for it.
    [Fact]
    public void Guide_TellsTheReaderToCompareTheDecryptedPassphraseCharacterByCharacter()
    {
        VerifyGuide.Text.Should().Contain("character for");
        VerifyGuide.Text.Should().Contain("leading space");
    }

    // Printed and read under stress, so keep it inside a paper-friendly width.
    // 72 rather than 66 because one command does not usefully wrap, and a command
    // split across lines is a transcription error waiting to happen.
    [Fact]
    public void Guide_StaysWithinAPrintableLineWidth() =>
        VerifyGuide.Text.Split('\n').Should().OnlyContain(l => l.TrimEnd().Length <= 72);

    [Fact]
    public void PayloadReadme_PointsAtTheGuide() =>
        PayloadReadme.Text.Should().Contain(VerifyGuide.FileName);



    // age publishes sigsum .proof files, not a checksums list. An instruction to
    // "compare against the published value" sends the reader looking for
    // something that does not exist.
    [Fact]
    public void Guide_DoesNotClaimAgePublishesAChecksumsList()
    {
        VerifyGuide.Text.Should().NotContain("The same page publishes checksums");
        VerifyGuide.Text.Should().Contain(".proof");
    }

    // "Independent" is a claim about authorship, not about code. The guide has to
    // name who wrote each tool, or the reader has no way to judge whether the
    // agreement between them means anything.
    [Theory]
    [InlineData("Filippo Valsorda", "age's author")]
    [InlineData("SatoshiLabs", "who wrote the SLIP-39 specification")]
    [InlineData("str4d", "rage's author, unrelated to age's")]
    public void Guide_NamesWhoWroteEachTool(string who, string why) =>
        VerifyGuide.Text.Should().Contain(who, why);

    // Nothing in this procedure may route the reader through software this
    // project produced. If it did, it would stop being an independent check,
    // which is the only reason the document exists.
    [Fact]
    public void Guide_SendsTheReaderOnlyToUpstreamProjects() =>
        VerifyGuide.Text.Should().NotContain("VERIFY-IN-BROWSER",
            "a checker shipped in our own bundle cannot verify our own bundle");

    // This procedure decrypts the wallet in the clear, and the guide's own
    // warning says to do it only on the offline machine. Listing a macOS or
    // Windows download quietly contradicts that: it invites the reader to verify
    // on their everyday laptop. The only download filenames offered are Linux
    // ones, and the macOS and Windows builds are named exactly once, to be
    // dismissed.
    [Fact]
    public void Guide_OffersLinuxDownloadsOnly()
    {
        var text = VerifyGuide.Text;

        text.Should().NotContain("darwin", "macOS build filenames must not be offered");
        text.Should().NotContain("windows-amd64", "Windows build filenames must not be offered");
        text.Should().Contain("age-v1.3.1-linux-amd64.tar.gz");
        text.Should().Contain("rage-v0.12.1-x86_64-linux.tar.gz");
        text.Should().Contain("Ignore them", "the other builds are mentioned only to be waved off");
    }

    // Neither project ships an AppImage, and neither needs to: both archives hold
    // a single self-contained program. Readers who go looking for an AppImage and
    // find none should not conclude they have the wrong download.
    [Fact]
    public void Guide_SaysNoAppImageIsNeeded() =>
        VerifyGuide.Text.Should().Contain("There is no AppImage and none is needed");

    // rage's ordinary Linux build is dynamically linked (glibc 2.34 as of
    // v0.12.1) while age's is fully static. Current Tails satisfies that, but the
    // musl package is the escape hatch if a future Tails does not, and it can be
    // unpacked without installing anything, which matters on an amnesic system.
    [Fact]
    public void Guide_OffersTheMuslFallbackForRage()
    {
        VerifyGuide.Text.Should().Contain("rage-musl_0.12.1-1_amd64.deb");
        VerifyGuide.Text.Should().Contain("dpkg-deb -x", "it must be unpacked, not installed");
    }

    // B6 used to reference ./rage without ever unpacking it.
    [Fact]
    public void Guide_UnpacksRageBeforeUsingIt()
    {
        var text = VerifyGuide.Text;
        text.IndexOf("tar -xzf rage-", StringComparison.Ordinal)
            .Should().BeGreaterThan(0).And
            .BeLessThan(text.IndexOf("rage/rage -d", StringComparison.Ordinal));
    }
}
