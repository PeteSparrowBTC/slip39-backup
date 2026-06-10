using System.Security.Cryptography;
using FluentAssertions;
using Slip39Demo.Core.Age;
using Slip39Demo.Core.Bip32;
using Slip39Demo.Core.Payload;
using Slip39Demo.Core.Slip39;
using Slip39Demo.Core.Verification;
using Xunit;

namespace Slip39Demo.Tests.Integration;

// End-to-end exercise of the whole Slip39Demo.Core chain — no test doubles,
// no mocks. Each test acts as both Owner (compose payload → encrypt → split)
// and Recoverer (combine threshold-many → decrypt → parse → assert equality
// with the original PayloadV1_1 record).
public class BackupRecoveryRoundTripTests
{
    [Fact]
    public void FullBackup_3of5_SingleGroup_RoundTrips()
    {
        // ── Owner side ────────────────────────────────────────
        var original = new PayloadV1_1(
            SchemaVersion: "1.1",
            Created: "2026-05-21T14:32:00Z",
            Label: "Main wallet",
            TopLevelSeedWords: "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            Cosigners: [new Cosigner("main", "bip39", "TREZOR", "m/84'/0'/0'", null, null)],
            Descriptor: null,
            Threshold: "3-of-5",
            Slip39Extendable: true,
            Notes: null);

        var payloadText = PayloadEmitter.Emit(original);
        var payloadBytes = System.Text.Encoding.UTF8.GetBytes(payloadText);

        // Random 32-byte master key K per the redesign — NOT the proprietary
        // [entropy][passphrase][padding] convention.
        var k = RandomNumberGenerator.GetBytes(32);

        var ciphertext = AgePassphrase.Encrypt(payloadBytes, k).Value;

        var cfg = new GroupConfig(1, [new ShareGroup("only", 3, 5)], true);
        var mnemonics = Slip39Wrapping.SplitKey(k, cfg).Value;
        mnemonics.Should().HaveCount(5);

        // Verification record uses the computed fingerprint directly so the
        // test stays self-consistent (Task 7's known-answer test already
        // pins the specific value '73c5da0a' for the no-passphrase case).
        var masterFp = Bip32MasterFingerprint.Compute(
            original.TopLevelSeedWords!,
            original.Cosigners[0].Passphrase);
        var rec = VerificationRecord.Build("2026-05-21", "2.0.0", original.Label!, mnemonics, ciphertext, masterFp);
        rec.Should().Contain(masterFp.ToString());

        // ── Recoverer side ────────────────────────────────────
        // Pick any threshold-many mnemonics; the wrapper is order-agnostic.
        var anyThreeMnemonics = mnemonics.Take(3).ToList();
        var recoveredK = Slip39Wrapping.CombineMnemonics(anyThreeMnemonics).Value;
        recoveredK.Should().Equal(k);

        var decryptedBytes = AgePassphrase.Decrypt(ciphertext, recoveredK).Value;
        var decryptedText = System.Text.Encoding.UTF8.GetString(decryptedBytes);
        decryptedText.Should().Be(payloadText);

        var reparsed = PayloadParser.Parse(decryptedText);
        reparsed.IsSuccess.Should().BeTrue();
        reparsed.Value.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void FullBackup_MultiGroup_RoundTrips()
    {
        var original = new PayloadV1_1(
            "1.1", "2026-05-21T14:32:00Z", "Multi-group wallet",
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            [new Cosigner("main", "bip39", null, "m/84'/0'/0'", null, null)],
            null,
            "Group threshold 2 of 3",
            Slip39Extendable: true,
            Notes: null);

        var payloadBytes = System.Text.Encoding.UTF8.GetBytes(PayloadEmitter.Emit(original));
        var k = RandomNumberGenerator.GetBytes(32);
        var ciphertext = AgePassphrase.Encrypt(payloadBytes, k).Value;

        var cfg = new GroupConfig(
            GroupThreshold: 2,
            Groups: [
                new ShareGroup("personal-1", 1, 1),
                new ShareGroup("friends",    3, 5),
                new ShareGroup("family",     2, 6)],
            Extendable: true);

        var mnemonics = Slip39Wrapping.SplitKey(k, cfg).Value;
        mnemonics.Should().HaveCount(1 + 5 + 6);

        // Satisfy group-threshold=2: personal-1 (idx 0, fully satisfied at 1-of-1)
        // + 3 friends (idx 1..3, satisfying 3-of-5 of friends group).
        var selected = new[] { mnemonics[0], mnemonics[1], mnemonics[2], mnemonics[3] };
        var recoveredK = Slip39Wrapping.CombineMnemonics(selected).Value;
        recoveredK.Should().Equal(k);

        var plain = AgePassphrase.Decrypt(ciphertext, recoveredK).Value;
        PayloadParser.Parse(System.Text.Encoding.UTF8.GetString(plain)).Value
            .Should().BeEquivalentTo(original);
    }
}
