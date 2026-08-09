using System.Text;
using FluentAssertions;
using Slip39Demo.Core.Age;
using Xunit;

namespace Slip39Demo.Tests.Age;

// Guards the class of implementation bug that verification CANNOT catch.
//
// Every other check in this repository proves a file decrypts: the runtime gate
// in Owner mode, the CCTV vectors, the foreign-backup fixtures, the owner's own
// run of the age CLI. None of them would notice if the ciphertext were
// catastrophically weak, because a file encrypted with a reused nonce, a fixed
// salt or a constant key still decrypts perfectly. Correctness testing is blind
// here by construction.
//
// The specific failures this rules out, all of which have appeared in real
// AEAD implementations:
//
//   - a reused ChaCha20 payload nonce, which under the same key exposes the XOR
//     of two plaintexts and collapses confidentiality outright
//   - a fixed or missing scrypt salt, which would let one precomputation attack
//     every file this tool has ever produced
//   - deterministic encryption generally, which leaks that two backups carry the
//     same wallet
//
// AgeSharp ships as a preview. The library is large, but this tool touches a
// narrow slice of it on the encryption path (scrypt mode, one recipient, a
// sub-kilobyte payload in a single STREAM chunk), and these assertions cover the
// parts of that slice which are observable from outside the library.
//
// What they cannot cover: the 128-bit file key itself, which is wrapped and so
// never visible in the output. A constant file key would still produce differing
// ciphertexts here, because the nonce varies and the payload key is derived from
// both. That one rests on reading AgeEncrypt.BuildHeaderAndFileKey, where it is
// a single RandomNumberGenerator.Fill call, and on the same OS CSPRNG the rest
// of the design already depends on.
public class AgeNonceReuseTests
{
    static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
    static readonly byte[] Plaintext = Encoding.UTF8.GetBytes(
        "schema_version: 1.1\nseed_words: abandon abandon abandon about\n");

    const int Samples = 64;

    // Each encryption runs scrypt at work factor 18, so the sample set costs real
    // seconds. Compute it once and share it across the assertions rather than
    // paying for it four times: the same files answer all four questions.
    static readonly Lazy<IReadOnlyList<byte[]>> Files = new(() =>
        Enumerable.Range(0, Samples)
            .Select(_ => AgePassphrase.Encrypt(Plaintext, Key))
            .Select(r => { r.IsSuccess.Should().BeTrue(); return r.Value; })
            .ToList());

    static IReadOnlyList<byte[]> EncryptMany() => Files.Value;

    [Fact]
    public void EncryptingTheSamePayloadTwice_NeverProducesTheSameBytes()
    {
        var files = EncryptMany();

        files.Select(Convert.ToBase64String).Distinct()
            .Should().HaveCount(Samples, "age encryption must be randomised, not deterministic");
    }

    // The scrypt salt is 16 bytes, base64 without padding (22 chars), carried as
    // the first argument of the "-> scrypt <salt> <workFactor>" stanza line. A
    // repeat across files would mean one scrypt precomputation unlocks many.
    [Fact]
    public void EveryFile_CarriesAFreshScryptSalt()
    {
        var salts = EncryptMany().Select(ScryptSaltOf).ToList();

        salts.Should().OnlyContain(s => s.Length > 0, "every file must carry a scrypt stanza");
        salts.Distinct().Should().HaveCount(Samples, "a repeated salt breaks scrypt's whole purpose");
    }

    // The 16-byte payload nonce follows the header, immediately after the line
    // holding the header HMAC ("--- <mac>"). Reuse under one key is the classic
    // catastrophic ChaCha20 failure.
    [Fact]
    public void EveryFile_CarriesAFreshPayloadNonce()
    {
        var nonces = EncryptMany().Select(PayloadNonceOf).Select(Convert.ToBase64String).ToList();

        nonces.Distinct().Should().HaveCount(Samples, "a reused nonce collapses ChaCha20 confidentiality");
    }

    // Belt and braces: the plaintext must not survive anywhere in the output. A
    // framing bug that wrote the payload alongside the ciphertext would pass
    // every decryption test ever written.
    [Fact]
    public void Ciphertext_NeverContainsThePlaintext()
    {
        var needle = Encoding.UTF8.GetString(Plaintext);

        foreach (var file in EncryptMany())
            Encoding.Latin1.GetString(file).Should().NotContain(needle);
    }

    static string ScryptSaltOf(byte[] ageFile)
    {
        var header = Encoding.Latin1.GetString(ageFile, 0, Math.Min(ageFile.Length, 200));
        var line = header.Split('\n').FirstOrDefault(l => l.StartsWith("-> scrypt ", StringComparison.Ordinal));
        return line?.Split(' ') is { Length: >= 3 } parts ? parts[2] : "";
    }

    static byte[] PayloadNonceOf(byte[] ageFile)
    {
        // Find the end of the "--- <hmac>" line; the 16 nonce bytes follow it.
        var text = Encoding.Latin1.GetString(ageFile);
        var macLine = text.IndexOf("\n--- ", StringComparison.Ordinal);
        macLine.Should().BeGreaterThan(0, "the header must end with its HMAC line");

        var afterMac = text.IndexOf('\n', macLine + 1);
        afterMac.Should().BeGreaterThan(0);

        return ageFile.Skip(afterMac + 1).Take(16).ToArray();
    }
}
