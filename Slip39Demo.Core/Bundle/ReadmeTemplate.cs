using System.Text;

namespace Slip39Demo.Core.Bundle;

// Builds the per-share README.txt that travels with every SLIP-39 share.
//
// Audience: this file is written for the RECOVERER (the executor / whoever
// reconstructs the wallet), NOT for the share-holder. A share-holder only needs
// to store the share safely — so the README says exactly that in one line and
// otherwise addresses the recoverer.
//
// Deliberately withheld: the threshold / group structure. Printing "any 3 of 5"
// on every share would leak the scheme to each holder (aiding collusion) and is
// redundant — SLIP-39 encodes the required share count inside each mnemonic, so a
// recovery tool derives it from the shares themselves. We also do NOT tell the
// holder the share is "useless alone": that invites carelessness with something
// that is valuable once combined with threshold-1 others.
//
// It contains NO secret data — the secret is in share.slip39 (the mnemonic) and
// in payload.age (the encrypted blob).
//
// Plain text — no Markdown — so it stays readable in any editor, printed on
// paper, or pasted into a password-manager note.
public static class ReadmeTemplate
{
    public static string Build(
        string groupName,
        int shareIndex,
        int shareCountInGroup,
        string createdDate,
        string toolVersion)
    {
        var sb = new StringBuilder();

        // ── Header: identifies which share this is (matches the zip filename) and
        //    the tool that made it. No threshold is revealed here.
        sb.AppendLine($"SLIP-39 SHARE BACKUP — {groupName} share {shareIndex} of {shareCountInGroup}");
        sb.AppendLine("================================================================");
        sb.AppendLine($"Created: {createdDate}");
        sb.AppendLine($"Tool: Seed-Phrase-Storage-SLIP39 v{toolVersion}");

        // ── Holder-facing: one line, then explicitly off the hook.
        sb.AppendLine();
        sb.AppendLine("This file is ONE PIECE of a Bitcoin wallet backup. Keep it");
        sb.AppendLine("private and physically intact. Do not photograph it or copy it");
        sb.AppendLine("onto an internet-connected device.");
        sb.AppendLine();
        sb.AppendLine("If you are only HOLDING this share, nothing else here needs your");
        sb.AppendLine("attention — just store it safely until the wallet's owner or their");
        sb.AppendLine("executor asks for it. The rest of this file is for whoever");
        sb.AppendLine("RECONSTRUCTS the wallet.");

        // ── Recovery procedure: for the executor / recoverer. Note it never prints
        //    the threshold — the SLIP-39 tool reads that from the shares.
        sb.AppendLine();
        sb.AppendLine("────────────────────────────────────────────────────────────────");
        sb.AppendLine("RECOVERY PROCEDURE (for the executor or recoverer)");
        sb.AppendLine("────────────────────────────────────────────────────────────────");
        sb.AppendLine();
        sb.AppendLine("You need:");
        sb.AppendLine("  - Enough of these SLIP-39 shares to meet the backup's threshold.");
        sb.AppendLine("    A SLIP-39 tool reads the required number from the shares");
        sb.AppendLine("    themselves and tells you when you have enough.");
        sb.AppendLine("  - The separate encrypted file `payload.age`, obtained from the");
        sb.AppendLine("    owner's password manager OR their offline backups.");
        sb.AppendLine("  - A clean offline machine — Tails ideally.");
        sb.AppendLine("  - The SPS-SLIP39 tool that produced this share (or an");
        sb.AppendLine("    alternative below).");
        sb.AppendLine();
        sb.AppendLine("Steps:");
        sb.AppendLine("  1. Combine threshold-many shares with a SLIP-39 tool to recover");
        sb.AppendLine("     a 32-byte key.");
        sb.AppendLine("  2. Use that key to decrypt `payload.age` with an age tool.");
        sb.AppendLine("  3. The decrypted file holds the wallet's seed words, optional");
        sb.AppendLine("     passphrase, derivation path, and label.");
        sb.AppendLine();
        sb.AppendLine("This share's mnemonic is in the file `share.slip39`.");

        // ── Fallback tools: in case the original tool is gone, name the interop alternatives.
        sb.AppendLine();
        sb.AppendLine("────────────────────────────────────────────────────────────────");
        sb.AppendLine("ALTERNATIVE TOOLS (if SPS-SLIP39 itself is unavailable)");
        sb.AppendLine("────────────────────────────────────────────────────────────────");
        sb.AppendLine();
        sb.AppendLine("SLIP-39 reconstruction:");
        sb.AppendLine("  - iancoleman/slip39  https://iancoleman.io/slip39/");
        sb.AppendLine("  - python-shamir-mnemonic  https://github.com/trezor/python-shamir-mnemonic");
        sb.AppendLine("  - Xecrets.Slip39  https://github.com/xecrets/xecrets-slip39");
        sb.AppendLine();
        sb.AppendLine("age decryption:");
        sb.AppendLine("  - age (Go)   https://github.com/FiloSottile/age");
        sb.AppendLine("  - rage (Rust)  https://github.com/str4d/rage");

        // ── Spec references: ultimate fallback — the cryptography itself is fully specified.
        sb.AppendLine();
        sb.AppendLine("────────────────────────────────────────────────────────────────");
        sb.AppendLine("SPEC REFERENCES (if every tool above is unavailable)");
        sb.AppendLine("────────────────────────────────────────────────────────────────");
        sb.AppendLine();
        sb.AppendLine("SLIP-39 specification:");
        sb.AppendLine("  https://github.com/satoshilabs/slips/blob/master/slip-0039.md");
        sb.AppendLine();
        sb.AppendLine("age v1 specification:");
        sb.AppendLine("  https://age-encryption.org/v1");
        sb.AppendLine();
        sb.AppendLine("End of README.");

        return sb.ToString();
    }
}
