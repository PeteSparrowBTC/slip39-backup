using System.IO.Compression;
using System.Text;

namespace Slip39Demo.Core.Bundle;

// Composes the final output.zip that Owner mode hands to the user. Tree:
//   shares/<name>.zip          one entry per share, content from ShareZipWriter
//   payload/payload.age        ciphertext bytes
//   payload/payload.age.txt    ASCII-armored ciphertext
//   payload/payload.age.gpg    the same blob inside an OpenPGP AES-256 envelope
//   payload/IMPORTANT-READ-FIRST.txt   PayloadReadme.Text
//   payload/VERIFY-THIS-BACKUP.txt     VerifyGuide.Text (owner, do it now)
//   verification-record.txt    VerificationRecord.Build(...) text
//   MANUAL-RECOVERY.txt        tool-independent recovery manual (heir, later)
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
        string verificationRecordText,
        byte[]? payloadAgeGpg = null)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, bytes) in shareZips)
                AddEntry(archive, $"shares/{name}", bytes);

            AddEntry(archive, "payload/payload.age", payloadAgeBinary);
            AddEntry(archive, "payload/payload.age.txt", Encoding.UTF8.GetBytes(payloadAgeArmoredText));
            // The double-wrapped copy. Shipped ALONGSIDE payload.age rather than
            // replacing it: an heir who manages only the age step still recovers
            // the wallet, and the extra layer is protection, not a gate.
            if (payloadAgeGpg is not null)
                AddEntry(archive, "payload/payload.age.gpg", payloadAgeGpg);
            AddEntry(archive, "payload/IMPORTANT-READ-FIRST.txt", Encoding.UTF8.GetBytes(PayloadReadme.Text));
            // Sits next to the blob it tells the owner to verify, so whoever opens
            // the folder holding the ciphertext finds the procedure for proving it
            // decrypts without this tool.
            AddEntry(archive, $"payload/{VerifyGuide.FileName}", Encoding.UTF8.GetBytes(VerifyGuide.Text));
            AddEntry(archive, "verification-record.txt", Encoding.UTF8.GetBytes(verificationRecordText));
            // Tool-independent recovery manual at the bundle root too (it is also
            // inside every share zip): the owner's master copy should be complete
            // even if the share zips are already distributed.
            AddEntry(archive, ManualRecoveryGuide.FileName, Encoding.UTF8.GetBytes(ManualRecoveryGuide.Text));
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
