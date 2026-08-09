using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Slip39Demo.Core.Pgp;
using Xunit;

namespace Slip39Demo.Tests.Pgp;

// The outer envelope has two implementations, and the only claim that matters is
// that they agree: whatever GnuPG writes, this tool must read on a machine that
// has no GnuPG, and whatever this tool writes, GnuPG must read.
//
// The first direction is the one recovery depends on. An heir may be handed a
// payload.age.gpg produced by anything, on a machine where installing GnuPG is
// not an option, and the tool has to open it in-process.
//
// The second direction is what keeps the artifact standard. If our envelope were
// subtly non-conformant, the owner's own verification with the gpg command line
// would fail, and the whole two-command recovery story in MANUAL-RECOVERY.txt
// would be fiction.
//
// gpg-dependent cases skip rather than fail when the binary is absent, so a
// contributor without GnuPG sees a partial run instead of a red suite. The pure
// C# round trip always runs.
public class PgpEnvelopeInteropTests
{
    static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)(i * 3 + 1)).ToArray();
    static string HexKey => Convert.ToHexString(Key).ToLowerInvariant();

    // Stands in for a real payload.age: the age magic followed by binary noise,
    // so the envelope is carrying the byte shapes it will carry in production
    // rather than tidy ASCII.
    static readonly byte[] AgeFile =
        Encoding.ASCII.GetBytes("age-encryption.org/v1\n-> scrypt abc 18\n--- mac\n")
            .Concat(Enumerable.Range(0, 300).Select(i => (byte)(i * 37 % 256)))
            .ToArray();

    [Fact]
    public void CSharp_RoundTripsItsOwnEnvelope()
    {
        var wrapped = PgpEnvelope.Encrypt(AgeFile, Key);
        wrapped.IsSuccess.Should().BeTrue(wrapped.IsFailure ? wrapped.Error : "");
        wrapped.Value.Should().NotEqual(AgeFile);

        var unwrapped = PgpEnvelope.Decrypt(wrapped.Value, Key);
        unwrapped.IsSuccess.Should().BeTrue(unwrapped.IsFailure ? unwrapped.Error : "");
        unwrapped.Value.Should().Equal(AgeFile);
    }

    [Fact]
    public void WrongKey_FailsRatherThanReturningRubbish()
    {
        var wrapped = PgpEnvelope.Encrypt(AgeFile, Key).Value;

        PgpEnvelope.Decrypt(wrapped, new byte[32]).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void TamperedEnvelope_IsRejected()
    {
        var wrapped = PgpEnvelope.Encrypt(AgeFile, Key).Value;
        wrapped[^5] ^= 0xFF; // flip bits well inside the ciphertext body

        var result = PgpEnvelope.Decrypt(wrapped, Key);

        result.IsFailure.Should().BeTrue("an altered envelope must not decrypt silently");
    }

    // The direction recovery depends on: GnuPG wrote it, we read it, with no
    // GnuPG involved in the reading.
    [SkippableFact]
    public void GnuPgWritesIt_ThisToolReadsIt()
    {
        Skip.IfNot(GpgAvailable, "gpg is not on PATH");

        var dir = NewTempDir();
        try
        {
            var input = Path.Combine(dir, "payload.age");
            var output = Path.Combine(dir, "payload.age.gpg");
            File.WriteAllBytes(input, AgeFile);

            RunGpg(["--batch", "--yes", "--symmetric", "--cipher-algo", "AES256",
                    "--passphrase-fd", "0", "--pinentry-mode", "loopback",
                    "--output", output, input])
                .Should().Be(0);

            var unwrapped = PgpEnvelope.Decrypt(File.ReadAllBytes(output), Key);

            unwrapped.IsSuccess.Should().BeTrue(unwrapped.IsFailure ? unwrapped.Error : "");
            unwrapped.Value.Should().Equal(AgeFile,
                "a payload.age.gpg made by GnuPG must open in-process, because the heir's machine may not have GnuPG");
        }
        finally { Directory.Delete(dir, true); }
    }

    // The direction that keeps MANUAL-RECOVERY.txt honest: our envelope has to be
    // openable by the gpg command line the document tells people to use.
    [SkippableFact]
    public void ThisToolWritesIt_GnuPgReadsIt()
    {
        Skip.IfNot(GpgAvailable, "gpg is not on PATH");

        var dir = NewTempDir();
        try
        {
            var wrapped = PgpEnvelope.Encrypt(AgeFile, Key);
            wrapped.IsSuccess.Should().BeTrue(wrapped.IsFailure ? wrapped.Error : "");

            var input = Path.Combine(dir, "payload.age.gpg");
            var output = Path.Combine(dir, "payload.age");
            File.WriteAllBytes(input, wrapped.Value);

            RunGpg(["--batch", "--yes", "--decrypt", "--passphrase-fd", "0",
                    "--pinentry-mode", "loopback", "--output", output, input])
                .Should().Be(0, "the exact command MANUAL-RECOVERY.txt gives the heir must work");

            File.ReadAllBytes(output).Should().Equal(AgeFile);
        }
        finally { Directory.Delete(dir, true); }
    }

    static bool GpgAvailable => RunGpg(["--version"], captureOnly: true) == 0;

    static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "slip39-pgp-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    // The passphrase goes in on stdin (--passphrase-fd 0), never on the command
    // line where the process list would expose it. The file being handed over is
    // already-encrypted age output, so a temporary file for it leaks nothing.
    static int RunGpg(string[] args, bool captureOnly = false)
    {
        var psi = new ProcessStartInfo("gpg")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
                return -1;

            if (!captureOnly)
                process.StandardInput.Write(HexKey);
            process.StandardInput.Close();

            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit(60_000);
            return process.ExitCode;
        }
        catch
        {
            return -1;
        }
    }
}
