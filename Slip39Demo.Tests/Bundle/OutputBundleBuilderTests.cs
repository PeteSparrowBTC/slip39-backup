using System.IO.Compression;
using FluentAssertions;
using Slip39Demo.Core.Age;
using Slip39Demo.Core.Bundle;
using Xunit;

namespace Slip39Demo.Tests.Bundle;

public class OutputBundleBuilderTests
{
    [Fact]
    public void Build_ProducesExpectedTopLevelStructure()
    {
        var shareZips = new[]
        {
            ("share-1-of-3.zip", ShareZipWriter.Write(ShareFolder.Build("readme1", "mnemonic-one"))),
            ("share-2-of-3.zip", ShareZipWriter.Write(ShareFolder.Build("readme2", "mnemonic-two"))),
            ("share-3-of-3.zip", ShareZipWriter.Write(ShareFolder.Build("readme3", "mnemonic-three"))),
        };
        var ciphertext = "pretend-age-ciphertext"u8.ToArray();
        var armored = AgeArmor.Encode(ciphertext);
        var verificationRecord = "fake verification record\nline 2\n";

        var bundle = OutputBundleBuilder.Build(shareZips, ciphertext, armored, verificationRecord);

        using var ms = new MemoryStream(bundle);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        var names = archive.Entries.Select(e => e.FullName).OrderBy(n => n, StringComparer.Ordinal).ToList();

        names.Should().BeEquivalentTo([
            "payload/IMPORTANT-READ-FIRST.txt",
            "payload/payload.age",
            "payload/payload.age.txt",
            "shares/share-1-of-3.zip",
            "shares/share-2-of-3.zip",
            "shares/share-3-of-3.zip",
            "verification-record.txt",
        ]);

        using var armorReader = new StreamReader(archive.GetEntry("payload/payload.age.txt")!.Open());
        armorReader.ReadToEnd().Should().Be(armored);
    }

    [Fact]
    public void PayloadReadme_ExplainsOwnerOnlyDistribution()
    {
        var text = PayloadReadme.Text;

        text.Should().Contain("OWNER", because: "this folder is for the owner, not share-holders");
        text.Should().Contain("payload.age");
        text.Should().Contain("password manager");
    }
}
