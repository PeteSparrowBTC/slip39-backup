namespace Slip39Demo.Core.Payload;

// Schema-versioned canonical payload describing a wallet setup that will be
// SLIP39-split. Immutable by construction (positional record). Cosigners is
// exposed as IReadOnlyList<Cosigner> so callers cannot mutate the membership
// after a payload is constructed.
public sealed record PayloadV1_1(
    string SchemaVersion,
    string Created,
    string? Label,
    string? TopLevelSeedWords,
    IReadOnlyList<Cosigner> Cosigners,
    string? Descriptor,
    string Threshold,
    bool Slip39Extendable,
    string? Notes);

// One cosigner entry. For shared-seed setups the SeedWords field is null
// (the seed is at the top level). For multivendor setups each cosigner
// supplies its own SeedWords and TopLevelSeedWords on the parent payload is null.
public sealed record Cosigner(
    string Id,
    string WalletType,
    string? Passphrase,
    string DerivationPath,
    string? SeedWords,
    string? XpubFingerprint);
