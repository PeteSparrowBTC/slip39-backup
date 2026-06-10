using System.IO.Compression;
using System.Text;

namespace Slip39Demo.Core.Bundle;

// Composes the final output.zip that Owner mode hands to the user. Tree:
//   shares/<name>.zip          one entry per share, content from ShareZipWriter
//   payload/payload.age        ciphertext bytes
//   payload/payload.age.txt    ASCII-armored ciphertext
//   payload/IMPORTANT-READ-FIRST.txt   PayloadReadme.Text
//   verification-record.txt    VerificationRecord.Build(...) text
//
// The user receives ONE file via browser download, copies it onto their
// output USB, then distributes pieces from there as per spec §6.4.
public static class OutputBundleBuilder
{
    static readonly DateTimeOffset FixedTimestamp = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static byte[] Build(
        IReadOnlyList<(string FileName, byte[] ZipBytes)> shareZips,
        byte[] payloadAgeBinary,
        string payloadAgeArmoredText,
        string verificationRecordText)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, bytes) in shareZips)
                AddEntry(archive, $"shares/{name}", bytes);

            AddEntry(archive, "payload/payload.age", payloadAgeBinary);
            AddEntry(archive, "payload/payload.age.txt", Encoding.UTF8.GetBytes(payloadAgeArmoredText));
            AddEntry(archive, "payload/IMPORTANT-READ-FIRST.txt", Encoding.UTF8.GetBytes(PayloadReadme.Text));
            AddEntry(archive, "verification-record.txt", Encoding.UTF8.GetBytes(verificationRecordText));
        }
        return ms.ToArray();
    }

    static void AddEntry(ZipArchive archive, string fullName, byte[] data)
    {
        var entry = archive.CreateEntry(fullName, CompressionLevel.NoCompression);
        entry.LastWriteTime = FixedTimestamp;
        using var es = entry.Open();
        es.Write(data, 0, data.Length);
    }
}
