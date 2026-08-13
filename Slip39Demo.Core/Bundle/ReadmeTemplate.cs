using System.Text;

namespace Slip39Demo.Core.Bundle;

// Builds the per-share README.txt that travels with every SLIP-39 share.
//
// Audience: this file is written for the RECOVERER (the executor / whoever
// reconstructs the wallet), NOT for the share-holder. A share-holder only needs
// to store the share safely, so the README says exactly that in one line and
// otherwise addresses the recoverer.
//
// Deliberately withheld: the threshold / group structure. Printing "any 3 of 5"
// on every share would leak the scheme to each holder (aiding collusion) and is
// redundant: SLIP-39 encodes the required share count inside each mnemonic, so a
// recovery tool derives it from the shares themselves. We also do NOT tell the
// holder the share is "useless alone": that invites carelessness with something
// that is valuable once combined with threshold-1 others.
//
// It contains NO secret data: the secret is in share.slip39 (the mnemonic) and
// in payload.age.gpg.asc (the encrypted blob, two locks deep).
//
// Plain text, no Markdown, so it stays readable in any editor, printed on
// paper, or pasted into a password-manager note.
public static class ReadmeTemplate
{
    public static string Build(
        string groupName,
        int shareIndex,
        int shareCountInGroup,
        string createdDate,
        string toolVersion,
        bool testOnly = false)
    {
        var sb = new StringBuilder();

        // ── Test watermark: generated online or without the airgap attestation.
        //    Baked into the artifact itself (not just the UI/filename) so a
        //    test share can never quietly graduate into a real backup.
        if (testOnly)
        {
            sb.AppendLine("!!!! INSECURE TEST BACKUP: DO NOT USE FOR REAL FUNDS !!!!");
            sb.AppendLine("Generated in an unverified environment (machine online or");
            sb.AppendLine("airgap not confirmed). For practice and testing only.");
            sb.AppendLine();
        }

        // ── Header: identifies which share this is (matches the zip filename) and
        //    the tool that made it. No threshold is revealed here.
        sb.AppendLine($"SLIP-39 SHARE BACKUP: {groupName} share {shareIndex} of {shareCountInGroup}");
        sb.AppendLine("================================================================");
        sb.AppendLine($"Created: {createdDate}");
        sb.AppendLine($"Tool: slip39-backup v{toolVersion}");

        // ── Holder-facing: one line, then explicitly off the hook.
        sb.AppendLine();
        sb.AppendLine("This file is ONE PIECE of a Bitcoin wallet backup. Keep it");
        sb.AppendLine("private and physically intact. Do not photograph it or copy it");
        sb.AppendLine("onto an internet-connected device.");
        sb.AppendLine();
        sb.AppendLine("If you are only HOLDING this share, nothing else here needs your");
        sb.AppendLine("attention: just store it safely until the wallet's owner or their");
        sb.AppendLine("executor asks for it. The rest of this file is for whoever");
        sb.AppendLine("RECONSTRUCTS the wallet.");

        // ── Recovery procedure: for the executor / recoverer. Note it never prints
        //    the threshold: the SLIP-39 tool reads that from the shares.
        sb.AppendLine();
        sb.AppendLine("────────────────────────────────────────────────────────────────");
        sb.AppendLine("RECOVERY PROCEDURE (for the executor or recoverer)");
        sb.AppendLine("────────────────────────────────────────────────────────────────");
        sb.AppendLine();
        sb.AppendLine("You need:");
        sb.AppendLine("  - Enough of these SLIP-39 shares to meet the backup's threshold.");
        sb.AppendLine("    A SLIP-39 tool reads the required number from the shares");
        sb.AppendLine("    themselves and tells you when you have enough.");
        sb.AppendLine("  - The separate encrypted file `payload.age.gpg.asc`, from the");
        sb.AppendLine("    owner's password manager OR their offline backups.");
        sb.AppendLine("  - A clean machine running Tails, disconnected from every");
        sb.AppendLine("    network. Required, not preferred: recovery shows the seed");
        sb.AppendLine("    words on screen.");
        sb.AppendLine("  - The slip39-backup tool that produced this share (or an");
        sb.AppendLine("    alternative below).");
        sb.AppendLine();
        sb.AppendLine("Steps:");
        sb.AppendLine("  1. Combine threshold-many shares with a SLIP-39 tool to recover");
        sb.AppendLine("     a 32-byte key.");
        sb.AppendLine("  2. Use that key to unlock `payload.age.gpg.asc`. It has two");
        sb.AppendLine("     locks, so this is two commands with the SAME key: `gpg -d`");
        sb.AppendLine("     first, then `age -d` on the result. MANUAL-RECOVERY.txt");
        sb.AppendLine("     spells both out, including where to get the programs.");
        sb.AppendLine("  3. The decrypted file holds the wallet's seed words, optional");
        sb.AppendLine("     passphrase, derivation path, and label.");
        sb.AppendLine();
        sb.AppendLine("This share's mnemonic is in the file `share.slip39`, and as a");
        sb.AppendLine("QR code in `share-qr.png` (scanning it yields the words directly).");
        sb.AppendLine();
        sb.AppendLine("Full step-by-step instructions for recovering WITHOUT this");
        sb.AppendLine("tool, using only standard SLIP-39, GnuPG and age software, are");
        sb.AppendLine("in the file `MANUAL-RECOVERY.txt` next to this README.");

        // ── Fallback tools: in case the original tool is gone, name the interop alternatives.
        sb.AppendLine();
        sb.AppendLine("────────────────────────────────────────────────────────────────");
        sb.AppendLine("ALTERNATIVE TOOLS (if slip39-backup itself is unavailable)");
        sb.AppendLine("────────────────────────────────────────────────────────────────");
        sb.AppendLine();
        sb.AppendLine("SLIP-39 reconstruction:");
        sb.AppendLine("  - python-shamir-mnemonic (reference)  https://github.com/trezor/python-shamir-mnemonic");
        sb.AppendLine("  - revised web page  https://3rditeration.github.io/slip39/src/");
        sb.AppendLine("  - slip39-js  https://github.com/ilap/slip39-js");
        sb.AppendLine("  - Xecrets.Slip39  https://github.com/xecrets/xecrets-slip39");
        sb.AppendLine("  (NOTE: the original iancoleman.io/slip39 web page does not support");
        sb.AppendLine("   the extendable shares this backup uses, as of 2026: use the");
        sb.AppendLine("   revised page above instead.)");
        sb.AppendLine();
        sb.AppendLine("age decryption:");
        sb.AppendLine("  - age (Go)   https://github.com/FiloSottile/age");
        sb.AppendLine("  - rage (Rust)  https://github.com/str4d/rage");

        // ── Spec references: ultimate fallback: the cryptography itself is fully specified.
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

        // Normalised to LF, because AppendLine emits Environment.NewLine and this document
        // must not depend on the machine that built it. A Windows build shipped CRLF and
        // the Linux CI that publishes the AppImage shipped LF, so the same version of the
        // same share README came out as two different files. ShareZipWriter fixes
        // timestamps to make share zips reproducible; this is the other half of that.
        return sb.ToString().Replace("\r\n", "\n");
    }
}
