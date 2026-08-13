using System.IO.Compression;

namespace Slip39Demo.Core.Bundle;

// Writes a ShareFolder dictionary as a zip archive in memory. Determinism is
// essential (verification-record SHA256s must be stable across rebuilds), so:
//   - entries are written in ordinal-sorted name order
//   - LastWriteTime is fixed to a constant (2020-01-01 UTC)
//   - compression mode is NoCompression (zip "stored", no DEFLATE)
//     so even a hypothetical DEFLATE implementation difference cannot
//     introduce byte-level non-determinism
public static class ShareZipWriter
{
    static readonly DateTimeOffset FixedTimestamp = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static byte[] Write(IReadOnlyDictionary<string, byte[]> folder)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in folder.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                var zipEntry = archive.CreateEntry(entry.Key, CompressionLevel.NoCompression);
                zipEntry.LastWriteTime = FixedTimestamp;
                using var es = zipEntry.Open();
                es.Write(entry.Value, 0, entry.Value.Length);
            }
        }
        return ms.ToArray();
    }
}
