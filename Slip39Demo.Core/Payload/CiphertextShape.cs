using System.Text;

namespace Slip39Demo.Core.Payload;

// What kind of encrypted payload the recoverer has been handed.
//
// WHY THIS IS A TESTED FUNCTION AND NOT AN `if` IN THE PAGE
// Recovery is the moment nobody can afford a wrong guess, and there are four shapes
// a payload can arrive in: an age file or an OpenPGP envelope, each either binary or
// ASCII armored. Getting this wrong does not produce a clear error. It sends the
// bytes to the wrong decryptor, which reports that the file is corrupt or that the
// shares do not match, and an heir reads that as "the backup is broken" rather than
// "the tool did not recognise the format".
//
// It began as a one-line high-bit check inside Recoverer.razor, which handled binary
// OpenPGP and silently mishandled armored OpenPGP, because armor is ASCII and the
// high bit is clear. That is exactly the case a password-manager note produces.
//
// DETECTION IS BY CONTENT, NEVER BY FILE NAME
// The file may have been renamed, downloaded from a password manager as
// "attachment.dat", pasted into a note and back out, or handed over with no
// extension. The bytes are the only thing that travelled intact.
public enum CiphertextShape
{
    // "age-encryption.org/v1" in the clear, and also the default for anything
    // unrecognised, so the age decryptor reports the error.
    AgeBinary,

    // "-----BEGIN AGE ENCRYPTED FILE-----". We no longer emit this, but an heir may
    // hold one from an earlier backup or from another tool, so it stays readable.
    AgeArmored,

    // An OpenPGP packet. The first byte is a packet tag, which always has the high
    // bit set, so it cannot be confused with either age form or with armor.
    OpenPgpBinary,

    // "-----BEGIN PGP MESSAGE-----", what `gpg --armor` writes and what a password
    // manager note holds.
    OpenPgpArmored,
}

public static class CiphertextShapeDetector
{
    const string AgeArmorFence = "-----BEGIN AGE ENCRYPTED FILE-----";
    const string PgpArmorFence = "-----BEGIN PGP MESSAGE-----";

    // How far in to look for a fence. Armor may be preceded by whitespace or by
    // explanatory text a person typed above it in a note, and both survive a paste.
    // Bounded so a large binary file is not scanned as text.
    const int PrefixBytes = 2048;

    public static CiphertextShape Detect(byte[] data)
    {
        if (data.Length == 0)
            return CiphertextShape.AgeBinary; // nothing to go on; the age path reports it

        // An OpenPGP packet tag has the high bit set. age and both armor forms are
        // printable ASCII, so one byte separates binary OpenPGP from everything else.
        if ((data[0] & 0x80) != 0)
            return CiphertextShape.OpenPgpBinary;

        // Latin1 rather than UTF8: this is a byte-for-byte view for fence matching, and
        // UTF8 decoding of arbitrary binary can throw or substitute replacement
        // characters, either of which would break the match for the wrong reason.
        var prefix = Encoding.Latin1.GetString(data, 0, Math.Min(data.Length, PrefixBytes));

        if (prefix.Contains(PgpArmorFence, StringComparison.Ordinal))
            return CiphertextShape.OpenPgpArmored;
        if (prefix.Contains(AgeArmorFence, StringComparison.Ordinal))
            return CiphertextShape.AgeArmored;

        // Everything else goes to the age path, including bytes matching nothing at
        // all. Deliberate rather than a fallthrough: age is the inner format, so its
        // decryptor gives the most useful error for an unrecognised payload, whereas
        // guessing OpenPGP would report a missing packet for a file that was never an
        // envelope. There is no positive test for the age magic because it would change
        // no outcome, and a check that changes no outcome invites someone to trust it.
        return CiphertextShape.AgeBinary;
    }

    // True when the payload carries an OpenPGP envelope that has to come off before
    // the age layer underneath can be reached.
    public static bool IsOpenPgp(this CiphertextShape shape) =>
        shape is CiphertextShape.OpenPgpBinary or CiphertextShape.OpenPgpArmored;

    // True when the bytes are text that needs dearmoring before the age decryptor
    // sees them. OpenPGP armor is not included: PgpEnvelope.Decrypt strips that
    // itself through GetDecoderStream.
    public static bool IsAgeArmor(this CiphertextShape shape) =>
        shape is CiphertextShape.AgeArmored;
}
