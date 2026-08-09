using CSharpFunctionalExtensions;

namespace Slip39Demo.UI.Services;

// Encrypts the wallet payload to an age v1 file, and reports exactly how it did
// it so the user can see rather than trust.
//
// WHY THIS IS AN INTERFACE
// Encryption failures are silent and decryption failures are loud: a file
// encrypted with a reused nonce or a weak key still decrypts perfectly, forever,
// while a bad decrypt shows up immediately as missing or wrong plaintext. So the
// side where mistakes are invisible gets the most-audited implementation
// available, and the AppImage (the artifact that touches real seed phrases) runs
// the reference age binary in a subprocess.
//
// The Blazor WASM build cannot exec anything and stays on the in-process
// AgeSharp path. That divergence is acceptable because the web build is marked
// DEMONSTRATION AND TESTING ONLY and watermarks everything it produces as
// INSECURE-TEST. It must never be the thing that guards a real wallet.
//
// There is deliberately NO fallback from native to in-process. If the bundled
// binary is missing or misbehaves, generation fails and says so. Silently
// dropping back to the implementation we were trying to avoid would make the
// whole exercise decorative.
public interface IPayloadEncryptor
{
    // key32 is the 32-byte SLIP-39 master secret; implementations hex-encode it
    // as the age passphrase, the convention AgePassphrase defines.
    Task<Result<EncryptionOutcome>> EncryptAsync(byte[] plaintext, byte[] key32);
}

// The ciphertext, plus a human-readable account of how it was produced.
public sealed record EncryptionOutcome(byte[] Ciphertext, EncryptionTranscript Transcript);

// What the app shows the user after encrypting. Everything here is safe to
// display: it describes the mechanism, never the key or the plaintext.
//
// Summary is one line for the impatient. Lines are the transcript proper, in the
// order things happened, so somebody who wants to check the work can read the
// command that ran, against which binary, and what it said back.
public sealed record EncryptionTranscript(
    string Summary,
    IReadOnlyList<TranscriptLine> Lines);

// Kind drives presentation only. Command and Output are rendered monospaced so a
// reader can tell what the app ran from what the program answered.
public sealed record TranscriptLine(TranscriptLineKind Kind, string Text);

public enum TranscriptLineKind
{
    Note,     // explanation in prose
    Command,  // something the app executed
    Output,   // what the executed thing printed
    Warning,  // a caveat the reader should not skim past
}
