namespace Slip39Demo.Core.Slip39;

// Top-level configuration for a SLIP-39 split. SLIP-39 supports multi-group
// thresholds (e.g. "any 2 of 4 groups must contribute, where Friends is 3-of-5,
// Family is 2-of-6, etc."). For the common single-group case, pass GroupThreshold=1
// and a single ShareGroup. Immutable by construction — IReadOnlyList<ShareGroup>
// enforces that callers cannot mutate the group set after construction.
public sealed record GroupConfig(
    int GroupThreshold,
    IReadOnlyList<ShareGroup> Groups,
    bool Extendable);

// One named group inside a SLIP-39 split. Threshold = M, Count = N (M-of-N).
// Name is for display only; it does not affect the cryptography. SLIP-39 itself
// does not store group names — they are tracked by this tool for the README,
// verification record, and UI.
public sealed record ShareGroup(
    string Name,
    int Threshold,
    int Count);
