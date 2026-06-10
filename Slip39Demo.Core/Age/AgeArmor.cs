using CSharpFunctionalExtensions;

namespace Slip39Demo.Core.Age;

// PEM-style ASCII armor for age binary ciphertext. Used when payload.age must
// be embedded in a text channel (PM secure note, printed PDF). Same fences as
// the age reference CLI's --armor output, so any `age -d` accepts our output
// directly without re-decoding.
//
// Wire format (matches age v1 spec §5):
//   -----BEGIN AGE ENCRYPTED FILE-----
//   <base64 body wrapped at 64 chars per line>
//   -----END AGE ENCRYPTED FILE-----
//
// Encode is a pure transform — cannot fail, returns string.
// Decode validates fences + base64 — returns Result<byte[]>.
public static class AgeArmor
{
    const string BeginFence = "-----BEGIN AGE ENCRYPTED FILE-----";
    const string EndFence = "-----END AGE ENCRYPTED FILE-----";
    const int LineWidth = 64;

    // Encodes binary -> base64 body chunked into 64-char lines and wrapped in
    // the BEGIN/END fences. Always terminates with a trailing newline so the
    // output is a complete text file (avoids "no newline at end of file" diffs
    // when the armor is stored as a file or pasted into a PM note).
    public static string Encode(byte[] data) =>
        string.Join('\n',
            new[] { BeginFence }
                .Concat(Convert.ToBase64String(data).Chunk(LineWidth).Select(c => new string(c)))
                .Append(EndFence)
                .Append(string.Empty)); // trailing empty -> final '\n'

    // Decodes armored text back to the original binary payload. Accepts both
    // LF and CRLF line endings (Windows PMs often inject CRLF on paste).
    // Failure modes surfaced to callers:
    //   - fences missing, out of order, or absent
    //   - body is not valid base64 after concatenation
    public static Result<byte[]> Decode(string armored)
    {
        var lines = armored.Replace("\r\n", "\n").Split('\n');
        var beginIdx = Array.IndexOf(lines, BeginFence);
        var endIdx = Array.IndexOf(lines, EndFence);
        if (beginIdx < 0 || endIdx < 0 || endIdx <= beginIdx)
            return Result.Failure<byte[]>("missing or malformed AGE armor fences");

        // Concatenate the body lines between (but not including) the fences.
        var body = string.Concat(lines.Skip(beginIdx + 1).Take(endIdx - beginIdx - 1));
        try
        {
            return Convert.FromBase64String(body);
        }
        catch (FormatException ex)
        {
            return Result.Failure<byte[]>($"armored body is not valid base64: {ex.Message}");
        }
    }
}
