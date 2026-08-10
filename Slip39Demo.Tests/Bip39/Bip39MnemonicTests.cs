using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Slip39Demo.Core.Bip39;
using Xunit;

namespace Slip39Demo.Tests.Bip39;

// Words back to entropy, checked against published vectors rather than against this
// repository's own output. An off-by-one in the 11-bit split is invisible from the
// inside: it still yields 16 or 32 plausible bytes, and only a vector somebody else
// published catches it.
public class Bip39MnemonicTests
{
    static Bip39WordList WordList => Bip39WordList.Load().Value;

    static string EntropyHexOf(string mnemonic) =>
        Convert.ToHexStringLower(Bip39Mnemonic.ToEntropy(mnemonic, WordList).Value);

    // The two the brief names explicitly, spelled out so this file can be read against
    // the brief without opening the fixture, plus one non-trivial case. All three are in
    // the published English set that runs in full below.
    [Theory]
    [InlineData(
        "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
        "00000000000000000000000000000000")]
    [InlineData(
        "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon "
        + "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon art",
        "0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData(
        "ozone drill grab fiber curtain grace pudding thank cruise elder eight picnic",
        "9e885d952ad362caeb4efe34a8e91bd2")]
    [InlineData(
        "void come effort suffer camp survey warrior heavy shoot primary clutch crush "
        + "open amazing screen patrol group space point ten exist slush involve unfold",
        "f585c11aec520db57dd353c69554b21a89b20fb0650966fa0a9d6f74fd989d8f")]
    public void The_official_vectors_named_in_the_brief(string mnemonic, string expectedEntropyHex) =>
        EntropyHexOf(mnemonic).Should().Be(expectedEntropyHex);

