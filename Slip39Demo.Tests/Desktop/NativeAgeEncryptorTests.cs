using System.Text;
using FluentAssertions;
using Slip39Demo.Core.Age;
using Slip39Demo.Desktop.Services;
using Xunit;

namespace Slip39Demo.Tests.Desktop;

// Exercises the subprocess path against a REAL age release rather than a mock.
// Mocking a process boundary tests the mock; the interesting failures all live in
// the boundary itself (argument handling, the plugin being found, stdin/stdout as
// bytes rather than text, exit codes, the passphrase actually arriving).
//
// The binaries are not committed: 10 MB of third-party executable does not belong
// in this repository, and a stale copy would be worse than none. Point
// SLIP39_AGE_DIR at an unpacked age release to run these. CI sets it after
// downloading the same pinned version the AppImage bundles; locally:
//
//   gh release download v1.3.1 --repo FiloSottile/age --pattern '*linux-amd64.tar.gz'
//   tar -xzf age-v1.3.1-linux-amd64.tar.gz
//   SLIP39_AGE_DIR=$PWD/age dotnet test
//
// Skipping when unset is deliberate. The alternative, failing, would make every
// contributor's first test run red for a reason unrelated to their change.
public class NativeAgeEncryptorTests
{
    static string? AgeDir => Environment.GetEnvironmentVariable("SLIP39_AGE_DIR");

    static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)(i * 7)).ToArray();
    static readonly byte[] Plaintext = Encoding.UTF8.GetBytes(
        "schema_version: 1.1\nseed_words: abandon abandon abandon about\npassphrase: hunter2\n");

    [SkippableFact]
    public async Task NativeAge_EncryptsToAFileAgeSharpCanRead()
    {
        Skip.If(AgeDir is null, "set SLIP39_AGE_DIR to an unpacked age release");

        var result = await new NativeAgeEncryptor(AgeDir).EncryptAsync(Plaintext, Key);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : "");

        var ciphertext = result.Value.Ciphertext;
        Encoding.ASCII.GetString(ciphertext, 0, 21).Should().Be("age-encryption.org/v1");

        // The cross-implementation claim in one assertion: what the Go binary
        // wrote, the C# library reads, and it is byte-identical to what went in.
        var back = AgePassphrase.Decrypt(ciphertext, Key);
        back.IsSuccess.Should().BeTrue(back.IsFailure ? back.Error : "");
        back.Value.Should().Equal(Plaintext);
    }

    // The transcript is the whole point of running a separate program: the user is
    // meant to read what happened. If it does not name the binary, its hash and
    // the command, it is decoration.
    [SkippableFact]
    public async Task Transcript_ShowsTheBinaryItsHashAndTheCommand()
    {
        Skip.If(AgeDir is null, "set SLIP39_AGE_DIR to an unpacked age release");

        var result = await new NativeAgeEncryptor(AgeDir).EncryptAsync(Plaintext, Key);
        result.IsSuccess.Should().BeTrue();

        var text = string.Join("\n", result.Value.Transcript.Lines.Select(l => $"{l.Kind}: {l.Text}"));

        text.Should().Contain("SHA-256:");
        text.Should().Contain("--version");
        text.Should().Contain("--encrypt -j batchpass");
        text.Should().Contain("AGE_PASSPHRASE");
        result.Value.Transcript.Summary.Should().Contain("official age program");

        // Nothing secret may reach the screen.
        text.Should().NotContain(Convert.ToHexString(Key).ToLowerInvariant());
        text.Should().NotContain("hunter2");
    }

    // Fail closed. A missing binary must stop generation, never quietly fall back
    // to the in-process library this class exists to avoid.
    [Fact]
    public async Task MissingBinary_FailsRatherThanFallingBack()
    {
        var result = await new NativeAgeEncryptor(Path.Combine(Path.GetTempPath(), "no-age-here"))
            .EncryptAsync(Plaintext, Key);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("missing");
        result.Error.Should().Contain("refuses to fall back");
    }

    [Fact]
    public async Task WrongKeyLength_IsRejectedBeforeAnythingIsExecuted()
    {
        var result = await new NativeAgeEncryptor(AgeDir).EncryptAsync(Plaintext, new byte[31]);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("32 bytes");
    }
}
