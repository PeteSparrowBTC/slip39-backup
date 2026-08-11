using CSharpFunctionalExtensions;

namespace Slip39Demo.Core.Payload;

// Emits the wallet payload and proves the text reads back as exactly the payload it was
// asked to write, naming the field if it does not.
//
// WHY THIS IS A GATE AND NOT JUST A TEST
// The canonical format puts each value verbatim on one line, so any value the format
// cannot carry comes back different, and nothing downstream can tell. The ciphertext is
// well formed either way. The independent verifier compares that ciphertext against the
// SAME text this code emitted, so it agrees. The wallet fingerprint in the verification
// record is computed from the form rather than from the payload, so it agrees too. A
// backup can therefore pass every check the tool performs and still recover a different
// wallet: the failure surfaces years later as a recovery that completes and finds an
// empty wallet.
//
// A test only covers values somebody thought of. This runs on the values the owner
// actually typed, every time, and refuses the backup rather than shipping one that
// cannot be read back. The failure mode being closed here is silence, so the fix has to
// be something that speaks.
//
// This is the only emit path generation should use. PayloadEmitter.Emit stays public for
// tests and for building fixtures.
public static class PayloadRoundTrip
{
    // Appended to every refusal. People read the first sentence of an error and act on
    // it, so the message has to say what happened to their data (nothing) and why the
    // tool is refusing rather than coping.
    const string Advice =
        " Nothing was encrypted and nothing was saved. This is a deliberate refusal, not a "
        + "crash: a payload that does not read back identically would recover a different "
        + "wallet, and no later check in this tool would notice.";

    public static Result<string> EmitChecked(PayloadV1_1 payload)
    {
        // Line breaks are checked BEFORE emitting rather than being left to the
        // comparison. A value split across lines does not come back as a clean mismatch:
        // the second half arrives where a key should be, and the reparse fails with a
        // message assembled out of the owner's own passphrase. Naming the field here is
        // both clearer to act on and safer to display.
        var split = SingleLineFields(payload)
            .FirstOrDefault(f => f.Value is not null && (f.Value.Contains('\n') || f.Value.Contains('\r')));
        if (split.Field is not null)
            return Result.Failure<string>(
                $"{split.Field} contains a line break, which the wallet payload format cannot "
                + $"carry: every value is written on one line.{Advice}");

        var text = PayloadEmitter.Emit(payload);
        var reparsed = PayloadParser.Parse(text);
        if (reparsed.IsFailure)
            return Result.Failure<string>(
                $"the wallet payload could not be read back after writing it ({reparsed.Error}).{Advice}");

        var difference = FirstDifference(payload, reparsed.Value);
        return difference is null
            ? Result.Success(text)
            : Result.Failure<string>(
                $"the wallet payload does not survive being written and read back: {difference}.{Advice}");
    }

    // Every field the format writes on a single line, under the name a reader would
    // recognise. One list drives both the line-break check and the comparison, so a
    // field added to the payload cannot be covered by one and forgotten by the other.
    //
    // notes is deliberately absent: it is emitted as an indented block, so line breaks
    // in it are legal. It is compared separately.
    static IReadOnlyList<(string Field, string? Value)> SingleLineFields(PayloadV1_1 p) =>
    [
        ("schema_version", p.SchemaVersion),
        ("created", p.Created),
        ("the wallet label", p.Label),
        ("the seed words", p.TopLevelSeedWords),
        ("the descriptor", p.Descriptor),
        ("threshold", p.Threshold),
        ("slip39_extendable", p.Slip39Extendable.ToString()),
        // Named by the id the owner gave the cosigner, so a four-cosigner multisig says
        // which one broke.
        ..p.Cosigners.SelectMany(c => new (string Field, string? Value)[]
        {
            ($"the id of cosigner '{c.Id}'", c.Id),
            ($"the wallet type of cosigner '{c.Id}'", c.WalletType),
            ($"the passphrase for cosigner '{c.Id}'", c.Passphrase),
            ($"the derivation path for cosigner '{c.Id}'", c.DerivationPath),
            ($"the seed words for cosigner '{c.Id}'", c.SeedWords),
            ($"the xpub fingerprint for cosigner '{c.Id}'", c.XpubFingerprint),
        }),
    ];

    // The first field that differs, or null when everything matched. First rather than
    // all: one named field is actionable, a list of eight is a wall of text, and the
    // usual cause (a single pasted value with a stray character) produces exactly one.
    static string? FirstDifference(PayloadV1_1 wanted, PayloadV1_1 got)
    {
        // Checked before zipping the field lists, which are index-aligned only while the
        // cosigner counts agree.
        if (wanted.Cosigners.Count != got.Cosigners.Count)
            return $"{wanted.Cosigners.Count} cosigners were written but {got.Cosigners.Count} read back";

        var scalar = SingleLineFields(wanted)
            .Zip(SingleLineFields(got), (w, g) => (w.Field, Wanted: w.Value, Got: g.Value))
            .Where(f => f.Wanted != f.Got)
            .Select(f => Describe(f.Field, f.Wanted, f.Got))
            .FirstOrDefault();

        return scalar ?? (wanted.Notes != got.Notes ? Describe("the notes", wanted.Notes, got.Notes) : null);
    }

    // Names the field and the cause, and NEVER the value.
    //
    // Echoing both strings would make the message far easier to read, and that is the
    // trap: the values here are seed words and passphrases, and this text lands in an
    // on-screen banner that gets photographed, read over a shoulder, or pasted into a
    // bug report. A length and a description of the cause are enough to act on and
    // disclose nothing.
    static string Describe(string field, string? wanted, string? got) =>
        (wanted, got) switch
        {
            (null, not null) => $"{field} was left out but read back as present",
            (not null, null) => $"{field} was written but read back as absent",
            (null, null) => $"{field} differs",   // unreachable: equal values never arrive here
            _ => $"{field} changed{Cause(wanted, got)} "
                 + $"({wanted.Length} characters written, {got.Length} read back)",
        };

    // Why the value did not survive. Whitespace at the edges is the case worth naming,
    // because someone comparing the two by eye sees two identical strings and concludes
    // the tool is broken.
    static string Cause(string wanted, string got) =>
        wanted.Trim() == got.Trim()
            ? ": the difference is whitespace at the start or the end"
            : "";
}