    [Fact]
    public void Every_published_english_vector_maps_back_to_its_entropy()
    {
        var failures = PublishedEnglishVectors()
            .Where(vector => EntropyHexOf(vector.Mnemonic) != vector.EntropyHex)
            .Select(vector =>
                $"\"{vector.Mnemonic}\": expected {vector.EntropyHex}, got {EntropyHexOf(vector.Mnemonic)}")
            .ToArray();

        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void The_published_vector_set_is_the_file_this_repository_vendored() =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(VectorsPath)))
            .Should().Be("fa3b937b7cff9c9b8ecd3aa011faeb8d6dd67993174b72326e83f4de8fdb30f8");

    // The published English set covers 12, 18 and 24 words and nothing else, so it cannot
    // speak for the two lengths in between. These six were produced with the Trezor
    // reference implementation (the python-mnemonic package, the same code that generated
    // the published vectors) and pinned here, because a value this repository produced
    // itself would only prove the code agrees with itself.
    //
    // Provenance, reproducible with python-mnemonic installed:
    //   python -c "from mnemonic import Mnemonic; print(Mnemonic('english').to_mnemonic(bytes.fromhex('<hex>')))"
    [Theory]
    [InlineData(
        "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon "
        + "abandon abandon address",
        "0000000000000000000000000000000000000000")]
    [InlineData(
        "legal winner thank year wave sausage worth useful legal winner thank year wave sausage wise",
        "7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f")]
    [InlineData(
        "diet glad hat rural panther lawsuit act drop gallery urge where firm impulse search settle",
        "3dac51a65ec9fcfc409a1b5f1defe92ba7238431")]
    [InlineData(
        "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon "
        + "abandon abandon abandon abandon abandon abandon abandon abandon admit",
        "00000000000000000000000000000000000000000000000000000000")]
    [InlineData(
        "legal winner thank year wave sausage worth useful legal winner thank year wave sausage worth "
        + "useful legal winner thank year viable",
        "7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f")]
    [InlineData(
        "diet glad hat rural panther lawsuit act drop gallery urge where firm impulse search settle "
        + "bubble extra slide hello miracle crucial",
        "3dac51a65ec9fcfc409a1b5f1defe92ba723843118ea511971ab46b3")]
    public void The_fifteen_and_twenty_one_word_lengths_the_published_set_omits(
        string mnemonic, string expectedEntropyHex) =>
        EntropyHexOf(mnemonic).Should().Be(expectedEntropyHex);

    // All five standard lengths produce their standard entropy size. The collision rule in
    // BackupKeyEntry compares k's first entropy.Length bytes, so every one of these five
    // has to come back at the right length for that single rule to cover them all.
    [Theory]
    [InlineData(12, 16)]
    [InlineData(15, 20)]
    [InlineData(18, 24)]
    [InlineData(21, 28)]
    [InlineData(24, 32)]
    public void Each_standard_length_yields_its_standard_entropy_size(int words, int entropyBytes)
    {
        var mnemonic = AllZeroMnemonicOf(words);

        var entropy = Bip39Mnemonic.ToEntropy(mnemonic, WordList);

        entropy.IsSuccess.Should().BeTrue(entropy.IsFailure ? entropy.Error : "");
        entropy.Value.Should().HaveCount(entropyBytes);
        entropy.Value.Should().AllSatisfy(b => b.Should().Be(0));
    }

    [Fact]
    public void Supported_word_counts_are_the_five_standard_ones() =>
        Bip39Mnemonic.SupportedWordCounts.Should().Equal(12, 15, 18, 21, 24);

    // The checksum is the whole reason this is safe to compare bytes with. Swapping the
    // last word of the all-zeros 12-word mnemonic for another listed word leaves twelve
    // real BIP-39 words that no wallet would accept, and an implementation that dropped
    // the checksum bits would happily return sixteen bytes of something.
    [Theory]
    [InlineData("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon")]
    [InlineData("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon ability")]
    [InlineData("about abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon")]
    public void A_mnemonic_that_fails_its_checksum_is_rejected(string mnemonic)
    {
        var entropy = Bip39Mnemonic.ToEntropy(mnemonic, WordList);

        entropy.IsFailure.Should().BeTrue();
        entropy.Error.Should().Contain("checksum");
    }

    [Fact]
    public void A_word_that_is_not_on_the_list_is_rejected_and_named()
    {
        var entropy = Bip39Mnemonic.ToEntropy(
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon zzzz",
            WordList);

        entropy.IsFailure.Should().BeTrue();
        entropy.Error.Should().Contain("zzzz");
    }

    [Theory]
    [InlineData("")]
    [InlineData("abandon")]
    [InlineData("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon")]
    [InlineData("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about about")]
    public void A_non_standard_word_count_is_rejected(string mnemonic)
    {
        var entropy = Bip39Mnemonic.ToEntropy(mnemonic, WordList);

        entropy.IsFailure.Should().BeTrue();
        entropy.Error.Should().Contain("12, 15, 18, 21 or 24");
    }

    // Words arrive pasted out of documents, in mixed case, with runs of whitespace and
    // line breaks. All of those are the same mnemonic and must read as the same entropy,
    // otherwise the collision check could be sidestepped by formatting alone.
    [Theory]
    [InlineData("  abandon\tabandon\nabandon abandon abandon abandon abandon abandon abandon abandon abandon   about  ")]
    [InlineData("ABANDON abandon Abandon abandon abandon abandon abandon abandon abandon abandon abandon ABOUT")]
    public void Case_and_whitespace_do_not_change_the_entropy(string mnemonic) =>
        EntropyHexOf(mnemonic).Should().Be("00000000000000000000000000000000");

    // The all-zeros mnemonic for a given length: "abandon" repeated, with the published
    // final word that carries the checksum. Pinned per length, not computed, for the same
    // reason as the vectors above.
    static string AllZeroMnemonicOf(int words)
    {
        var last = words switch
        {
            12 => "about",
            15 => "address",
            18 => "agent",
            21 => "admit",
            24 => "art",
            _ => throw new ArgumentOutOfRangeException(nameof(words)),
        };

        return string.Join(' ', Enumerable.Repeat("abandon", words - 1).Append(last));
    }

    static string VectorsPath =>
        Path.Combine(AppContext.BaseDirectory, "Bip39", "Vectors", "bip39-english-vectors.json");

    sealed record Vector(string EntropyHex, string Mnemonic);

    // The fixture holds every language the reference implementation publishes; only the
    // English rows are read, because that is the only list this build embeds. Each row is
    // [entropy hex, mnemonic, seed hex, xprv].
    static IReadOnlyList<Vector> PublishedEnglishVectors() =>
        JsonDocument.Parse(File.ReadAllBytes(VectorsPath))
            .RootElement.GetProperty("english")
            .EnumerateArray()
            .Select(row => new Vector(row[0].GetString()!, row[1].GetString()!))
            .ToList();
}
