using System.Text;

namespace Slip39Demo.Core.Payload;

// Emits a PayloadV1_1 instance as canonical text. The format is intentionally
// a small, fixed YAML-shaped subset (not full YAML) so that a single
// round-trip parser can recreate the same bytes. This emitter is total —
// it cannot fail — so it returns a plain string (no Result<T>).
public static class PayloadEmitter
{
    public static string Emit(PayloadV1_1 p)
    {
        var sb = new StringBuilder();

        // Header — schema_version and created are always present.
        sb.Append($"schema_version: {p.SchemaVersion}\n");
        sb.Append($"created: {p.Created}\n");

        // Optional label is double-quoted (labels may contain spaces).
        if (p.Label is not null)
            sb.Append($"label: \"{p.Label}\"\n");

        // Top-level seed words (shared-seed mode). Preceded by a blank line.
        if (p.TopLevelSeedWords is not null)
            sb.Append($"\nseed_words: {p.TopLevelSeedWords}\n");

        // Cosigners block — always present, preceded by a blank line.
        sb.Append("\ncosigners:\n");
        foreach (var c in p.Cosigners)
            AppendCosigner(sb, c);

        // Optional descriptor (multisig wallets), preceded by a blank line.
        if (p.Descriptor is not null)
            sb.Append($"\ndescriptor: {p.Descriptor}\n");

        // Threshold and extendable flag are always present, threshold preceded by blank line.
        sb.Append($"\nthreshold: {p.Threshold}\n");
        sb.Append($"slip39_extendable: {(p.Slip39Extendable ? "true" : "false")}\n");

        // Optional notes block — emitted as a YAML literal-style block.
        // Each input line is indented by two spaces.
        if (p.Notes is not null)
        {
            sb.Append("notes: |\n");
            foreach (var line in p.Notes.Split('\n'))
                sb.Append($"  {line}\n");
        }

        return sb.ToString();
    }

    // Per-cosigner block. Field order is fixed so the output is canonical:
    // id, wallet_type, passphrase?, seed_words?, derivation_path, xpub_fingerprint?
    static void AppendCosigner(StringBuilder sb, Cosigner c)
    {
        sb.Append($"  - id: {c.Id}\n");
        sb.Append($"    wallet_type: {c.WalletType}\n");
        if (c.Passphrase is not null)
            sb.Append($"    passphrase: {c.Passphrase}\n");
        if (c.SeedWords is not null)
            sb.Append($"    seed_words: {c.SeedWords}\n");
        sb.Append($"    derivation_path: {c.DerivationPath}\n");
        if (c.XpubFingerprint is not null)
            sb.Append($"    xpub_fingerprint: {c.XpubFingerprint}\n");
    }
}
