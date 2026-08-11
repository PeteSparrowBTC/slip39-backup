using System.IO.Compression;
using System.Text;

namespace Slip39Demo.Core.Bundle;

// Composes the final output.zip that Owner mode hands to the user. Tree:
//   shares/<name>.zip          one entry per share, content from ShareZipWriter
//   payload/payload.age.gpg.asc        the ONLY ciphertext: the age file inside an
//                                      OpenPGP AES-256 envelope, ASCII armored
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

    // The single ciphertext file name, shared with the verification record and the
    // documents so none of them can name a file the bundle does not contain. The .asc
    // extension is the conventional marker for ASCII-armored OpenPGP.
    public const string PayloadFileName = "payload.age.gpg.asc";

    public static byte[] Build(
        IReadOnlyList<(string FileName, byte[] ZipBytes)> shareZips,
        string payloadArmoredText,
        string verificationRecordText)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, bytes) in shareZips)
                AddEntry(archive, $"shares/{name}", bytes);

            // ONE ciphertext. Three files used to ship here: payload.age,
            // payload.age.txt and payload.age.gpg.
            //
            // Shipping the unwrapped forms beside the wrapped one destroyed the reason
            // the wrapper exists. Its claim is that an attacker must break BOTH age and
            // OpenPGP; an attacker holding this folder simply takes payload.age and
            // breaks one. The storage guidance made that certain rather than possible,
            // since it told the owner to put payload.age in the password manager, on the
            // USB and in the safe. The justification was availability, which is real, and
            // it was bought with the whole of the confidentiality argument.
            AddEntry(archive, $"payload/{PayloadFileName}", Encoding.UTF8.GetBytes(payloadArmoredText));
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
