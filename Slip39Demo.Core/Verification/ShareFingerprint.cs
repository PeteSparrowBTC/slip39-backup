using System.Security.Cryptography;
using System.Text;

namespace Slip39Demo.Core.Verification;

// Truncated SHA-256 of a SLIP-39 mnemonic — used as a non-secret identifier
// for a specific share. 8 lowercase hex characters (32 bits). Two different
// mnemonics produce different fingerprints with overwhelming probability;
// the fingerprint reveals zero bits of the underlying share secret. Used in
// the verification record (and per-share quick-verify UI in Phase 2/v2.1).
public static class ShareFingerprint
{
    public static string Compute(string mnemonic) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(mnemonic.Trim())))
            .ToLowerInvariant()[..8];
}
