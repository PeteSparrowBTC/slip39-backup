using FluentAssertions;
using Slip39Demo.Core.Bip39;
using Xunit;

namespace Slip39Demo.Tests.Bip39;

// The wordlist is the only input to the collision check that was not written in this
// repository, so it is the one that gets verified at runtime. A single altered word maps
// an index to a different word, which would silently change the entropy a mnemonic stands
// for, and nothing on screen would look wrong.
//
// The expected hash is the SHA-256 of the BIP-39 English list as published in
// bitcoin/bips: 2048 lines, LF endings, one trailing newline. .gitattributes pins the
// file to eol=lf so a Windows checkout cannot change it.
public class Bip39WordListTests
{
    [Fact]
    public void The_embedded_list_loads() =>
        Bip39WordList.Load().IsSuccess.Should().BeTrue(Bip39WordList.Load().IsFailure
            ? Bip39WordList.Load().Error
            : "");

    [Fact]
    public void The_embedded_list_matches_the_published_sha256() =>
        Bip39WordList.Load().Value.Sha256Hex.Should()
            .Be("2f5eed53a4727b4bf8880d8f3f199efc90e58503646d9ff8eff3a2ed3b24dbda");

    // The literal above and the constant the code compares against must be the same
    // string. If one is updated and not the other, the check still passes and means
    // nothing.
    [Fact]
    public void The_expected_hash_constant_is_the_value_actually_checked() =>
        Bip39WordList.ExpectedSha256Hex.Should().Be(Bip39WordList.Load().Value.Sha256Hex);

    [Fact]
    public void The_embedded_list_holds_exactly_2048_words() =>
        Bip39WordList.Load().Value.Words.Should().HaveCount(2048);

    // Sorted and duplicate-free is what makes the 11-bit index unambiguous in both
    // directions, so it is asserted rather than assumed.
    [Fact]
    public void The_list_is_sorted_and_free_of_duplicates()
    {
        var words = Bip39WordList.Load().Value.Words;

        words.Should().BeInAscendingOrder(StringComparer.Ordinal);
        words.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(0, "abandon")]
    [InlineData(1, "ability")]
    [InlineData(2047, "zoo")]
    public void Known_indexes_hold_known_words(int index, string expected) =>
        Bip39WordList.Load().Value.Words[index].Should().Be(expected);

    [Theory]
    [InlineData("abandon", 0)]
    [InlineData("zoo", 2047)]
    public void Lookup_agrees_with_the_index(string word, int expected) =>
        Bip39WordList.Load().Value.IndexOf(word).Should().Be(expected);

    // Exact match only. The four-letter-prefix convention some tools accept is
    // deliberately not honoured: guessing which word was meant is not a decision a
    // check that guards key material should make.
    [Theory]
    [InlineData("aband")]
    [InlineData("aban")]
    [InlineData("Abandon")]
    [InlineData("notaword")]
    public void Lookup_rejects_anything_that_is_not_a_listed_word(string word) =>
        Bip39WordList.Load().Value.IndexOf(word).Should().BeNull();
}
