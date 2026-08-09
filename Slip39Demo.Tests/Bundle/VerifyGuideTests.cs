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
    [InlineData("Tails does not come with age", "the fact that forces the USB-stick approach")]
    [InlineData("pip install --no-index -f shamir-pkgs", "installing the SLIP-39 tool with no internet")]
    [InlineData("pip download shamir-mnemonic", "fetching it on the online machine first")]
    [InlineData("sha256sum age-v1.2.x-linux-amd64.tar.gz", "verifying the downloaded age binary itself")]
    public void Guide_ExplainsHowToObtainTheTools(string fragment, string why) =>
        VerifyGuide.Text.Should().Contain(fragment, why);

    [Theory]
    [InlineData("age -d payload.age", "decrypting the binary blob")]
    [InlineData("age -d payload.age.txt", "decrypting the armored copy")]
    [InlineData("sha256sum payload.age", "checking the blob against the verification record")]
    [InlineData("shamir recover", "rebuilding the key from shares")]
    public void Guide_GivesTheActualCommands(string fragment, string why) =>
        VerifyGuide.Text.Should().Contain(fragment, why);

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
}
