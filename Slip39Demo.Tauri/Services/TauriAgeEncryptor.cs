using System.Text;
using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using Slip39Demo.UI.Services;

namespace Slip39Demo.Tauri.Services;

// What src-tauri/src/age.rs reports back. It is a record of what happened, not a
// verdict on it: every judgement below is made here.
//
// A record with init-only properties rather than settable ones: nothing should be able
// to edit a report of what already happened. The names match the camelCase that
// AgeRun's serde(rename_all) produces, and AgeRunDtoTests pins that agreement, because
// nothing else in the suite deserializes real shell output.
public sealed record AgeRunDto
{
    [JsonPropertyName("exitCode")] public int ExitCode { get; init; }
    [JsonPropertyName("stdoutB64")] public string StdoutB64 { get; init; } = "";
    [JsonPropertyName("stdoutText")] public string StdoutText { get; init; } = "";
    [JsonPropertyName("stderrText")] public string StderrText { get; init; } = "";
    [JsonPropertyName("agePath")] public string AgePath { get; init; } = "";
    [JsonPropertyName("ageSha256")] public string AgeSha256 { get; init; } = "";
    [JsonPropertyName("pluginPath")] public string PluginPath { get; init; } = "";
    [JsonPropertyName("ageMissing")] public bool AgeMissing { get; init; }
    [JsonPropertyName("pluginMissing")] public bool PluginMissing { get; init; }
}

// Encrypts the payload by running the official age binary bundled in the AppImage,
// rather than the AgeSharp library.
//
// WHY
// A bug in an encryptor is invisible: a file written with a reused nonce or a weak key
// decrypts perfectly and stays weak forever, so no amount of round-trip testing finds
// it. A bug in a decryptor is loud. This artifact is the one people run against real
// seed phrases, so the side where mistakes cannot be seen gets the reference
// implementation, written by the author of the age format and far more scrutinised
// than any C# port.
//
// AgeSharp still handles decryption in Recoverer mode, where a fault announces itself,
// and it still passes the CCTV wire-format vectors in the test suite.
//
// FAIL CLOSED
// If the binary is missing, refuses to run, or returns anything unexpected,
// generation fails. There is deliberately no fallback to the in-process library:
// falling back to the thing this class exists to avoid would make it decorative.
public sealed class TauriAgeEncryptor(ITauriInterop interop) : IPayloadEncryptor
{
    public async Task<Result<EncryptionOutcome>> EncryptAsync(byte[] plaintext, byte[] key32)
    {
        if (key32.Length != 32)
            return Result.Failure<EncryptionOutcome>($"key must be 32 bytes (got {key32.Length})");

        AgeRunDto run;
        try
        {
            run = await interop.InvokeAsync<AgeRunDto>("age_encrypt", new
            {
                plaintextB64 = Convert.ToBase64String(plaintext),
                passphraseHex = Convert.ToHexString(key32).ToLowerInvariant(),
            });
        }
        catch (Exception ex)
        {
            return Result.Failure<EncryptionOutcome>($"the shell could not run age: {ex.Message}");
        }

        if (run.AgeMissing)
            return Result.Failure<EncryptionOutcome>(
                $"the bundled age program is missing (expected at {run.AgePath}). This build refuses to fall "
                + "back to encrypting in-process, because the whole point of running age is that a "
                + "mistake made while encrypting cannot be detected afterwards.");
        if (run.PluginMissing)
            return Result.Failure<EncryptionOutcome>(
                $"the bundled age-plugin-batchpass program is missing (expected at {run.PluginPath}). age "
                + "needs it to accept a passphrase without a terminal prompt.");

        if (run.ExitCode != 0)
            return Result.Failure<EncryptionOutcome>(
                $"age exited with code {run.ExitCode}: {run.StderrText.Trim()}");

        var ciphertext = Convert.FromBase64String(run.StdoutB64);
        if (ciphertext.Length == 0)
            return Result.Failure<EncryptionOutcome>("age produced no output");

        // A last sanity check on the shape of what came back. Not a substitute for the
        // independent verification that follows, just a guard against handing on
        // something that is obviously not an age file.
        var magic = Encoding.ASCII.GetString(ciphertext, 0, Math.Min(ciphertext.Length, 21));
        if (magic != "age-encryption.org/v1")
            return Result.Failure<EncryptionOutcome>(
                $"age returned something that does not start with the age v1 header (saw \"{magic}\")");

        var version = run.StdoutText.Trim();
        var lines = new List<TranscriptLine>
        {
            new(TranscriptLineKind.Note,
                "Encrypting with the official age program, not with code built into this app."),
            new(TranscriptLineKind.Note, $"Program:  {run.AgePath}"),
            new(TranscriptLineKind.Note, $"SHA-256:  {run.AgeSha256}"),
            new(TranscriptLineKind.Command, "age --version"),
            new(TranscriptLineKind.Output, version),
            new(TranscriptLineKind.Command,
                "printf '%s' \"<the wallet payload>\" | age --encrypt -j batchpass"),
            new(TranscriptLineKind.Note,
                "The payload goes in through a pipe and the encrypted file comes back through a pipe, "
                + "so the unencrypted wallet is never written to disk."),
            new(TranscriptLineKind.Note,
                "The key is given to age in the environment variable AGE_PASSPHRASE, which "
                + "age-plugin-batchpass reads. It is deliberately NOT on the command line, where every "
                + "other program on the machine could read it from the process list."),
            new(TranscriptLineKind.Output,
                $"exit code 0, {ciphertext.Length} bytes produced, header reads \"age-encryption.org/v1\""),
        };

        if (!string.IsNullOrWhiteSpace(run.StderrText))
            lines.Add(new(TranscriptLineKind.Output, run.StderrText.Trim()));

        lines.Add(new(TranscriptLineKind.Note,
            $"Payload in: {plaintext.Length} bytes. Encrypted file out: {ciphertext.Length} bytes."));
        lines.Add(new(TranscriptLineKind.Warning,
            "While age was running (about a second), the key was readable from this machine's "
            + "process information by anything running as the same user. On an offline Tails "
            + "session that nobody else is using, that is nothing; on a shared computer it would "
            + "matter, which is one more reason to do this offline."));

        var transcript = new EncryptionTranscript(
            $"Encrypted by the official age program ({version}), run as a separate "
            + "program, not by code built into this app.",
            lines);

        return Result.Success(new EncryptionOutcome(ciphertext, transcript));
    }
}
