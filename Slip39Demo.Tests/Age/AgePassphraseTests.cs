using FluentAssertions;
using Slip39Demo.Core.Age;
using Xunit;

namespace Slip39Demo.Tests.Age;

public class AgePassphraseTests
{
    // Deterministic 32-byte key for round-trip tests — bytes 0x00..0x1f.
    static readonly byte[] FixedKey = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    [Fact]
    public void EncryptThenDecrypt_RecoversOriginalPlaintext()
    {
        var plaintext = "hello, world\nthis is a payload"u8.ToArray();

        var ciphertext = AgePassphrase.Encrypt(plaintext, FixedKey);
        ciphertext.IsSuccess.Should().BeTrue();
        ciphertext.Value.Should().NotEqual(plaintext);

        var roundTripped = AgePassphrase.Decrypt(ciphertext.Value, FixedKey);
        roundTripped.IsSuccess.Should().BeTrue();
        roundTripped.Value.Should().Equal(plaintext);
    }

    [Fact]
    public void Decrypt_WithWrongKey_ReturnsFailure()
    {
        var plaintext = "secret"u8.ToArray();
        var ciphertext = AgePassphrase.Encrypt(plaintext, FixedKey).Value;
        var wrongKey = new byte[32]; // all zeros, definitely not FixedKey

        var attempt = AgePassphrase.Decrypt(ciphertext, wrongKey);

        attempt.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Encrypt_WithKeyOfWrongLength_ReturnsFailure()
    {
        var attempt = AgePassphrase.Encrypt("x"u8.ToArray(), new byte[31]);
        attempt.IsFailure.Should().BeTrue();
        attempt.Error.Should().Contain("32 bytes");
    }
}
