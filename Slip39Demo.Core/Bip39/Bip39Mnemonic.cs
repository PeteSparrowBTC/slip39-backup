using System.Security.Cryptography;
using System.Text;
using CSharpFunctionalExtensions;

namespace Slip39Demo.Core.Bip39;

// BIP-39 words back to the entropy they stand for, checksum verified.
//
// WHY THIS EXISTS
// Only one caller needs it: the backup-key collision check. dice-to-seed derives a
// 24-word seed's BIP-39 entropy with the same SHA-256 it uses for the backup key k,
// so a reused roll log makes k equal to the wallet's own entropy, and the shares
// then protect nothing. Catching that means turning the words in the form back into
// entropy and comparing bytes. See BackupKeyEntry for the rule.
//
// This is ordinary BIP-39 and nothing else: no PBKDF2, no seed, no BIP-32, no
// passphrase. Bip32.Bip39Seed does the PBKDF2 half and is unrelated to this file.
//
// The algorithm, run backwards:
//   each word is a zero-based index into the 2048-word list, 11 bits, most
//   significant first
//   the concatenated bits are entropy || checksum
//   checksum length is (entropy bits / 32), and its value is the top bits of
//   SHA-256(entropy)
//
// The checksum is VERIFIED rather than discarded. An unverified reverse mapping would
// happily turn a mistyped word into 32 bytes of something, and this code exists to
// decide whether two values are the same key: an answer derived from words that no
// wallet would accept is not an answer worth comparing.
public static class Bip39Mnemonic
{
    const int BitsPerWord = 11;

    // BIP-39 admits 128 to 256 bits of entropy in 32-bit steps, which is 12, 15, 18, 21
    // or 24 words. Anything else is rejected rather than handled: a length no other
    // implementation produces is a typo, not an input.
    static readonly IReadOnlyDictionary<int, int> EntropyBytesByWordCount = new Dictionary<int, int>
    {
        [12] = 16,
        [15] = 20,
        [18] = 24,
        [21] = 28,
        [24] = 32,
    };

    public static IReadOnlyList<int> SupportedWordCounts => EntropyBytesByWordCount.Keys.Order().ToList();

    // Splits on any run of whitespace after NFKD normalisation and lowercasing. The
    // English list is ASCII, so normalisation is a no-op on a clean mnemonic; it matters
    // for words pasted out of a document, where a non-breaking space or a full-width
    // character would otherwise fail lookup for an invisible reason.
    public static IReadOnlyList<string> Split(string? mnemonic) =>
        string.IsNullOrWhiteSpace(mnemonic)
            ? []
            : mnemonic.Normalize(NormalizationForm.FormKD)
                .ToLowerInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    public static Result<byte[]> ToEntropy(string? mnemonic, Bip39WordList wordList)
    {
        var words = Split(mnemonic);

        if (!EntropyBytesByWordCount.TryGetValue(words.Count, out var entropyByteCount))
            return Result.Failure<byte[]>(
                $"A BIP-39 mnemonic has 12, 15, 18, 21 or 24 words; {words.Count} given.");

        var indexed = words.Select(word => (Word: word, Index: wordList.IndexOf(word))).ToList();

        var unknown = indexed.Where(pair => pair.Index is null).Select(pair => pair.Word).Distinct().ToArray();
        if (unknown.Length > 0)
            return Result.Failure<byte[]>(
                "These words are not on the BIP-39 English list: "
                + string.Join(", ", unknown.Take(4))
                + (unknown.Length > 4 ? $" (and {unknown.Length - 4} more)." : "."));

        // entropy || checksum, one bool per bit, most significant bit of each index first.
        var bits = indexed
            .SelectMany(pair => Enumerable.Range(0, BitsPerWord)
                .Select(offset => ((pair.Index!.Value >> (BitsPerWord - 1 - offset)) & 1) == 1))
            .ToArray();

        var entropy = Enumerable.Range(0, entropyByteCount)
            .Select(index => PackByte(bits, index * 8))
            .ToArray();

        // The checksum is at most 8 bits for the lengths above, so it always sits inside
        // the first byte of SHA-256(entropy), counted from that byte's most significant bit.
        var checksumBitCount = words.Count / 3;
        var expected = SHA256.HashData(entropy)[0];

        var checksumMismatch = Enumerable.Range(0, checksumBitCount)
            .Any(offset => bits[entropyByteCount * 8 + offset] != (((expected >> (7 - offset)) & 1) == 1));

        return checksumMismatch
            ? Result.Failure<byte[]>(
                $"The {words.Count} words are all on the BIP-39 list but the checksum does not "
                + "match, so this is not a valid mnemonic. A word is probably wrong or out of order.")
            : entropy;
    }

    static byte PackByte(IReadOnlyList<bool> bits, int start) =>
        (byte)Enumerable.Range(0, 8).Aggregate(0, (acc, offset) => (acc << 1) | (bits[start + offset] ? 1 : 0));
}
