using System.Security.Cryptography;
using System.Text;

namespace Slip39Demo.Core.Bip32;

// BIP-39 mnemonic-to-seed: PBKDF2-HMAC-SHA512 over the NFKD-normalised mnemonic,
// salted with "mnemonic" + (passphrase ?? ""), 2048 iterations, 64-byte output.
// This is the standard BIP-39 derivation — no Bitcoin-Core deviation, identical
// to what every wallet does.
public static class Bip39Seed
{
    public static byte[] Derive(string mnemonicWords, string? passphrase)
    {
        var mnemonicBytes = Encoding.UTF8.GetBytes(mnemonicWords.Normalize(NormalizationForm.FormKD));
        var saltBytes = Encoding.UTF8.GetBytes(("mnemonic" + (passphrase ?? "")).Normalize(NormalizationForm.FormKD));

        return Rfc2898DeriveBytes.Pbkdf2(
            password: mnemonicBytes,
            salt: saltBytes,
            iterations: 2048,
            hashAlgorithm: HashAlgorithmName.SHA512,
            outputLength: 64);
    }
}
