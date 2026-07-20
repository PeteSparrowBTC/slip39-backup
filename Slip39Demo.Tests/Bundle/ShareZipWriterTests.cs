using System.IO.Compression;
using FluentAssertions;
using Slip39Demo.Core.Bundle;
using Xunit;

namespace Slip39Demo.Tests.Bundle;

public class ShareZipWriterTests
{
    [Fact]
    public void Write_ProducesValidZipWithExpectedEntries()
    {
        var folder = ShareFolder.Build(
            readmeText: "hello world readme",
            mnemonicText: "abandon ability able");

        var zipBytes = ShareZipWriter.Write(folder);

        using var ms = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

        archive.Entries.Select(e => e.FullName).Should().BeEquivalentTo(["README.txt", "share.slip39", "share-qr.png", "MANUAL-RECOVERY.txt"]);
        using var reader = new StreamReader(archive.GetEntry("share.slip39")!.Open());
        reader.ReadToEnd().Should().Be("abandon ability able\n");
    }

    [Fact]
    public void Write_IsDeterministicAcrossInvocations()
    {
        var folder = ShareFolder.Build("readme", "mnemonic");

        var first  = ShareZipWriter.Write(folder);
        var second = ShareZipWriter.Write(folder);

        first.Should().Equal(second);
    }
}
