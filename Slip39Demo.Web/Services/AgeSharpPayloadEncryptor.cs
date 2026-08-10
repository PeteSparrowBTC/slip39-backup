using CSharpFunctionalExtensions;
using Slip39Demo.Core.Age;
using Slip39Demo.UI.Services;

namespace Slip39Demo.Web.Services;

// In-process encryption via AgeSharp. Used by the Blazor WASM build, which
// cannot start a subprocess.
//
// This is the path the native encryptor exists to avoid on real backups, so the
// transcript says so plainly instead of presenting the two as equivalent. A user
// who reads it should come away knowing which one they are on and why it
// matters.
//
// Lives in Slip39Demo.Web rather than in the shared Slip39Demo.UI project on
// purpose: the AppImage frontend, Slip39Demo.Tauri, references UI but never
// references Web, so there is no type here for it to pick up even by mistake.
// That is what turns the claim in Slip39Demo.Tauri.csproj into a structural fact
// about project references rather than a matter of what Program.cs happens to
// register.
public sealed class AgeSharpPayloadEncryptor : IPayloadEncryptor
{
    public Task<Result<EncryptionOutcome>> EncryptAsync(byte[] plaintext, byte[] key32)
    {
        var result = AgePassphrase.Encrypt(plaintext, key32);
        if (result.IsFailure)
            return Task.FromResult(Result.Failure<EncryptionOutcome>(result.Error));

        var transcript = new EncryptionTranscript(
            "Encrypted in this app, using the AgeSharp library (no separate program was run).",
            [
                new(TranscriptLineKind.Note,
                    "This build encrypts inside the app itself, with the AgeSharp library "
                    + "compiled into it. Nothing was executed as a separate program, so there "
                    + "is no command to show you."),
                new(TranscriptLineKind.Note,
                    $"Plaintext in: {plaintext.Length} bytes. Encrypted out: {result.Value.Length} bytes. "
                    + "The key was used as a 64-character hexadecimal passphrase and never left memory."),
                new(TranscriptLineKind.Warning,
                    "The desktop app for Tails does this differently: it runs the official age "
                    + "program from its own release, because a mistake made while encrypting is "
                    + "invisible afterwards. This browser build is for demonstration and testing. "
                    + "Do not guard a real wallet with a backup made here."),
            ]);

        return Task.FromResult(Result.Success(new EncryptionOutcome(result.Value, transcript)));
    }
}
