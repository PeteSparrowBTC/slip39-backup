namespace Slip39Demo.Core.Bip32;

// 4-byte BIP-32 master fingerprint. Non-secret — derivable from the wallet's
// master public key, identifies which wallet was reconstructed. Immutable
// value object: construct via the From(byte[]) factory which enforces the
// 4-byte length invariant. ToString() renders as 8 lowercase hex characters,
// which is the canonical display form in wallet UIs (Sparrow, Electrum, etc.).
public sealed record Bip32Fingerprint
{
    public byte[] Bytes { get; }

    Bip32Fingerprint(byte[] bytes) =>
        Bytes = bytes;

    public static Bip32Fingerprint From(byte[] bytes) =>
        bytes.Length == 4
            ? new Bip32Fingerprint(bytes.ToArray())   // defensive copy
            : throw new ArgumentException($"Bip32Fingerprint must be 4 bytes (got {bytes.Length})", nameof(bytes));

    public override string ToString() =>
        Convert.ToHexString(Bytes).ToLowerInvariant();

    // Record equality on a byte[] property doesn't work out of the box (reference
    // equality), so override to do structural comparison.
    public bool Equals(Bip32Fingerprint? other) =>
        other is not null && Bytes.AsSpan().SequenceEqual(other.Bytes);

    public override int GetHashCode() =>
        BitConverter.ToInt32(Bytes, 0);
}
