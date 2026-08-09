using System.Text;
using CSharpFunctionalExtensions;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Security;

namespace Slip39Demo.Core.Pgp;

// The outer envelope: OpenPGP symmetric (passphrase) encryption with AES-256,
// wrapped around the age file.
//
// WHY THERE IS A SECOND LAYER AT ALL
// Nesting composes in a way that parallel copies do not. With gpg(age(payload)),
// an attacker must break BOTH formats to reach the wallet, so the secret
// survives if either holds. Concretely it lifts the post-quantum margin from
// 2^64 to 2^128: age's file key is 128 bits, which Grover halves to 64, while
// AES-256 here restores the headroom. That is the one structural weakness we
// identified in age v1, and this is the mechanism that addresses it.
//
// The cost is a second thing that must still work at recovery time, which is why
// payload.age ships alongside payload.age.gpg rather than being replaced by it.
// An heir who manages only the age step still recovers the wallet.
//
// WHY BOUNCYCASTLE AND NOT ONLY THE gpg BINARY
// Three reasons, all about not having a single point of failure:
//   - Recoverer must unwrap a .gpg on whatever machine the heir has, and
//     requiring GnuPG to be installed and driveable there would be a new way for
//     recovery to fail. Decryption in-process is always available.
//   - The Blazor WASM build cannot start a subprocess at all.
//   - It gives us a second implementation to test against the first. The
//     interop tests encrypt with each and decrypt with the other, the same
//     discipline applied to the age layer.
// BouncyCastle is already a dependency (Bip32MasterFingerprint uses it for
// secp256k1 and RIPEMD160), so this adds no new supply chain.
//
// KEY CONVENTION
// The same 64-character lowercase hex encoding of the 32-byte SLIP-39 master
// secret that AgePassphrase uses. Deliberately the same secret for both layers:
// reusing a full-entropy passphrase across two independent schemes, each with
// its own salt and KDF, is sound, and it keeps recovery to one secret typed
// twice rather than a derivation step no ordinary tool can perform.
public static class PgpEnvelope
{
    // gpg's own default for symmetric encryption is AES-256; matching it keeps
    // files produced by either side indistinguishable in structure.
    const SymmetricKeyAlgorithmTag Cipher = SymmetricKeyAlgorithmTag.Aes256;

    // SHA-256 for the string-to-key derivation. The S2K iteration count is
    // irrelevant to our security (the passphrase carries 256 bits of entropy, so
    // there is nothing to grind), but weak defaults would matter to anyone who
    // later reused this code with a human-chosen passphrase.
    const HashAlgorithmTag S2kHash = HashAlgorithmTag.Sha256;

    public static Result<byte[]> Encrypt(byte[] ageFile, byte[] key32) =>
        ValidateKey(key32).Bind(_ => Try(() =>
        {
            // Literal data packet first, which is what gpg produces and expects.
            // Compression is deliberately omitted: the input is already encrypted
            // and therefore incompressible, and a compression layer would only add
            // a decoder that could go wrong at recovery.
            using var literalBuffer = new MemoryStream();
            var literalGenerator = new PgpLiteralDataGenerator();
            using (var literalOut = literalGenerator.Open(
                       literalBuffer, PgpLiteralData.Binary, "payload.age",
                       ageFile.Length, DateTime.UnixEpoch))
            {
                literalOut.Write(ageFile, 0, ageFile.Length);
            }

            var encryptedGenerator = new PgpEncryptedDataGenerator(
                Cipher, withIntegrityPacket: true, new SecureRandom());
            encryptedGenerator.AddMethod(ToHexPassphrase(key32).ToCharArray(), S2kHash);

            var literal = literalBuffer.ToArray();
            using var output = new MemoryStream();
            using (var encryptedOut = encryptedGenerator.Open(output, literal.Length))
            {
                encryptedOut.Write(literal, 0, literal.Length);
            }

            return output.ToArray();
        }, "OpenPGP encrypt failed"));

    public static Result<byte[]> Decrypt(byte[] pgpFile, byte[] key32) =>
        ValidateKey(key32).Bind(_ => Try(() =>
        {
            var factory = new PgpObjectFactory(pgpFile);
            var first = factory.NextPgpObject();

            // gpg may emit a marker packet before the encrypted list; skip until
            // the thing we can actually work with turns up.
            while (first is not null and not PgpEncryptedDataList)
                first = factory.NextPgpObject();

            if (first is not PgpEncryptedDataList list)
                throw new PgpException("no OpenPGP encrypted data found in this file");

            var pbe = list.GetEncryptedDataObjects().OfType<PgpPbeEncryptedData>().FirstOrDefault()
                ?? throw new PgpException(
                    "this file is encrypted to a key, not to a passphrase, so this key cannot open it");

            using var clear = pbe.GetDataStream(ToHexPassphrase(key32).ToCharArray());

            // Unwrap whatever the producer nested inside: gpg compresses by
            // default, so a compressed packet is the common case even though we
            // never write one ourselves.
            var inner = new PgpObjectFactory(clear).NextPgpObject();
            if (inner is PgpCompressedData compressed)
                inner = new PgpObjectFactory(compressed.GetDataStream()).NextPgpObject();

            if (inner is not PgpLiteralData literal)
                throw new PgpException($"expected literal data inside the envelope, got {inner?.GetType().Name}");

            using var literalStream = literal.GetInputStream();
            using var result = new MemoryStream();
            literalStream.CopyTo(result);
            var bytes = result.ToArray();

            // The integrity packet is what distinguishes "decrypted" from
            // "decrypted and unmodified". Checking it is the whole point of
            // asking for one at encryption time.
            if (pbe.IsIntegrityProtected() && !pbe.Verify())
                throw new PgpException("integrity check failed: this file has been altered");

            return bytes;
        }, "OpenPGP decrypt failed"));

    static Result<byte[]> ValidateKey(byte[] key32) =>
        key32.Length == 32
            ? Result.Success(key32)
            : Result.Failure<byte[]>($"key must be 32 bytes (got {key32.Length})");

    static string ToHexPassphrase(byte[] key32) =>
        Convert.ToHexString(key32).ToLowerInvariant();

    static Result<byte[]> Try(Func<byte[]> action, string prefix)
    {
        try
        {
            return Result.Success(action());
        }
        catch (Exception ex)
        {
            return Result.Failure<byte[]>($"{prefix}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
