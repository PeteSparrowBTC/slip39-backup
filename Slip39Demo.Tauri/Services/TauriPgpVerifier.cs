using System.Text.Json.Serialization;
using Slip39Demo.UI.Services;

namespace Slip39Demo.Tauri.Services;

// What src-tauri/src/gpg.rs reports back. Init-only properties: nothing should be able
// to edit a report of what already happened. The names match the camelCase that GpgRun's
// serde(rename_all) produces.
public sealed record GpgRunDto
{
    [JsonPropertyName("exitCode")] public int ExitCode { get; init; }
    [JsonPropertyName("stdoutB64")] public string StdoutB64 { get; init; } = "";
    [JsonPropertyName("stderrText")] public string StderrText { get; init; } = "";
    [JsonPropertyName("version")] public string Version { get; init; } = "";
    [JsonPropertyName("gpgMissing")] public bool GpgMissing { get; init; }
}

// Runs the system GnuPG against the envelope this build just produced, and checks that
// what comes out is byte-for-byte the age file that went in.
//
// The judgement lives here rather than in gpg.rs: that module reports what gpg said and
// applies no policy, so everything this class decides is visible in one place with tests
// around it. The policy about what a given outcome COSTS (refuse the backup, or note it)
// belongs one level further out again, in Owner, because it depends on whether the
// backup being made is real or a watermarked practice run.
public sealed class TauriPgpVerifier(ITauriInterop interop) : IOuterLockVerifier
{
    public async Task<OuterLockVerification> VerifyAsync(
        string armoredEnvelope, byte[] expectedInner, byte[] key32)
    {
        if (key32.Length != 32)
            return OuterLockVerification.Failed($"key must be 32 bytes (got {key32.Length})");

        GpgRunDto run;
        try
        {
            run = await interop.InvokeAsync<GpgRunDto>("gpg_decrypt", new
            {
                armored = armoredEnvelope,
                // The same 64-character lowercase hex encoding PgpEnvelope uses. If the
                // two ever disagree, gpg refuses the passphrase and this check fails,
                // which is exactly the kind of mistake it exists to catch.
                passphrase = Convert.ToHexString(key32).ToLowerInvariant(),
            });
        }
        catch (Exception ex)
        {
            // An interop exception is not evidence about the backup, so it is Unavailable
            // rather than Failed. The caller still refuses a real backup on it: "the
            // check could not run" and "the check said no" both mean nobody independent
            // has opened this file.
            return OuterLockVerification.Unavailable($"the shell could not run gpg: {ex.Message}");
        }

        if (run.GpgMissing)
            return OuterLockVerification.Unavailable(
                string.IsNullOrWhiteSpace(run.StderrText)
                    ? "GnuPG could not be run on this machine."
                    : run.StderrText.Trim());

        if (run.ExitCode != 0)
            return OuterLockVerification.Failed(
                $"gpg could not open the envelope this build just produced (exit code "
                + $"{run.ExitCode}): {run.StderrText.Trim()}");

        var opened = Convert.FromBase64String(run.StdoutB64);
        if (!opened.SequenceEqual(expectedInner))
            return OuterLockVerification.Failed(
                $"gpg opened the envelope but returned different bytes than were put in "
                + $"({expectedInner.Length} bytes in, {opened.Length} bytes out). The outer "
                + "layer is not carrying the age file intact.");

        return OuterLockVerification.Verified(
            $"Opened by {Describe(run.Version)}, which returned the age file unchanged.",
            Transcript(run, opened.Length));
    }

    // gpg --version prints "gpg (GnuPG) 2.2.40"; anything else is reported verbatim
    // rather than reformatted, because a surprise here is worth seeing.
    static string Describe(string version) =>
        string.IsNullOrWhiteSpace(version) ? "the system gpg" : version.Trim();

    static IReadOnlyList<TranscriptLine> Transcript(GpgRunDto run, int openedLength)
    {
        var lines = new List<TranscriptLine>
        {
            new(TranscriptLineKind.Note,
                "Checking the outer OpenPGP lock with the system's own GnuPG, not with the "
                + "library that made it. A library asked to open its own output would agree "
                + "with itself whatever it had written."),
            new(TranscriptLineKind.Command, "gpg --version"),
            new(TranscriptLineKind.Output, Describe(run.Version)),
            new(TranscriptLineKind.Command,
                "gpg --batch --decrypt --passphrase-fd 0 --pinentry-mode loopback payload.age.gpg.asc"),
            new(TranscriptLineKind.Note,
                "The key goes in on standard input, not on the command line, where every other "
                + "program on the machine could read it from the process list."),
            new(TranscriptLineKind.Output,
                $"exit code 0, {openedLength} bytes returned, identical to the age file that was wrapped"),
        };

        // gpg is chatty on stderr even when it succeeds ("gpg: AES256.CFB encrypted data").
        // Shown because it names the cipher that was actually used, which is worth reading.
        if (!string.IsNullOrWhiteSpace(run.StderrText))
            lines.Add(new(TranscriptLineKind.Output, run.StderrText.Trim()));

        return lines;
    }
}
