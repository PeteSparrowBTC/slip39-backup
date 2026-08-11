using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Slip39Demo.Core.Age;
using Slip39Demo.Core.Payload;
using Slip39Demo.Core.Pgp;
using Xunit;

namespace Slip39Demo.Tests.Payload;

// Recovery has to recognise what it was handed. Four shapes exist, an age file or an
// OpenPGP envelope, each binary or ASCII armored, and getting it wrong does not fail
// loudly: the bytes go to the wrong decryptor, which reports corruption or a key
// mismatch, and an heir reads that as a broken backup rather than an unrecognised
// format.
//
// The armored OpenPGP case is the regression these tests exist for. Recoverer used to
// test only the high bit of the first byte, which catches binary OpenPGP and misses
// armor because armor is printable ASCII. Armor is exactly what a password-manager
// note holds, so the likeliest payload an heir pastes was the one that could not be
// recovered.
public class CiphertextShapeTests
{
    static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)(i + 5)).ToArray();
    static readonly byte[] Payload = Encoding.UTF8.GetBytes("schema_version: 1.1\nseed_words: abandon about\n");

    [Fact]
    public void An_age_file_is_recognised_as_age_binary()
    {
        var age = AgePassphrase.Encrypt(Payload, Key).Value;

        CiphertextShapeDetector.Detect(age).Should().Be(CiphertextShape.AgeBinary);
    }

    [Fact]
    public void Age_armor_is_recognised_and_needs_dearmoring()
    {
        var armored = Encoding.UTF8.GetBytes(AgeArmor.Encode(AgePassphrase.Encrypt(Payload, Key).Value));

        var shape = CiphertextShapeDetector.Detect(armored);

        shape.Should().Be(CiphertextShape.AgeArmored);
        shape.IsAgeArmor().Should().BeTrue();
        shape.IsOpenPgp().Should().BeFalse();
    }

    [Fact]
    public void A_binary_openpgp_envelope_is_recognised()
    {
        var wrapped = PgpEnvelope.Encrypt(AgePassphrase.Encrypt(Payload, Key).Value, Key).Value;

        var shape = CiphertextShapeDetector.Detect(wrapped);

        shape.Should().Be(CiphertextShape.OpenPgpBinary);
        shape.IsOpenPgp().Should().BeTrue();
    }

    // The regression. Produced by real GnuPG rather than by us, because the point is
    // to read what other tools write.
    [SkippableFact]
    public void Gpg_armored_output_is_recognised_as_openpgp_not_age()
    {
        Skip.IfNot(GpgAvailable, "gpg is not on PATH");

        var armored = GpgArmor(AgePassphrase.Encrypt(Payload, Key).Value);

        var shape = CiphertextShapeDetector.Detect(armored);

        shape.Should().Be(CiphertextShape.OpenPgpArmored,
            "a pasted password-manager note holds this form, and the old high-bit check missed it");
        shape.IsOpenPgp().Should().BeTrue();
    }

    // The whole point of recognising it: the full chain has to come back out.
    [SkippableFact]
    public void Gpg_armored_output_decrypts_all_the_way_to_the_payload()
    {
        Skip.IfNot(GpgAvailable, "gpg is not on PATH");

        var armored = GpgArmor(AgePassphrase.Encrypt(Payload, Key).Value);

        var unwrapped = PgpEnvelope.Decrypt(armored, Key);
        unwrapped.IsSuccess.Should().BeTrue(unwrapped.IsFailure ? unwrapped.Error : "");

        var plain = AgePassphrase.Decrypt(unwrapped.Value, Key);
        plain.IsSuccess.Should().BeTrue(plain.IsFailure ? plain.Error : "");
        plain.Value.Should().Equal(Payload);
    }

    // Our own armored output must round-trip too, so the artifact we ship as text is
    // readable by the code that reads it.
    [Fact]
    public void Our_own_envelope_decrypts_whether_binary_or_armored()
    {
        var age = AgePassphrase.Encrypt(Payload, Key).Value;
        var binary = PgpEnvelope.Encrypt(age, Key).Value;

        PgpEnvelope.Decrypt(binary, Key).Value.Should().Equal(age);
    }

    // Leading text above the fence survives a paste out of a note, and must not stop
    // the fence being found.
    [Fact]
    public void Armor_preceded_by_notes_is_still_recognised()
    {
        var wrapped = PgpEnvelope.Encrypt(AgePassphrase.Encrypt(Payload, Key).Value, Key).Value;
        var armored = "Wallet backup, do not delete\n\n" + ToPgpArmorText(wrapped);

        CiphertextShapeDetector.Detect(Encoding.UTF8.GetBytes(armored))
            .Should().Be(CiphertextShape.OpenPgpArmored);
    }

    [Fact]
    public void Unrecognised_bytes_fall_to_the_age_path_rather_than_guessing()
    {
        CiphertextShapeDetector.Detect("not a backup at all"u8.ToArray())
            .Should().Be(CiphertextShape.AgeBinary);
        CiphertextShapeDetector.Detect([]).Should().Be(CiphertextShape.AgeBinary);
    }

    // Wraps binary OpenPGP in the armor fences, without a CRC line, purely to test
    // detection. Real armor carries "=CRC" and gpg produces it; this helper is not a
    // substitute for the gpg-produced cases above.
    static string ToPgpArmorText(byte[] binary) =>
        "-----BEGIN PGP MESSAGE-----\n\n"
        + Convert.ToBase64String(binary) + "\n"
        + "-----END PGP MESSAGE-----\n";

    static bool GpgAvailable => RunGpg(["--version"], null) == 0;

    static byte[] GpgArmor(byte[] input)
    {
        var dir = Path.Combine(Path.GetTempPath(), "slip39-armor-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var inPath = Path.Combine(dir, "payload.age");
            var outPath = Path.Combine(dir, "payload.age.gpg.txt");
            File.WriteAllBytes(inPath, input);

            RunGpg(["--batch", "--yes", "--armor", "--symmetric", "--cipher-algo", "AES256",
                    "--passphrase-fd", "0", "--pinentry-mode", "loopback",
                    "--output", outPath, inPath],
                   Convert.ToHexString(Key).ToLowerInvariant())
                .Should().Be(0);

            return File.ReadAllBytes(outPath);
        }
        finally { Directory.Delete(dir, true); }
    }

    static int RunGpg(string[] args, string? passphrase)
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

            if (passphrase is not null)
                process.StandardInput.Write(passphrase);
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
