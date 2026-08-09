using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using CSharpFunctionalExtensions;
using Slip39Demo.UI.Services;

namespace Slip39Demo.Desktop.Services;

// Encrypts the payload by running the official age binary bundled in the
// AppImage, rather than the AgeSharp library compiled into this app.
//
// WHY
// A bug in an encryptor is invisible: a file written with a reused nonce or a
// weak key decrypts perfectly and stays weak forever, so no amount of
// round-trip testing finds it. A bug in a decryptor is loud. This artifact is
// the one people run against real seed phrases, so the side where mistakes
// cannot be seen gets the reference implementation, written by the author of
// the age format and far more scrutinised than any C# port.
//
// AgeSharp still handles decryption in Recoverer mode, where a fault announces
// itself, and it still passes the CCTV wire-format vectors in the test suite.
//
// HOW THE KEY IS PASSED
// Not on the command line, which would put it in the process list for every
// other process on the machine to read. It goes in AGE_PASSPHRASE, which
// age-plugin-batchpass reads. That is visible in /proc/<pid>/environ to
// processes running as the same user for the second or so age lives.
//
// The stronger option, AGE_PASSPHRASE_FD, needs the passphrase on an inherited
// file descriptor, and .NET's Process has no API for inheriting one, so it would
// mean wrapping the whole thing in `sh -c` with a FIFO. That trade was rejected:
// it hides the command from the transcript the user is meant to read, to close a
// gap that is already open, since the key sits in this process's own memory,
// readable by anything running as the same user. On an amnesic single-user
// airgapped system this is the right balance, and the transcript says so out
// loud rather than quietly picking for the user.
//
// FAIL CLOSED
// If the binary is missing, refuses to run, or returns anything unexpected,
// generation fails. There is deliberately no fallback to the in-process library:
// falling back to the thing this class exists to avoid would make it decorative.
// ageDirectory overrides where the binaries are looked up. Production leaves it
// null and uses the AppImage layout; tests point it at a downloaded release so
// the subprocess plumbing is exercised for real rather than mocked.
public sealed class NativeAgeEncryptor(string? ageDirectory = null) : IPayloadEncryptor
{
    // Where build-appimage.sh puts the official binaries, relative to the app.
    const string AgeSubdirectory = "age";
    const string AgeBinary = "age";
    const string PluginBinary = "age-plugin-batchpass";

