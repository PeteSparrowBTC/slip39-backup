using System.Diagnostics;
using FluentAssertions;
using Slip39Demo.Core.Age;
using Xunit;

namespace Slip39Demo.Tests.Age;

// Cross-tool interop with the Go reference `age` CLI (filippo.io/age).
// These tests are SKIPPED if the `age` binary is not on PATH so local dev
// machines without age installed don't fail. CI installs age explicitly
// (see Task 13) so these always run there.
public class AgeGoCliInteropTests
{
    static readonly string? AgePath = FindBinary("age");

    static string? FindBinary(string name)
    {
        var ext = OperatingSystem.IsWindows() ? ".exe" : "";
        var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator);
        return pathDirs
            .Select(d => Path.Combine(d, name + ext))
            .FirstOrDefault(File.Exists);
    }

    [SkippableFact]
    public void OurCiphertext_DecryptsUnderGoAgeCli()
    {
        Skip.If(AgePath is null, "age CLI not on PATH");

        var plaintext = "round-trip via Go age\n"u8.ToArray();
        var k = Enumerable.Range(0, 32).Select(i => (byte)(i * 11 & 0xff)).ToArray();
        var passphrase = Convert.ToHexString(k).ToLowerInvariant();

        var ciphertext = AgePassphrase.Encrypt(plaintext, k).Value;
        var ctPath = Path.Combine(Path.GetTempPath(), $"interop-{Guid.NewGuid():N}.age");
        var ptPath = ctPath + ".out";
        File.WriteAllBytes(ctPath, ciphertext);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = AgePath!,
                Arguments = $"-d -o \"{ptPath}\" \"{ctPath}\"",
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var proc = Process.Start(psi)!;
            proc.StandardInput.Write(passphrase);
            proc.StandardInput.Close();
            proc.WaitForExit(15_000).Should().BeTrue("age should finish within 15s");
            proc.ExitCode.Should().Be(0, proc.StandardError.ReadToEnd());

            File.ReadAllBytes(ptPath).Should().Equal(plaintext);
        }
        finally
        {
            File.Delete(ctPath);
            if (File.Exists(ptPath)) File.Delete(ptPath);
        }
    }

    [SkippableFact]
    public void GoAgeCliCiphertext_DecryptsUnderOurCode()
    {
        Skip.If(AgePath is null, "age CLI not on PATH");

        var plaintext = "produced by go age\n"u8.ToArray();
        var k = Enumerable.Range(0, 32).Select(i => (byte)(i * 13 & 0xff)).ToArray();
        var passphrase = Convert.ToHexString(k).ToLowerInvariant();

        var ptPath = Path.Combine(Path.GetTempPath(), $"interop-{Guid.NewGuid():N}.txt");
        var ctPath = ptPath + ".age";
        File.WriteAllBytes(ptPath, plaintext);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = AgePath!,
                Arguments = $"-p -o \"{ctPath}\" \"{ptPath}\"",
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var proc = Process.Start(psi)!;
            // age -p reads passphrase twice (entry + confirmation) when stdin
            // isn't a tty; feed the passphrase + newline twice.
            proc.StandardInput.Write(passphrase + "\n" + passphrase + "\n");
            proc.StandardInput.Close();
            proc.WaitForExit(15_000).Should().BeTrue();
            proc.ExitCode.Should().Be(0, proc.StandardError.ReadToEnd());

            var ct = File.ReadAllBytes(ctPath);
            var dec = AgePassphrase.Decrypt(ct, k);
            dec.IsSuccess.Should().BeTrue(dec.IsFailure ? dec.Error : "");
            dec.Value.Should().Equal(plaintext);
        }
        finally
        {
            if (File.Exists(ptPath)) File.Delete(ptPath);
            if (File.Exists(ctPath)) File.Delete(ctPath);
        }
    }
}
