using System.Text;
using Slip39Demo.Core.Slip39;

namespace Slip39Demo.Core.Bundle;

// Builds the per-share README.txt that travels with every SLIP-39 share.
// The README is human-readable, tells the recoverer which tools to use, and
// includes the full threshold structure so they know what mix of shares
// they need to gather. It contains NO secret data — the secret is in
// share.slip39 (the mnemonic) and in payload.age (the encrypted blob).
//
// The template is intentionally plain text — no Markdown, no fancy formatting —
// so it remains readable in any text editor, printed on paper, or pasted into
// a password-manager note. Future PDF templates (Phase 2) will format the same
// content for print but the canonical recovery text is here.
public static class ReadmeTemplate
{
    public static string Build(
        GroupConfig cfg,
        string groupName,
        int shareIndex,
        int shareCountInGroup,
        string createdDate,
        string toolVersion)
    {
        // Functional composition of the group list — one line per group,
        // joined with newlines and indented to match the surrounding block.
        var groupLines = string.Join(
            Environment.NewLine,
            cfg.Groups.Select(g => $"    {g.Name}: {g.Threshold}-of-{g.Count}"));

        var sb = new StringBuilder();

        // ── Header: identifies which share this is and the tool that made it.
        sb.AppendLine($"SLIP-39 SHARE BACKUP — {groupName} share {shareIndex} of {shareCountInGroup}");
        sb.AppendLine("================================================================");
        sb.AppendLine($"Created: {createdDate}");
        sb.AppendLine($"Tool: Seed-Phrase-Storage-SLIP39 v{toolVersion}");
        sb.AppendLine("Threshold structure:");
        sb.AppendLine($"    Group threshold: any {cfg.GroupThreshold} of {cfg.Groups.Count} groups recover.");
        sb.AppendLine(groupLines);

        // ── Recipient-facing notice: a single share is useless on its own.
        sb.AppendLine();
        sb.AppendLine("────────────────────────────────────────────────────────────────");
        sb.AppendLine("IF YOU ARE THE RECIPIENT OF THIS FILE:");
        sb.AppendLine("────────────────────────────────────────────────────────────────");
        sb.AppendLine();
        sb.AppendLine("This share alone reveals NOTHING. It's cryptographically");
        sb.AppendLine("useless without enough companion shares to meet the");
        sb.AppendLine("threshold above AND access to a separate encrypted file");
        sb.AppendLine("(payload.age) held by the original owner / their password");
        sb.AppendLine("manager / their executor.");

        // ── Recovery procedure: what the executor needs to actually rebuild the secret.
        sb.AppendLine();
        sb.AppendLine("────────────────────────────────────────────────────────────────");
        sb.AppendLine("RECOVERY PROCEDURE (for the executor or recoverer)");
        sb.AppendLine("────────────────────────────────────────────────────────────────");
        sb.AppendLine();
        sb.AppendLine("The recoverer needs:");
        sb.AppendLine("  - Threshold-many shares from the groups above.");
        sb.AppendLine("  - The file `payload.age`, obtained from the owner's password");
        sb.AppendLine("    manager OR from the owner's offline backups.");
        sb.AppendLine("  - A clean offline machine — Tails ideally.");
        sb.AppendLine("  - The SPS-SLIP39 tool that produced this share.");

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
        sb.AppendLine("This share's mnemonic is in share.slip39.");
        sb.AppendLine("End of README.");

        return sb.ToString();
    }
}
