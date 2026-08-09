using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Slip39Demo.Core.Age;
using Slip39Demo.Core.Payload;
using Slip39Demo.Core.Pgp;
using Slip39Demo.Core.Slip39;
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

    // GnuPG compresses before encrypting by DEFAULT, and lets the user pick the
    // algorithm, so a real payload.age.gpg is very likely to have a compressed
    // packet inside it that we never produce ourselves. If the decrypt path could
    // not unwrap those, recovery would fail on precisely the files most heirs
    // will be handed. Every algorithm gpg can emit is covered here rather than
    // trusting that the default is the only one that turns up.
    [SkippableTheory]
    [InlineData("none")]
    [InlineData("zip")]
    [InlineData("zlib")]
    [InlineData("bzip2")]
    public void GnuPgCompressedEnvelopes_AreUnwrapped(string compressAlgorithm)
    {
        Skip.IfNot(GpgAvailable, "gpg is not on PATH");

        var dir = NewTempDir();
        try
        {
            var input = Path.Combine(dir, "payload.age");
            var output = Path.Combine(dir, "payload.age.gpg");
            File.WriteAllBytes(input, AgeFile);

            RunGpg(["--batch", "--yes", "--symmetric", "--cipher-algo", "AES256",
                    "--compress-algo", compressAlgorithm,
                    "--passphrase-fd", "0", "--pinentry-mode", "loopback",
                    "--output", output, input])
                .Should().Be(0);

            var unwrapped = PgpEnvelope.Decrypt(File.ReadAllBytes(output), Key);

            unwrapped.IsSuccess.Should().BeTrue(unwrapped.IsFailure ? unwrapped.Error : "");
            unwrapped.Value.Should().Equal(AgeFile,
                $"a gpg envelope compressed with {compressAlgorithm} must still yield the exact age file");
        }
        finally { Directory.Delete(dir, true); }
    }

    // One byte separates the two layers, and Recoverer relies on it to decide
    // whether to unwrap before decrypting. OpenPGP packet tags always have the
    // high bit set; age and its armor are plain ASCII.
    [Fact]
    public void OpenPgpAndAgeFiles_AreDistinguishableByTheirFirstByte()
    {
        var envelope = PgpEnvelope.Encrypt(AgeFile, Key).Value;

        (envelope[0] & 0x80).Should().NotBe(0, "an OpenPGP packet tag has the high bit set");
        (AgeFile[0] & 0x80).Should().Be(0, "an age file starts with ASCII 'a'");
        ((byte)'-' & 0x80).Should().Be(0, "age armor starts with ASCII '-'");
    }

    // The whole stack, walked the way Recoverer walks it, starting from nothing
    // but share mnemonics and a doubly-wrapped file. This is the claim that
    // actually matters: shares in, wallet out, with the OpenPGP layer unwrapped
    // and the age layer decrypted in-process, nothing else installed.
    [Fact]
    public void SharesPlusDoubleWrappedFile_RecoverTheWallet()
    {
        var payloadText =
            """
            schema_version: 1.1
            created: 2026-01-01
            label: "Main wallet"

            seed_words: abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about

            cosigners:
              - id: main
                wallet_type: single_sig
                derivation_path: m/84'/0'/0'

            threshold: 3-of-5
            slip39_extendable: true

            """;

        // Owner side: split K, encrypt with age, then wrap the age file in OpenPGP.
        var k = RandomNumberGenerator.GetBytes(32);
        var mnemonics = Slip39Wrapping.SplitKey(k, new GroupConfig(
            GroupThreshold: 1,
            Groups: [new ShareGroup("group", Threshold: 3, Count: 5)],
            Extendable: true));
        mnemonics.IsSuccess.Should().BeTrue(mnemonics.IsFailure ? mnemonics.Error : "");

        var ageFile = AgePassphrase.Encrypt(Encoding.UTF8.GetBytes(payloadText), k);
        ageFile.IsSuccess.Should().BeTrue();
        var doubleWrapped = PgpEnvelope.Encrypt(ageFile.Value, k);
        doubleWrapped.IsSuccess.Should().BeTrue();

        // Recovery side: three of the five shares plus the wrapped file. The key is
        // reconstructed here, never carried across from the owner side.
        var recoveredKey = Slip39Wrapping.CombineMnemonics(mnemonics.Value.Take(3));
        recoveredKey.IsSuccess.Should().BeTrue(recoveredKey.IsFailure ? recoveredKey.Error : "");

        var unwrapped = PgpEnvelope.Decrypt(doubleWrapped.Value, recoveredKey.Value);
        unwrapped.IsSuccess.Should().BeTrue(unwrapped.IsFailure ? unwrapped.Error : "");

        var plain = AgePassphrase.Decrypt(unwrapped.Value, recoveredKey.Value);
        plain.IsSuccess.Should().BeTrue(plain.IsFailure ? plain.Error : "");

        var parsed = PayloadParser.Parse(Encoding.UTF8.GetString(plain.Value));
        parsed.IsSuccess.Should().BeTrue(parsed.IsFailure ? parsed.Error : "");
        parsed.Value.TopLevelSeedWords.Should().StartWith("abandon abandon");
        parsed.Value.Label.Should().Be("Main wallet");
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
