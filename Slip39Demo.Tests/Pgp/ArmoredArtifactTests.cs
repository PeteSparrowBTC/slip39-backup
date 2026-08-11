using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Slip39Demo.Core.Age;
using Slip39Demo.Core.Payload;
using Slip39Demo.Core.Pgp;
using Xunit;

namespace Slip39Demo.Tests.Pgp;

// The armored envelope is the ONLY ciphertext the bundle ships, so its shape is not a
// presentation detail. It is the text a person pastes into a password manager, prints,
// and may retype by hand years later.
//
// What each assertion defends:
//   fences and CRC   the CRC24 is what detects a mangled paste, and it is a capability
//                    gained by shipping PGP armor rather than age armor, which has no
//                    checksum. Losing it silently would remove the only transcription
//                    check this artifact has
//   the Comment      somebody who finds this text alone, with no bundle around it, has
//                    to know what it is and what to do. Armor headers are ignored by
//                    readers that do not care, so it costs nothing
//   no Version       fingerprints the producing library for no reader benefit
//   real gpg         the documented recovery command is `gpg -d`. If GnuPG cannot read
//                    what we write, MANUAL-RECOVERY.txt is fiction
public class ArmoredArtifactTests
{
    static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)(i + 9)).ToArray();
    static readonly byte[] Payload = Encoding.UTF8.GetBytes("seed_words: abandon abandon about\n");

    static string Armored()
    {
        var age = AgePassphrase.Encrypt(Payload, Key).Value;
        var armored = PgpEnvelope.EncryptArmored(age, Key);
        armored.IsSuccess.Should().BeTrue(armored.IsFailure ? armored.Error : "");
        return armored.Value;
    }

    [Fact]
    public void It_is_text_with_fences_and_a_crc_and_no_version_header()
    {
        var text = Armored();

        text.Should().StartWith("-----BEGIN PGP MESSAGE-----");
        text.TrimEnd().Should().EndWith("-----END PGP MESSAGE-----");
        text.Should().NotContain("Version:", "the producing library is nobody's business");

        // "=" then four base64 characters on its own line, immediately before the
        // closing fence. Without it, a mangled paste goes undetected.
        var lines = text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        var closing = Array.FindIndex(lines, l => l.StartsWith("-----END", StringComparison.Ordinal));
        closing.Should().BeGreaterThan(0);
        lines[closing - 1].Should().MatchRegex("^=[A-Za-z0-9+/]{4}$", "the CRC24 line must be present");
    }

    [Fact]
    public void It_says_what_it_is_and_how_to_recover_it()
    {
        var text = Armored();

        text.Should().Contain("Comment:");
        text.Should().Contain("gpg -d");
        text.Should().Contain("age -d");
        text.Should().Contain("MANUAL-RECOVERY.txt");
    }

    [Fact]
    public void It_is_pure_ascii_so_it_survives_any_text_channel() =>
        Armored().Where(c => c > 127).Should().BeEmpty(
            "a password manager, an email body and a printed page all mangle non-ASCII");

    [Fact]
    public void Recoverer_classifies_it_as_an_openpgp_envelope()
    {
        var shape = CiphertextShapeDetector.Detect(Encoding.UTF8.GetBytes(Armored()));

        shape.Should().Be(CiphertextShape.OpenPgpArmored);
        shape.IsOpenPgp().Should().BeTrue();
    }

    [Fact]
    public void It_round_trips_through_our_own_stack_to_the_payload()
    {
        var unwrapped = PgpEnvelope.Decrypt(Encoding.UTF8.GetBytes(Armored()), Key);
        unwrapped.IsSuccess.Should().BeTrue(unwrapped.IsFailure ? unwrapped.Error : "");

        var plain = AgePassphrase.Decrypt(unwrapped.Value, Key);
        plain.IsSuccess.Should().BeTrue(plain.IsFailure ? plain.Error : "");
        plain.Value.Should().Equal(Payload);
    }

    // The documented recovery command, run for real. The Comment header is the part
    // most likely to trip a strict parser, so this is the test that says whether adding
    // it was safe.
    [SkippableFact]
    public void Real_gpg_reads_it_including_the_comment_header()
    {
        Skip.IfNot(GpgAvailable, "gpg is not on PATH");

        var dir = Path.Combine(Path.GetTempPath(), "slip39-asc-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var input = Path.Combine(dir, "payload.age.gpg.asc");
            var output = Path.Combine(dir, "payload.age");
            File.WriteAllText(input, Armored());

            RunGpg(["--batch", "--yes", "--decrypt", "--passphrase-fd", "0",
                    "--pinentry-mode", "loopback", "--output", output, input])
                .Should().Be(0, "MANUAL-RECOVERY.txt tells the heir to run exactly this");

            // And the inner lock still opens, so the whole documented chain works.
            var plain = AgePassphrase.Decrypt(File.ReadAllBytes(output), Key);
            plain.IsSuccess.Should().BeTrue(plain.IsFailure ? plain.Error : "");
            plain.Value.Should().Equal(Payload);
        }
        finally { Directory.Delete(dir, true); }
    }

    static bool GpgAvailable => RunGpg(["--version"], sendPassphrase: false) == 0;

    static int RunGpg(string[] args, bool sendPassphrase = true)
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

            if (sendPassphrase)
                process.StandardInput.Write(Convert.ToHexString(Key).ToLowerInvariant());
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
