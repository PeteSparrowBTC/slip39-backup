using System.Text;
using System.Text.Json;
using FluentAssertions;
using Slip39Demo.Tauri.Services;
using Slip39Demo.UI.Services;
using Xunit;

namespace Slip39Demo.Tests.Tauri;

// The judgement half of the outer-lock check. src-tauri/src/gpg.rs reports what gpg said
// and decides nothing; everything decided from that report is here, so this is where the
// decisions are pinned.
//
// The Rust side has its own tests against a real GnuPG, and PgpEnvelopeInteropTests +
// ArmoredArtifactTests already prove real gpg opens the artifact this tool ships. What is
// left, and what is easy to get wrong without noticing, is the mapping: which shell
// outcome means "verified", which means "not checked", and which means "refuse". A stub
// covers those exhaustively and without needing gpg installed.
public class TauriPgpVerifierTests
{
    static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
    static readonly byte[] AgeFile = Encoding.ASCII.GetBytes("age-encryption.org/v1\npretend inner file");

    const string Armored = "-----BEGIN PGP MESSAGE-----\n\npretend\n=abcd\n-----END PGP MESSAGE-----\n";

    // The command name the Rust shell registers, and the argument names Tauri maps to its
    // parameters. Asserted rather than accepted: renaming either side would leave every
    // test here green and fail only on a real machine, at the moment a real backup was
    // being generated.
    const string Command = "gpg_decrypt";

    sealed class StubInterop(GpgRunDto result) : ITauriInterop
    {
        public ValueTask<T> InvokeAsync<T>(string command, object? args = null)
        {
            Assert.Equal(Command, command);

            var sent = JsonSerializer.SerializeToElement(args);
            Assert.True(sent.TryGetProperty("armored", out _), "armored must be sent");
            Assert.True(sent.TryGetProperty("passphrase", out var passphrase), "passphrase must be sent");

            // The passphrase convention is the point of the whole check: 64 lowercase hex
            // characters, the same encoding PgpEnvelope uses. If this side sent raw bytes
            // or uppercase, gpg would refuse the passphrase on a real machine and the
            // gate would fail for a reason that has nothing to do with the backup.
            Assert.Equal("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f",
                passphrase.GetString());

            return ValueTask.FromResult((T)(object)result);
        }
    }

    sealed class ThrowingInterop : ITauriInterop
    {
        public ValueTask<T> InvokeAsync<T>(string command, object? args = null) =>
            throw new InvalidOperationException("no shell here");
    }

    static GpgRunDto Opened(byte[] stdout) => new()
    {
        ExitCode = 0,
        StdoutB64 = Convert.ToBase64String(stdout),
        StderrText = "gpg: AES256.CFB encrypted data",
        Version = "gpg (GnuPG) 2.2.40",
        GpgMissing = false,
    };

    static Task<OuterLockVerification> Verify(GpgRunDto run) =>
        new TauriPgpVerifier(new StubInterop(run)).VerifyAsync(Armored, AgeFile, Key);

    [Fact]
    public async Task Gpg_returning_the_age_file_unchanged_is_verified()
    {
        var result = await Verify(Opened(AgeFile));

        result.Outcome.Should().Be(OuterLockOutcome.Verified);
        result.Detail.Should().Contain("2.2.40", "the transcript should name what opened it");
        // The transcript is what the user reads instead of taking our word for it, so it
        // has to contain the command that ran and what came back.
        result.Transcript.Should().Contain(
            l => l.Kind == TranscriptLineKind.Command && l.Text.Contains("--decrypt"));
        result.Transcript.Should().Contain(l => l.Text.Contains("AES256"),
            "gpg names the cipher it actually used, which is worth showing");
    }

    // The interesting failure, and the one no in-process check could ever find: gpg opens
    // the envelope and hands back something else. A wrong length is a stand-in for any
    // packet-level divergence between what BouncyCastle wrote and what gpg read.
    [Fact]
    public async Task Gpg_returning_different_bytes_is_a_failure()
    {
        var result = await Verify(Opened(AgeFile.Concat([(byte)'x']).ToArray()));

        result.Outcome.Should().Be(OuterLockOutcome.Failed);
        result.Detail.Should().Contain("different bytes");
    }

    [Fact]
    public async Task A_nonzero_exit_code_is_a_failure_and_carries_what_gpg_said()
    {
        var result = await Verify(new GpgRunDto
        {
            ExitCode = 2,
            StderrText = "gpg: decryption failed: Bad session key",
            Version = "gpg (GnuPG) 2.2.40",
        });

        result.Outcome.Should().Be(OuterLockOutcome.Failed);
        result.Detail.Should().Contain("Bad session key");
    }

    // Missing is NOT failed. Owner treats the two differently: a real backup refuses on
    // both, a watermarked practice backup tolerates only this one. Collapsing them would
    // either kill the browser demo or let a real backup through unchecked.
    [Fact]
    public async Task A_missing_gpg_is_unavailable_not_a_failure()
    {
        var result = await Verify(new GpgRunDto
        {
            ExitCode = -1,
            GpgMissing = true,
            StderrText = "gpg could not be run. It is expected to be present: Tails ships GnuPG.",
        });

        result.Outcome.Should().Be(OuterLockOutcome.Unavailable);
        result.Detail.Should().Contain("Tails ships GnuPG");
    }

    [Fact]
    public async Task An_interop_exception_is_unavailable_not_a_verdict_on_the_backup()
    {
        var result = await new TauriPgpVerifier(new ThrowingInterop())
            .VerifyAsync(Armored, AgeFile, Key);

        result.Outcome.Should().Be(OuterLockOutcome.Unavailable);
        result.Detail.Should().Contain("no shell here");
    }

    [Fact]
    public async Task A_key_that_is_not_32_bytes_fails_before_the_shell_is_touched()
    {
        var result = await new TauriPgpVerifier(new ThrowingInterop())
            .VerifyAsync(Armored, AgeFile, [1, 2, 3]);

        result.Outcome.Should().Be(OuterLockOutcome.Failed);
        result.Detail.Should().Contain("32 bytes");
    }
}
