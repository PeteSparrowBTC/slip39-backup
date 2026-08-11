namespace Slip39Demo.UI.Services;

// Opens the OUTER lock of the artifact the owner keeps, using software this project did
// not write, and checks that what comes out is exactly the age file that went in.
//
// WHY THIS EXISTS
// The outer OpenPGP layer is produced in-process by BouncyCastle, and encryption
// failures are silent: a file written with the wrong key, the wrong passphrase encoding
// or a subtly malformed packet still looks like an OpenPGP message, and BouncyCastle
// will happily decrypt its own output whatever it did. The tool would be vouching for
// itself. So the check is run by the system's own GnuPG, the implementation everyone
// else's recovery instructions assume, and the one already present on Tails.
//
// This is the same argument that put the official age binary on the encryption path
// (see IPayloadEncryptor) applied to the second layer. Without it, the outer lock is the
// only part of the backup that nothing independent has ever opened.
//
// WHY NOT A BUNDLED JAVASCRIPT OpenPGP LIBRARY
// Because it would sit next to the problem, not outside it: a checker shipped inside our
// own bundle cannot independently vouch for its own producer. GnuPG is already on the
// target machine, so nothing has to be bundled to get a genuinely foreign opinion.
//
// WHY NOT Result<T>
// Three outcomes matter and only two of them are a verdict on the backup. "GnuPG is not
// installed" is a fact about the machine, and the caller applies a different rule to it
// than to "GnuPG opened this and disagreed". Squeezing that into a success/failure pair
// would push the distinction into string matching on an error message.
public enum OuterLockOutcome
{
    // gpg ran, opened the envelope, and returned the expected bytes.
    Verified,

    // gpg could not be run at all: not installed, or not reachable from this build.
    // Not a statement about the backup.
    Unavailable,

    // gpg ran and disagreed: it refused the passphrase, errored, or returned something
    // other than the age file that went in. Always fatal.
    Failed,
}

// Detail is one line for the error banner or the result panel. Transcript is what the
// user can read to see the check happen rather than take our word for it, in the same
// shape the encryption transcript uses, and contains no key material by construction.
public sealed record OuterLockVerification(
    OuterLockOutcome Outcome,
    string Detail,
    IReadOnlyList<TranscriptLine> Transcript)
{
    public static OuterLockVerification Unavailable(string detail) =>
        new(OuterLockOutcome.Unavailable, detail, []);

    public static OuterLockVerification Failed(string detail, IReadOnlyList<TranscriptLine>? transcript = null) =>
        new(OuterLockOutcome.Failed, detail, transcript ?? []);

    public static OuterLockVerification Verified(string detail, IReadOnlyList<TranscriptLine> transcript) =>
        new(OuterLockOutcome.Verified, detail, transcript);
}

public interface IOuterLockVerifier
{
    // armoredEnvelope: the ASCII-armored OpenPGP message the bundle ships.
    // expectedInner: the age file that was wrapped, byte for byte.
    // key32: the 32-byte SLIP-39 master secret; implementations hex-encode it the way
    // PgpEnvelope does, so a divergence in that convention is one of the things this
    // catches.
    Task<OuterLockVerification> VerifyAsync(string armoredEnvelope, byte[] expectedInner, byte[] key32);
}
