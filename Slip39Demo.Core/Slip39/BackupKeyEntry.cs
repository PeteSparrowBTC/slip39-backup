using System.Security.Cryptography;
using System.Text;
using CSharpFunctionalExtensions;
using Slip39Demo.Core.Bip39;

namespace Slip39Demo.Core.Slip39;

// Where k comes from. k is the 32-byte SLIP-39 master secret: it is split into the
// shares, and its lowercase hex is the age passphrase.
public enum BackupKeySource
{
    // RandomNumberGenerator.GetBytes(32), which is what this tool has always done and
    // remains the behaviour when the user pastes nothing.
    Generated,

    // 64 hex characters the user transcribed from somewhere they can account for,
    // typically the sibling dice-to-seed tool.
    Pasted,
}

// The resolved key plus where it came from. The source is display and record only: the
// result panel names it, because which key wrapped a backup is a fact the owner should
// be able to read afterwards. The key itself never reaches the transcript or the panel.
public sealed record BackupKeyChoice(byte[] Key, BackupKeySource Source);

// What the collision scan covered: the number of distinct mnemonics whose entropy was
// recovered and compared against k. There is no count of what could not be read, because
// a seed that cannot be read is a refusal (see ScanForSeedCollision), so on success this
// number is every non-blank seed the form held.
public sealed record SeedCollisionScan(int Compared);

// Accepting a backup key the user pasted instead of generating one.
//
// THE CONTRACT, which belongs to dice-to-seed and is restated here so this file can be
// read against that one (see DiceToSeed.Core/BackupKey.cs):
//
//   k     = SHA-256(the roll digits, joined by nothing), all 32 bytes, no truncation
//   shown = 64 lowercase hex characters
//   check = SHA-256(that lowercase hex STRING, as UTF-8) hex-encoded, first 4 characters
//
// The check code is computed over the printed string, not over the key bytes, so anyone
// can reproduce it from what is on screen with no hex decoding step:
//
//   printf '%s' "$K_HEX" | sha256sum | cut -c1-4
//
// It is a typo detector, sixteen bits of it, and nothing more. It catches a slip made
// while transcribing 64 characters by hand. It protects nothing against somebody who can
// change the key, and it is not treated as if it did.
//
// FAIL CLOSED, THE HOUSE RULE
// IPayloadEncryptor states it for encryption and it holds here for the same reason: there
// is deliberately NO fallback from a supplied key to the generator. If a key is supplied
// and is unusable for any reason, generation fails and says which reason. Quietly using a
// different key than the one the user asked for is exactly the invisible failure this
// codebase refuses elsewhere; the user would not find out until recovery, when the shares
// no longer open the backup they were told they open.
//
// THE COLLISION RULE, which is the part that matters
// dice-to-seed's seed mode derives a mnemonic's BIP-39 entropy with the same SHA-256 over
// the same roll log. So for a 24-word seed the entropy IS k byte for byte, and for a
// 12-word seed it is k's first 16 bytes. If somebody rolls one log and uses it for both
// their seed phrase and their backup key, k becomes derivable from the wallet k is
// supposed to protect, and the threshold scheme stops protecting anything. dice-to-seed
// clears its roll log when the mode changes, which handles one session, and then hands
// the rest to the tool that consumes k. That is this file: k is compared against the
// BIP-39 entropy of the seed words in the form, and generation is REFUSED on a match.
// Refusing is right and a warning would not be, because the resulting backup is worthless
// and nothing about it looks wrong.
//
// A seed the tool cannot read as BIP-39 is refused for the same reason, and the first
// version of this file got that wrong. It warned instead, on the argument that refusing
// "would change what generation accepts for every user who pastes no key". It would not:
// this scan is reached only behind the IsSupplied guard, so a refusal here can only ever
// reach somebody who pasted a key, and that population was never protected by the warning.
// What the warning did instead was ship the exact break above. Change one word of a
// 12-word seed for another listed word and the checksum fails, the seed becomes unreadable,
// the collision check silently covers nothing, and the tool hands out a backup whose key is
// the first 16 bytes of the wallet's own entropy. A typo in the seed field is how somebody
// reaches that, and the words look right on screen. So an unreadable seed stops generation
// and asks for the words to be corrected or cleared.
//
// AN HONESTY CONSTRAINT, also stated by dice-to-seed
// Dice for k do not remove trust in a random number generator, and nothing here may claim
// they do. age generates its own file key with this machine's RNG and the payload is
// encrypted under that; k only wraps it. What dice buy is a k whose origin can be
// accounted for and recomputed, which is worth having and is a different claim.
public static class BackupKeyEntry
{
    // The SLIP-39 master secret this tool splits: 32 bytes, so 64 hex characters.
    public const int KeyByteLength = 32;