    static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    public async Task<Result<EncryptionOutcome>> EncryptAsync(byte[] plaintext, byte[] key32)
    {
        if (key32.Length != 32)
            return Result.Failure<EncryptionOutcome>($"key must be 32 bytes (got {key32.Length})");

        var dir = ageDirectory ?? Path.Combine(AppContext.BaseDirectory, AgeSubdirectory);
        var age = Path.Combine(dir, OperatingSystem.IsWindows() ? AgeBinary + ".exe" : AgeBinary);
        var plugin = Path.Combine(dir, OperatingSystem.IsWindows() ? PluginBinary + ".exe" : PluginBinary);

        if (!File.Exists(age))
            return Result.Failure<EncryptionOutcome>(
                $"the bundled age program is missing (expected at {age}). This build refuses to fall "
                + "back to encrypting in-process, because the whole point of running age is that a "
                + "mistake made while encrypting cannot be detected afterwards.");
        if (!File.Exists(plugin))
            return Result.Failure<EncryptionOutcome>(
                $"the bundled age-plugin-batchpass program is missing (expected at {plugin}). age "
                + "needs it to accept a passphrase without a terminal prompt.");

        var lines = new List<TranscriptLine>();

        // Identify the exact binary being trusted, by hash and by its own version
        // string. Both are shown so the reader can compare the hash against the
        // one they downloaded themselves from the age release page.
        var sha = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(age)));
        lines.Add(new(TranscriptLineKind.Note,
            "Encrypting with the official age program, not with code built into this app."));
        lines.Add(new(TranscriptLineKind.Note, $"Program:  {age}"));
        lines.Add(new(TranscriptLineKind.Note, $"SHA-256:  {sha}"));

        var version = await RunAsync(age, ["--version"], dir, null, null);
        if (version.IsFailure)
            return Result.Failure<EncryptionOutcome>($"the bundled age program would not run: {version.Error}");
        lines.Add(new(TranscriptLineKind.Command, $"{AgeBinary} --version"));
        lines.Add(new(TranscriptLineKind.Output, version.Value.StdOutText.Trim()));

        // The encryption itself. Plaintext goes in on stdin and ciphertext comes
        // back on stdout, so neither ever touches the filesystem.
        var hexKey = Convert.ToHexString(key32).ToLowerInvariant();
        lines.Add(new(TranscriptLineKind.Command,
            $"printf '%s' \"<the wallet payload>\" | {AgeBinary} --encrypt -j batchpass"));
        lines.Add(new(TranscriptLineKind.Note,
            "The payload goes in through a pipe and the encrypted file comes back through a pipe, "
            + "so the unencrypted wallet is never written to disk."));
        lines.Add(new(TranscriptLineKind.Note,
            "The key is given to age in the environment variable AGE_PASSPHRASE, which "
            + "age-plugin-batchpass reads. It is deliberately NOT on the command line, where every "
            + "other program on the machine could read it from the process list."));

        var run = await RunAsync(age, ["--encrypt", "-j", "batchpass"], dir, plaintext, hexKey);
        if (run.IsFailure)
            return Result.Failure<EncryptionOutcome>($"age failed to encrypt the payload: {run.Error}");

        var result = run.Value;
        if (result.ExitCode != 0)
            return Result.Failure<EncryptionOutcome>(
                $"age exited with code {result.ExitCode}: {result.StdErrText.Trim()}");
        if (result.StdOut.Length == 0)
            return Result.Failure<EncryptionOutcome>("age produced no output");

        // A last sanity check on the shape of what came back. Not a substitute for
        // the independent verification that follows, just a guard against handing
        // on something that is obviously not an age file.
        var magic = Encoding.ASCII.GetString(result.StdOut, 0, Math.Min(result.StdOut.Length, 21));
        if (magic != "age-encryption.org/v1")
            return Result.Failure<EncryptionOutcome>(
                $"age returned something that does not start with the age v1 header (saw \"{magic}\")");

        lines.Add(new(TranscriptLineKind.Output,
            $"exit code 0, {result.StdOut.Length} bytes produced, header reads \"age-encryption.org/v1\""));
        if (!string.IsNullOrWhiteSpace(result.StdErrText))
            lines.Add(new(TranscriptLineKind.Output, result.StdErrText.Trim()));
        lines.Add(new(TranscriptLineKind.Note,
            $"Payload in: {plaintext.Length} bytes. Encrypted file out: {result.StdOut.Length} bytes."));
        lines.Add(new(TranscriptLineKind.Warning,
            "While age was running (about a second), the key was readable from this machine's "
            + "process information by anything running as the same user. On an offline Tails "
            + "session that nobody else is using, that is nothing; on a shared computer it would "
            + "matter, which is one more reason to do this offline."));

        var transcript = new EncryptionTranscript(
            $"Encrypted by the official age program ({version.Value.StdOutText.Trim()}), run as a separate "
            + "program, not by code built into this app.",
            lines);

        return Result.Success(new EncryptionOutcome(result.StdOut, transcript));
    }

    sealed record RunResult(int ExitCode, byte[] StdOut, string StdOutText, string StdErrText);

    // Runs a bundled binary with stdin/stdout as raw bytes. stdin is written and
    // closed before stdout is drained; the payloads here are well under a
    // kilobyte, so there is no risk of the deadlock that ordering would cause
    // with large data.
    static async Task<Result<RunResult>> RunAsync(
        string exe, string[] args, string workingDirectory, byte[]? stdin, string? passphrase)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            // age resolves plugins from PATH. Point it at the bundled directory
            // only, so it cannot pick up some other age-plugin-batchpass that
            // happens to be on the machine.
            WorkingDirectory = workingDirectory,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        psi.Environment["PATH"] = workingDirectory;
        if (passphrase is not null)
            psi.Environment["AGE_PASSPHRASE"] = passphrase;

        try
        {
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("process did not start");

            using var stdoutBuffer = new MemoryStream();
            var stderrTask = process.StandardError.ReadToEndAsync();
            var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(stdoutBuffer);

            if (stdin is not null)
                await process.StandardInput.BaseStream.WriteAsync(stdin);
            process.StandardInput.Close();

            using var cts = new CancellationTokenSource(Timeout);
            await process.WaitForExitAsync(cts.Token);
            await stdoutTask;
            var stderr = await stderrTask;

            var bytes = stdoutBuffer.ToArray();
            return new RunResult(process.ExitCode, bytes, Encoding.UTF8.GetString(bytes), stderr);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure<RunResult>($"age did not finish within {Timeout.TotalSeconds:0} seconds");
        }
        catch (Exception ex)
        {
            return Result.Failure<RunResult>($"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
