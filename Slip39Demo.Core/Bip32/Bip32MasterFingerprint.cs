using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Math;
using BcSecp = Org.BouncyCastle.Asn1.X9.X9ECParameters;

namespace Slip39Demo.Core.Bip32;

// Derives the BIP-32 master fingerprint from BIP-39 seed words + optional
// passphrase. The fingerprint is 4 bytes of HASH160 of the compressed master
// public key — non-secret, identifies which wallet was reconstructed.
//
// Used by the verification record so a dry-run recovery can confirm the right
// wallet was rebuilt without ever showing seed words on screen.
//
// Crypto chain (BIP-32):
//   1. bip39_seed = PBKDF2-HMAC-SHA512(mnemonic, "mnemonic"+passphrase, 2048, 64B)
//   2. I = HMAC-SHA512("Bitcoin seed", bip39_seed)  -> (priv, chain_code)
//   3. pubkey_compressed = secp256k1 * priv          (33 bytes)
//   4. fingerprint = HASH160(pubkey_compressed)[0..4]
//
// secp256k1 point multiplication + RIPEMD160 come from BouncyCastle (already
// pulled in via AgeSharp and pinned directly in Slip39Demo.Core.csproj).
public static class Bip32MasterFingerprint
{
    static readonly BcSecp Secp256k1 = SecNamedCurves.GetByName("secp256k1");

    public static Bip32Fingerprint Compute(string seedWords, string? passphrase)
    {
        var bip39Seed = Bip39Seed.Derive(seedWords, passphrase);

        // BIP-32 root derivation: HMAC-SHA512 with "Bitcoin seed" as the key
        // (NOT the salt — BIP-32 uses HMAC, not PBKDF2 here).
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes("Bitcoin seed"));
        var i = hmac.ComputeHash(bip39Seed);
        var masterPriv = i.AsSpan(0, 32).ToArray();
        // i[32..64] is the chain code — not needed for the fingerprint.

        // Compute master pubkey = secp256k1 * priv (compressed form, 33 bytes).
        var d = new BigInteger(1, masterPriv);
        var q = Secp256k1.G.Multiply(d).Normalize();
        var compressed = q.GetEncoded(compressed: true);

        // HASH160 = RIPEMD160(SHA256(compressed pubkey)); take first 4 bytes.
        return Bip32Fingerprint.From(Hash160(compressed).AsSpan(0, 4).ToArray());
    }

    // BIP-32's HASH160 = RIPEMD160(SHA256(data)).
    // SHA256 is native (.NET); RIPEMD160 is BouncyCastle.
    static byte[] Hash160(byte[] data)
    {
        var sha = SHA256.HashData(data);
        var rip = new RipeMD160Digest();
        rip.BlockUpdate(sha, 0, sha.Length);
        var output = new byte[20];
        rip.DoFinal(output, 0);
        return output;
    }
}