    public const int KeyHexLength = KeyByteLength * 2;

    public const int CheckCodeLength = 4;

    // Every refusal wears the same prefix, so the reason is never mistaken for a note
    // about something that still went ahead.
    const string Refused = "Backup key REFUSED: ";

    // True when the user has typed into either field. A check code on its own counts as
    // supplied, deliberately: it means somebody is halfway through transcribing, and
    // silently generating a random key at that moment is the failure this class exists to
    // prevent.
    public static bool IsSupplied(string? keyHexInput, string? checkCodeInput) =>
        !string.IsNullOrWhiteSpace(keyHexInput) || !string.IsNullOrWhiteSpace(checkCodeInput);

    // Whitespace out, lowercase in. dice-to-seed prints k in groups of four characters
    // for transcription, so a paste or a retype legitimately arrives with spaces or line
    // breaks in it, and the canonical form (over which the check code is defined) is the
    // unbroken lowercase string. Lowercasing is part of the contract rather than a
    // convenience: the check code is the hash of the LOWERCASE hex string, so an
    // uppercase transcription of the same key must be folded before it is hashed.
    public static string NormalizeKeyHex(string? input) =>
        new string((input ?? "").Where(ch => !char.IsWhiteSpace(ch)).ToArray()).ToLowerInvariant();

    public static string NormalizeCheckCode(string? input) => NormalizeKeyHex(input);

