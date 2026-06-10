using Age;
using Age.Recipients;
using CSharpFunctionalExtensions;

namespace Slip39Demo.Core.Age;

// Thin wrapper over AgeSharp's passphrase (scrypt) mode. Per the SLIP-39 + age
// redesign spec, the age "passphrase" is always the lowercase hex encoding of
// the 32-byte SLIP-39 master_secret (64 hex characters). Callers therefore pass
// the raw 32 bytes (key32) and never see the hex-encoding choice — this wrapper
// centralises the encoding so the rest of the codebase cannot accidentally use
// a different convention.
//
// Underlying AgeSharp API (verified against AgeSharp 0.2.0-preview.2):
//   - Age.AgeEncrypt.Encrypt(Stream in, Stream out, params IRecipient[] recipients)
//   - Age.AgeEncrypt.Decrypt(Stream in, Stream out, params IIdentity[] identities)
//   - Age.Recipients.ScryptRecipient(string passphrase) implements both
//     IRecipient and IIdentity, so the same type is used for encrypt + decrypt.
// On wrong passphrase, AgeSharp throws NoIdentityMatchException, which we
// catch and turn into a Result.Failure so call sites can branch cleanly.
public static class AgePassphrase
{
    // Encrypts plaintext to a valid age v1 file (starts with the
    // "age-encryption.org/v1" magic header) using the 32-byte key as the
    // hex-encoded scrypt passphrase. Returns Failure if the key is not exactly
    // 32 bytes, or if AgeSharp throws for any reason.
    public static Result<byte[]> Encrypt(byte[] plaintext, byte[] key32) =>
        ValidateKey(key32)
            .Bind(_ => TryRun(() =>
            {
                var recipient = new ScryptRecipient(ToHexPassphrase(key32));
                using var input = new MemoryStream(plaintext);
                using var output = new MemoryStream();
                AgeEncrypt.Encrypt(input, output, recipient);
                return output.ToArray();
            }, "age encrypt failed"));

    // Decrypts an age v1 ciphertext using the 32-byte key as the hex-encoded
    // scrypt passphrase. Returns Failure on the usual error paths: wrong key
    // (AgeSharp throws NoIdentityMatchException), malformed ciphertext, or
    // any other AgeSharp-internal exception.
    public static Result<byte[]> Decrypt(byte[] ciphertext, byte[] key32) =>
        ValidateKey(key32)
            .Bind(_ => TryRun(() =>
            {
                var identity = new ScryptRecipient(ToHexPassphrase(key32));
                using var input = new MemoryStream(ciphertext);
                using var output = new MemoryStream();
                AgeEncrypt.Decrypt(input, output, identity);
                return output.ToArray();
            }, "age decrypt failed"));

    // Validates the key is exactly 32 bytes — the SLIP-39 master_secret length
    // mandated by the redesign spec. Returns the error message verbatim
    // (tests assert that "32 bytes" appears in the failure text).
    static Result<byte[]> ValidateKey(byte[] key32) =>
        key32.Length == 32
            ? Result.Success(key32)
            : Result.Failure<byte[]>($"key must be 32 bytes (got {key32.Length})");

    // Converts the 32-byte key into the canonical 64-character lowercase hex
    // passphrase. Convert.ToHexString returns uppercase, so we lowercase.
    static string ToHexPassphrase(byte[] key32) =>
        Convert.ToHexString(key32).ToLowerInvariant();

    // Generic exception->Result bridge used by Encrypt and Decrypt. AgeSharp
    // signals all failures (wrong passphrase, corrupt header, etc.) as
    // exceptions; we surface the exception type and message in the Result
    // text for diagnostics.
    static Result<byte[]> TryRun(Func<byte[]> action, string prefix)
    {
        try
        {
            return Result.Success(action());
        }
        catch (Exception ex)
        {
            return Result.Failure<byte[]>($"{prefix}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
