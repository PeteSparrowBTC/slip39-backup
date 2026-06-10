using CSharpFunctionalExtensions;

namespace Slip39Demo.Core.Payload;

// Parses canonical PayloadV1_1 text (as emitted by PayloadEmitter) back into a
// PayloadV1_1 record. Returns Result<T> for any malformed input — never throws.
// Only schema_version: 1.1 is accepted; any other version is a clean failure.
public static class PayloadParser
{
    public static Result<PayloadV1_1> Parse(string text)
    {
        // Normalise CRLF -> LF so the line scanner can split on a single delimiter.
        var lines = text.Replace("\r\n", "\n").Split('\n');

        // Mutable locals scoped to this method only — public contract stays pure.
        string? schemaVersion = null;
        string? created = null;
        string? label = null;
        string? topSeed = null;
        string? descriptor = null;
        string? threshold = null;
        string? notes = null;
        var slip39Extendable = true;
        var cosigners = new List<Cosigner>();
        Cosigner? curCosigner = null;
        var inCosignersBlock = false;
        var inNotesBlock = false;
        var notesBuf = new List<string>();

        // Helper: build a 1-based line-number error message.
        Result<PayloadV1_1> Fail(int lineNo, string msg) =>
            Result.Failure<PayloadV1_1>($"payload parse error at line {lineNo + 1}: {msg}");

        // Helper: push the cosigner under construction (if any) into the list.
        void FlushCosigner()
        {
            if (curCosigner is not null)
            {
                cosigners.Add(curCosigner);
                curCosigner = null;
            }
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var line = raw.TrimEnd('\r');

            // Notes block: indented continuation lines are collected literally
            // until a non-indented line ends the block. The terminating line
            // then falls through to be processed as a normal top-level line.
            if (inNotesBlock)
            {
                if (line.StartsWith("  "))
                {
                    notesBuf.Add(line.Substring(2));
                    continue;
                }
                inNotesBlock = false;
                notes = string.Join('\n', notesBuf);
            }

            // Blank lines and # comments are skipped at the top level.
            if (line.Length == 0 || line.StartsWith("#"))
                continue;

            // Inside cosigners: "  - id: ..." starts a new cosigner entry.
            if (inCosignersBlock && line.StartsWith("  - "))
            {
                FlushCosigner();
                var rest = line.Substring(4);
                var (k, v) = SplitKV(rest);
                if (k != "id")
                    return Fail(i, "first key of a cosigner entry must be 'id'");
                // Seed an entry with defaults; subsequent four-space-indented
                // lines patch the remaining fields via record `with` expressions.
                curCosigner = new Cosigner(v, "bip39", null, "", null, null);
                continue;
            }

            // Inside cosigners: four-space indent = continuation of current entry.
            if (inCosignersBlock && line.StartsWith("    "))
            {
                if (curCosigner is null)
                    return Fail(i, "cosigner field outside an entry");
                var (k, v) = SplitKV(line.Substring(4));
                curCosigner = k switch
                {
                    "wallet_type"      => curCosigner with { WalletType = v },
                    "passphrase"       => curCosigner with { Passphrase = v },
                    "seed_words"       => curCosigner with { SeedWords = v },
                    "derivation_path"  => curCosigner with { DerivationPath = v },
                    "xpub_fingerprint" => curCosigner with { XpubFingerprint = v },
                    _ => curCosigner
                };
                continue;
            }

            // Any non-cosigner line exits the cosigners block.
            if (inCosignersBlock)
            {
                FlushCosigner();
                inCosignersBlock = false;
            }

            var (key, val) = SplitKV(line);
            switch (key)
            {
                case "schema_version":     schemaVersion = val; break;
                case "created":            created = val; break;
                case "label":              label = StripQuotes(val); break;
                case "seed_words":         topSeed = val; break;
                case "descriptor":         descriptor = val; break;
                case "threshold":          threshold = val; break;
                case "slip39_extendable":  slip39Extendable = val == "true"; break;
                case "cosigners":          inCosignersBlock = true; break;
                case "notes":
                    if (val == "|")
                    {
                        inNotesBlock = true;
                        notesBuf.Clear();
                    }
                    else
                    {
                        notes = val;
                    }
                    break;
                default:
                    return Fail(i, $"unknown key '{key}'");
            }
        }

        // EOF cleanup: flush any in-flight cosigner and finalise an open notes block.
        FlushCosigner();
        if (inNotesBlock)
            notes = string.Join('\n', notesBuf);

        // Required-field validation. v1.1 is the only supported schema version.
        if (schemaVersion is null) return Result.Failure<PayloadV1_1>("missing schema_version");
        if (schemaVersion != "1.1") return Result.Failure<PayloadV1_1>($"unsupported schema_version '{schemaVersion}' (this tool reads only 1.1)");
        if (created is null)       return Result.Failure<PayloadV1_1>("missing created");
        if (threshold is null)     return Result.Failure<PayloadV1_1>("missing threshold");
        if (cosigners.Count == 0)  return Result.Failure<PayloadV1_1>("payload has no cosigners");

        // Explicit Result.Success<T>(...) keeps nullability hints aligned under
        // TreatWarningsAsErrors=true (the implicit T -> Result<T> conversion
        // can otherwise produce a CS8619-style mismatch on records with nullable members).
        return Result.Success<PayloadV1_1>(new PayloadV1_1(
            schemaVersion, created, label, topSeed, cosigners,
            descriptor, threshold, slip39Extendable, notes));
    }

    // Splits "key: value" into ("key", "value"). Missing colon -> empty value.
    static (string Key, string Value) SplitKV(string s)
    {
        var idx = s.IndexOf(':');
        if (idx < 0) return (s.Trim(), "");
        var k = s.Substring(0, idx).Trim();
        var v = idx + 1 < s.Length ? s.Substring(idx + 1).TrimStart() : "";
        return (k, v);
    }

    // Strips a single layer of surrounding double quotes from a label.
    static string StripQuotes(string v) =>
        v.Length >= 2 && v[0] == '"' && v[^1] == '"' ? v.Substring(1, v.Length - 2) : v;
}
