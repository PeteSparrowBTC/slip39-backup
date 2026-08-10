using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using CSharpFunctionalExtensions;

namespace Slip39Demo.Core.Bip39;

// The BIP-39 English wordlist, embedded in this assembly and verified against its
// published SHA-256 the first time it is loaded in a process.
//
// WHY IT IS HERE AT ALL
// Nothing else in this repository can supply it. BouncyCastle has no BIP-39, and
// Xecrets.Slip39 carries the SLIP-39 wordlist, which is a different list of 1024
// words. The backup-key collision check needs mnemonic-to-entropy, so the list
// comes in as data, copied from the sibling dice-to-seed tool, which took it from
// bitcoin/bips. The list is fixed by the specification and never changes.
//
// WHY THE HASH IS CHECKED AT RUNTIME
// This is the only input to the derivation path that was not written in this
// repository. One altered word maps an index to a different word, so a mnemonic
// would still read as ordinary English while standing for different entropy, and
// nothing on screen would look wrong. The check costs one hash of 13 KB and
// removes that whole class of failure.
//
// The expected hash covers the file exactly as published: 2048 lines, LF endings,
// one trailing newline. .gitattributes pins this path to eol=lf so a Windows
// checkout cannot alter it; without that pin every clone on Windows would fail the
// check for a reason that has nothing to do with the contents.
public sealed class Bip39WordList
{
    public const string ExpectedSha256Hex = "2f5eed53a4727b4bf8880d8f3f199efc90e58503646d9ff8eff3a2ed3b24dbda";

    public const int ExpectedWordCount = 2048;

    const string ResourceName = "Slip39Demo.Core.Bip39.WordList.english.txt";

    // Word to index, built once per load. The 11-bit index is what mnemonic-to-entropy
    // needs, and a linear scan of 2048 words per word of a 24-word mnemonic is wasteful
    // for something the UI recomputes on every keystroke.
    readonly IReadOnlyDictionary<string, int> indexByWord;

    Bip39WordList(IReadOnlyList<string> words, string sha256Hex)
    {
        Words = words;
        Sha256Hex = sha256Hex;
        indexByWord = words
            .Select((word, index) => (word, index))
            .ToDictionary(pair => pair.word, pair => pair.index, StringComparer.Ordinal);
    }

    public IReadOnlyList<string> Words { get; }

    // The hash of the embedded bytes, so a caller can show it rather than assert it.
    public string Sha256Hex { get; }

    // Loaded and verified once per process, then reused.
    //
    // WHY CACHING IS SOUND HERE
    // The bytes are a manifest resource of an assembly that is already loaded, so they
    // cannot change while the process runs: hashing them again would re-answer a question
    // whose answer cannot have moved. What it would cost is real, because the callers are
    // interactive. ScanForSeedCollision loads the list, and the Owner page's live state
    // calls it on every render, so without this every keystroke-driven re-render hashed
    // 13 KB and rebuilt a 2048-entry dictionary, next to the PBKDF2 cost that already
    // decided that page's fields bind on change rather than on input.
    //
    // Lazy<T> is used for its default thread-safe mode: two callers racing on the first
    // load get one verification and the same instance.
    static readonly Lazy<Result<Bip39WordList>> Verified = new(LoadAndVerify);

    // A failure here means nothing may be derived, so it comes back as a Result for the
    // caller to render rather than as an exception: a corrupted resource is a state the
    // user needs explained, not a stack trace.
    public static Result<Bip39WordList> Load() => Verified.Value;

    static Result<Bip39WordList> LoadAndVerify()
    {
        var bytes = ReadEmbeddedBytes();

        if (bytes is null)
            return Result.Failure<Bip39WordList>(
                $"The embedded BIP-39 wordlist resource '{ResourceName}' is missing from this build. "
                + "Nothing can be derived.");

        var sha256Hex = Convert.ToHexStringLower(SHA256.HashData(bytes));

        if (sha256Hex != ExpectedSha256Hex)
            return Result.Failure<Bip39WordList>(
                "The embedded BIP-39 wordlist does not match the published English list. Expected "
                + $"SHA-256 {ExpectedSha256Hex}, found {sha256Hex}. Nothing can be derived.");

        // Split on both endings so a mangled checkout reports the word count rather than
        // 2048 words with a stray carriage return welded onto each. The hash check above
        // has already rejected that case; this keeps the failure legible if the expected
        // hash is ever updated without the surrounding reasoning.
        var words = Encoding.UTF8.GetString(bytes)
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .ToArray();

        return words.Length == ExpectedWordCount
            ? new Bip39WordList(words, sha256Hex)
            : Result.Failure<Bip39WordList>(
                $"The embedded BIP-39 wordlist holds {words.Length} words, not {ExpectedWordCount}. "
                + "Nothing can be derived.");
    }

    // The zero-based index of a word, or null when the word is not on the list. Exact
    // match only: the four-letter-prefix convention some tools accept is deliberately
    // not honoured here, because this type is used to decide whether a pasted key
    // collides with a seed, and guessing which word was meant is not a decision a
    // security check should make.
    public int? IndexOf(string word) =>
        indexByWord.TryGetValue(word, out var index) ? index : null;

    static byte[]? ReadEmbeddedBytes()
    {
        using var stream = typeof(Bip39WordList).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName);

        if (stream is null)
            return null;

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return buffer.ToArray();
    }
}
