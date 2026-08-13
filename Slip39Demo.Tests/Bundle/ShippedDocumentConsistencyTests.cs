using FluentAssertions;
using Slip39Demo.Core.Bundle;
using Xunit;

namespace Slip39Demo.Tests.Bundle;

// Invariants across every document written INTO a backup, rather than facts about one of
// them.
//
// WHY THIS FILE EXISTS
// These documents are shipped artifacts, not documentation. A bundle generated today
// carries today's text to an heir who may open it in 2040, and nobody can correct it in
// between. That makes a wrong sentence here worse than a wrong sentence in the README,
// which anyone can fix and republish in a minute.
//
// Every assertion below corresponds to drift that actually happened. When the payload
// went from three files to one, and again when the OpenPGP lock became mandatory, the
// code was updated and these documents were not: MANUAL-RECOVERY.txt still opened by
// telling the heir that "any SLIP-39 implementation plus any age implementation can
// rebuild the wallet", which had stopped being true, and VERIFY-THIS-BACKUP.txt still
// told the owner to verify a file the bundle no longer contained. Both passed every test
// in the suite, because no test compared one document against another or against the
// builder.
public class ShippedDocumentConsistencyTests
{
    // Everything a bundle carries, paired with a name for the failure message.
    public static TheoryData<string, string> Documents() => new()
    {
        { "MANUAL-RECOVERY.txt", ManualRecoveryGuide.Text },
        { "VERIFY-THIS-BACKUP.txt", VerifyGuide.Text },
        { "IMPORTANT-READ-FIRST.txt", PayloadReadme.Text },
        { "share README.txt", ReadmeTemplate.Build("family", 1, 3, "2026-08-13", "9.9.9", testOnly: false) },
    };

    // A document that names the ciphertext at all must name the one the bundle actually
    // contains. Retired spellings are allowed, but only alongside the current one, which
    // is what makes them a compatibility note rather than a wrong instruction.
    [Theory]
    [MemberData(nameof(Documents))]
    public void A_document_that_names_the_ciphertext_names_the_one_that_ships(string name, string text)
    {
        if (!text.Contains("payload.age", StringComparison.Ordinal))
            return; // the share README does not have to mention it at all

        text.Should().Contain(OutputBundleBuilder.PayloadFileName,
            $"{name} names a ciphertext, so it must name {OutputBundleBuilder.PayloadFileName}");
    }

    // Recovery needs BOTH locks off, in order, and the two documents that walk somebody
    // through it must say so. This is the assertion that would have caught the intro
    // paragraph promising recovery from SLIP-39 and age alone.
    [Theory]
    [InlineData("MANUAL-RECOVERY.txt")]
    [InlineData("VERIFY-THIS-BACKUP.txt")]
    public void A_recovery_document_gives_both_locks_in_order(string name)
    {
        var text = name == "MANUAL-RECOVERY.txt" ? ManualRecoveryGuide.Text : VerifyGuide.Text;

        var outer = text.IndexOf($"gpg -d {OutputBundleBuilder.PayloadFileName}", StringComparison.Ordinal);
        var inner = text.IndexOf("age -d payload.age", StringComparison.Ordinal);

        outer.Should().BeGreaterThan(-1, $"{name} must give the OpenPGP command");
        inner.Should().BeGreaterThan(-1, $"{name} must give the age command");
        outer.Should().BeLessThan(inner,
            $"{name} must unwrap before it decrypts, or the reader runs age against an OpenPGP file");
    }

    // GnuPG is a tool the reader has to have, so a document that lists what recovery
    // needs cannot list only the other two. Tails ships GnuPG and does not ship age,
    // which is why this was easy to leave out: it happens to be there already.
    [Theory]
    [MemberData(nameof(Documents))]
    public void A_document_listing_the_tools_does_not_omit_GnuPG(string name, string text)
    {
        var claimsToolIndependence = text.Contains("standard SLIP-39", StringComparison.Ordinal)
                                  || text.Contains("open standards", StringComparison.Ordinal);
        if (!claimsToolIndependence)
            return;

        text.Should().MatchRegex("GnuPG|gpg",
            $"{name} tells the reader which implementations they need, and one of them is GnuPG");
    }

    // No shipped document may name the download, because it is named after the wallet
    // label and the date and these documents are inside it. "output.zip" was the name
    // for a long time and outlived it in prose.
    [Theory]
    [MemberData(nameof(Documents))]
    public void No_document_calls_the_download_output_zip(string name, string text) =>
        text.Should().NotContain("output.zip", $"{name} still uses the download's old name");

    // Read on paper, under stress, possibly by somebody who has never seen a terminal. A
    // line wider than the page wraps somewhere the author did not choose, and that place
    // is often the middle of a command.
    //
    // The limits differ because two documents contain one thing each that cannot be
    // wrapped without breaking it, and pinning the real number is more useful than
    // picking a round one that hides them:
    //
    //   MANUAL-RECOVERY.txt   82   the offline pip install, one command on one line
    //   share README.txt      89   a column of project URLs
    //
    // A new line wider than its document's limit fails here. Raising a number is then a
    // deliberate edit rather than something that happens by accident.
    [Theory]
    [InlineData("MANUAL-RECOVERY.txt", 82)]
    [InlineData("VERIFY-THIS-BACKUP.txt", 72)]
    [InlineData("IMPORTANT-READ-FIRST.txt", 72)]
    [InlineData("share README.txt", 89)]
    public void A_document_stays_within_its_printable_width(string name, int limit)
    {
        var text = Documents().Cast<object[]>().Single(row => (string)row[0] == name)[1] as string;

        text!.Split('\n').Should().OnlyContain(line => line.TrimEnd().Length <= limit,
            $"{name} is printed and read on paper, at {limit} columns");
    }

    // Byte-for-byte identical whoever builds it. ReadmeTemplate builds with AppendLine,
    // which emits Environment.NewLine, so the share README used to ship with CRLF from a
    // Windows build and LF from the Linux CI that produces the AppImage. ShareZipWriter
    // goes to some trouble to make share zips reproducible (fixed timestamps, no
    // compression variance); a document whose bytes depend on the build host defeats
    // that, and the difference is invisible to everyone until two checksums disagree.
    [Fact]
    public void The_share_readme_uses_one_line_ending_everywhere() =>
        ReadmeTemplate.Build("family", 1, 3, "2026-08-13", "9.9.9", testOnly: false)
            .Should().NotContain("\r", "line endings must not depend on the machine that built the bundle");
}
