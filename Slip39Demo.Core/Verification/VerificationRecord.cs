using System.Security.Cryptography;
using System.Text;
using Slip39Demo.Core.Bip32;

namespace Slip39Demo.Core.Verification;

// Builds the non-secret SPS-SLIP39 verification record. Stored alongside
// payload.age and consumed by the dry-run recovery flow (spec §6.5) so the
// owner can periodically confirm their backup chain still works without
// ever displaying the seed words on screen.
//
// Contents are all one-way derivations — knowing them gives an attacker zero
// information that helps reconstruct the wallet:
//   - Wallet master fingerprint: HASH160 of the master public key, 4 bytes.
//   - Per-share fingerprints: truncated SHA-256 of each mnemonic.
//   - Payload integrity: full SHA-256 of payload.age bytes.
public static class VerificationRecord
{
    public static string Build(
        string createdDate,
        string toolVersion,
        string label,
        IReadOnlyList<string> mnemonicsInOrder,
        byte[] payloadAgeBytes,
        Bip32Fingerprint walletMasterFingerprint)
    {
        // Number of shares is needed both for the "share-i-of-N" labels and
        // for sizing the per-share list — captured once for clarity.
        var n = mnemonicsInOrder.Count;

        // Per-share fingerprint lines, rendered functionally via Select+Join.
        // Each line: "   share-{i}-of-{n}:  {8-hex-fingerprint}"
        var shareLines = string.Join(Environment.NewLine,
            mnemonicsInOrder.Select((m, i) => $"   share-{i + 1}-of-{n}:  {ShareFingerprint.Compute(m)}"));

        // Full SHA-256 of the payload.age ciphertext bytes — gives the owner
        // a way to confirm the ciphertext on disk hasn't been silently
        // corrupted (bitrot, bad USB, etc.) without needing to decrypt.
        var payloadHash = Convert.ToHexString(SHA256.HashData(payloadAgeBytes)).ToLowerInvariant();

        var sb = new StringBuilder();
        sb.AppendLine("SPS-SLIP39 Verification Record");
        sb.AppendLine("================================================================");
        sb.AppendLine($"Created:       {createdDate}");
        sb.AppendLine($"Tool version:  {toolVersion}");
        sb.AppendLine($"Label:         {label}");
        sb.AppendLine();
        // Bip32Fingerprint.ToString() returns 8 lowercase hex chars — exactly
        // the canonical display form used by Sparrow/Electrum/etc.
        sb.AppendLine($"Wallet master fingerprint (BIP-32):  {walletMasterFingerprint}");
        sb.AppendLine("   This is the fingerprint of the recovered wallet's master");
        sb.AppendLine("   public key — derivable from the seed but reveals NO secret");
        sb.AppendLine("   data. Knowing it does not help an attacker.");
        sb.AppendLine();
        sb.AppendLine("Per-share fingerprints (SHA256 of mnemonic words, truncated):");
        sb.AppendLine(shareLines);
        sb.AppendLine();
        sb.AppendLine("Payload integrity (SHA256 of payload.age):");
        sb.AppendLine($"   {payloadHash}");
        sb.AppendLine();
        sb.AppendLine("────────────────────────────────────────────────────────────────");
        sb.AppendLine("This record is non-secret. Store it where you can find it for");
        sb.AppendLine("dry-run verification. Suggested locations:");
        sb.AppendLine("  - Printed copy in your home safe");
        sb.AppendLine("  - Plain-text note inside the dedicated PM entry");
        sb.AppendLine("  - Separate text file on your encrypted USB");
        sb.AppendLine("DO NOT distribute to share-holders.");
        return sb.ToString();
    }
}