    // SHA-256 of the printed hex string as UTF-8, hex-encoded, first four characters.
    // Takes the string and not the bytes on purpose: that is what makes it reproducible
    // from the screen with one shell command.
    public static string CheckCodeFor(string keyHex) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(keyHex)))[..CheckCodeLength];

    // The two typed fields to 32 bytes, or a refusal naming the reason. Format and check
    // code only; the seed collision is a separate pass because it depends on the rest of
    // the form and the UI reports the two states separately.
    public static Result<byte[]> Read(string? keyHexInput, string? checkCodeInput)
    {
        var keyHex = NormalizeKeyHex(keyHexInput);
        var checkCode = NormalizeCheckCode(checkCodeInput);

        if (keyHex.Length == 0)
            return Result.Failure<byte[]>(
                Refused + $"no key was entered. Paste the {KeyHexLength} hex characters of the "
                + "key, or clear both fields to let this tool generate one.");

        if (keyHex.Length != KeyHexLength)
            return Result.Failure<byte[]>(
                Refused + $"the key must be {KeyHexLength} hex characters ({KeyByteLength} bytes). "
                + $"{keyHex.Length} were read, ignoring spaces.");

        // Reported by position rather than by echoing the character: the position is
        // enough to find the slip, and nothing about the key needs repeating back.
        var badCharacterAt = keyHex.AsSpan().IndexOfAnyExcept(HexDigits);
        if (badCharacterAt >= 0)
            return Result.Failure<byte[]>(
                Refused + "the key must be hexadecimal, using only 0 to 9 and a to f. Character "
                + $"{badCharacterAt + 1} is not one of those.");

        if (checkCode.Length == 0)
            return Result.Failure<byte[]>(
                Refused + $"the {CheckCodeLength} character check code is required when a key is "
                + "supplied. It is the only thing that catches a mistyped key, and a mistyped key "
                + "produces a backup nobody can recover.");

        if (checkCode.Length != CheckCodeLength || checkCode.AsSpan().IndexOfAnyExcept(HexDigits) >= 0)
            return Result.Failure<byte[]>(
                Refused + $"the check code must be exactly {CheckCodeLength} hex characters, as "
                + "printed by the tool that produced the key.");

        // Not a constant-time comparison, and it should not pretend to be: this is a
        // transcription check over a value the user just typed, not a secret.
        if (checkCode != CheckCodeFor(keyHex))
            return Result.Failure<byte[]>(
                Refused + "the check code does not match the key, so one of the two was "
                + "transcribed wrong. Recompute it from the key on screen with: printf '%s' "
                + "\"$K_HEX\" | sha256sum | cut -c1-4. Nothing was generated.");

        return Convert.FromHexString(keyHex);
    }

    // Compares k against the BIP-39 entropy of every distinct mnemonic in the form, and
    // refuses on a match. One rule covers all five mnemonic lengths, because it compares
    // k's first entropy.Length bytes: 16 bytes for 12 words, 32 for 24, and the three
    // lengths in between without a special case each.
    public static Result<SeedCollisionScan> ScanForSeedCollision(
        byte[] key, IEnumerable<string?> seedWordsInForm)
    {
        var wordList = Bip39WordList.Load();

        // Fail closed: without the wordlist the collision rule cannot be applied, and a
        // key that has not been checked against the seed must not be used.
        if (wordList.IsFailure)
            return Result.Failure<SeedCollisionScan>(
                Refused + "it could not be checked against the seed words, because the BIP-39 "
                + $"wordlist in this build is unusable. {wordList.Error}");

        var mnemonics = seedWordsInForm
            .Where(seed => !string.IsNullOrWhiteSpace(seed))
            .Select(seed => string.Join(' ', Bip39Mnemonic.Split(seed)))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var entropies = mnemonics
            .Select(mnemonic => Bip39Mnemonic.ToEntropy(mnemonic, wordList.Value))
            .ToList();

        var collidingLength = entropies
            .Where(entropy => entropy.IsSuccess && SharesAPrefixWith(key, entropy.Value))
            .Select(entropy => entropy.Value.Length)
            .FirstOrDefault(0);

        // Reported before the unreadable case below, because it is the more specific finding:
        // if one seed collides and another is unreadable, the collision is what the user needs
        // to hear first.
        if (collidingLength > 0)
            return Result.Failure<SeedCollisionScan>(
                Refused + $"its first {collidingLength} bytes are exactly the {collidingLength} "
                + "bytes of BIP-39 entropy behind seed words entered in this form. That happens "
                + "when one dice roll log is used for both the seed and the key, and it means the "
                + "key can be recomputed from the wallet it is meant to protect, so the shares "
                + "would protect nothing. Roll a fresh log for the key and paste that instead. "
                + "Nothing was generated.");

        // A seed that will not read as BIP-39 cannot be compared, and a collision check that
        // covered nothing looks exactly like one that passed. One changed word in a valid
        // mnemonic reaches this state, and the key it would let through is the wallet's own
        // entropy, so this refuses rather than notes.
        var unreadable = entropies.Count(entropy => entropy.IsFailure);
        if (unreadable > 0)
            return Result.Failure<SeedCollisionScan>(
                Refused + $"{unreadable} of the seeds in this form "
                + (unreadable == 1 ? "does" : "do")
                + " not read as BIP-39 (a word not on the English list, a word count that is not "
                + "12, 15, 18, 21 or 24, or a checksum that does not match), so this key could "
                + "not be compared against "
                + (unreadable == 1 ? "it" : "them")
                + ". That comparison is the one thing standing between a reused dice roll log "
                + "and a backup key that can be recomputed from the wallet it protects, so it is "
                + "not skipped. Correct or clear those words, then paste the key again. Nothing "
                + "was generated.");

        return new SeedCollisionScan(Compared: entropies.Count);
    }

    // The single entry point the page uses. Nothing typed means the generator, exactly as
    // before. Anything typed must survive both passes or generation fails.
    public static Result<BackupKeyChoice> Resolve(
        string? keyHexInput, string? checkCodeInput, IEnumerable<string?> seedWordsInForm) =>
        IsSupplied(keyHexInput, checkCodeInput)
            ? Read(keyHexInput, checkCodeInput)
                .Bind(key => ScanForSeedCollision(key, seedWordsInForm)
                    .Map(_ => new BackupKeyChoice(key, BackupKeySource.Pasted)))
            : new BackupKeyChoice(RandomNumberGenerator.GetBytes(KeyByteLength), BackupKeySource.Generated);

    // k's leading bytes against a shorter or equal run of entropy. A 12-word seed's
    // entropy is 16 bytes and k is 32, so the comparison is over the prefix; for 24 words
    // the prefix is the whole key.
    static bool SharesAPrefixWith(byte[] key, byte[] entropy) =>
        entropy.Length <= key.Length && key.Take(entropy.Length).SequenceEqual(entropy);

    // Lowercase only, because NormalizeKeyHex has already folded the case. Uppercase here
    // would quietly accept an input the check code was never computed over.
    const string HexDigits = "0123456789abcdef";
}
