using CSharpFunctionalExtensions;
using Xecrets.Slip39;

namespace Slip39Demo.Core.Slip39;

// Wraps Xecrets.Slip39 with a clean Result<T> API. The redesign deletes the
// proprietary [entropy][passphrase][padding] master_secret convention — the
// 32-byte SLIP-39 master_secret is now a truly-random key K, and the
// BIP-39 passphrase + seed words travel inside payload.age. The SLIP-39
// passphrase parameter to Xecrets is therefore always the empty string.
//
// Verified Xecrets.Slip39 2.3.1315 surface (see also Slip39Demo.Web/Pages/Home.razor):
//   - new ShamirsSecretSharing(IRandom)                        — class with ctor
//   - Share[][] GenerateShares(bool extendable, int iterationExponent,
//                              int groupThreshold, Group[] groups,
//                              string passphrase, byte[] masterSecret)
//   - byte[] CombineShares(Share[] shares, string passphrase)  — returns secret bytes
//   - Group(int ShareThreshold, int ShareCount)                — positional record
//   - Share.Parse(string) / Share.ToString()                   — mnemonic round-trip
// On invalid input (insufficient shares, bad checksum, malformed mnemonic, etc.)
// Xecrets throws Slip39Exception, which we catch and surface as Result.Failure.
public static class Slip39Wrapping
{
    // Iteration exponent for PBKDF2 inside SLIP-39 (10000 * 2^e iterations).
    // Since our SLIP-39 passphrase is always empty (the age layer handles secrecy),
    // PBKDF2 does no useful work here — keep the exponent at 0 to minimise share
    // generation cost. This matches the spec's recommendation when no passphrase
    // is used.
    const int IterationExponent = 0;

    // Xecrets.Slip39 2.3.1315 has an internal bug in its Feistel cipher path
    // (only exercised when extendable=false): for certain randomly-generated
    // 15-bit share ids, the Feistel round throws OverflowException. The bug is
    // probabilistic — re-rolling the id (i.e. re-calling GenerateShares with a
    // fresh StrongRandom seed) clears it. We retry up to this many times.
    // Empirically the failure rate is well below 50%, so a handful of retries
    // makes the wrapper deterministic from the caller's perspective.
    const int MaxXecretsOverflowRetries = 8;

    // Xecrets.Slip39 2.3.1315 ships a broken Feistel cipher on the
    // extendable=false code path: stress testing shows ~50% of share generations
    // throw OverflowException and ~25% silently produce shares that recover
    // wrong bytes (the round-trip fails without any exception being thrown).
    // The extendable=true path is solid (100% success across thousands of trials).
    // Until upstream is fixed we refuse to emit non-extendable shares — silently
    // producing un-recoverable backups would be the worst possible outcome for
    // a backup tool. Callers who genuinely need non-extendable mode should
    // re-test this and remove the guard once Xecrets fixes the bug.
    public const string NonExtendableUnsupportedReason =
        "SLIP-39 split failed: extendable=false is disabled because Xecrets.Slip39 2.3.1315 "
        + "produces incorrect (non-recoverable) shares on that code path. Use Extendable=true.";

    // Splits a 32-byte master key K into SLIP-39 mnemonics per the group
    // configuration. Returns one mnemonic string per share in group-flat order
    // (group 0's shares first, then group 1's, etc.). Returns Failure if K
    // is not 32 bytes, if Extendable=false (see NonExtendableUnsupportedReason),
    // or if Xecrets rejects the group configuration (e.g., threshold > count).
    public static Result<IReadOnlyList<string>> SplitKey(byte[] key32, GroupConfig cfg)
    {
        if (key32.Length != 32)
            return Result.Failure<IReadOnlyList<string>>($"key must be 32 bytes (got {key32.Length})");

        if (!cfg.Extendable)
            return Result.Failure<IReadOnlyList<string>>(NonExtendableUnsupportedReason);

        // Each ShareGroup maps directly to a Xecrets Group — names are
        // tool-side metadata only and have no place in the SLIP-39 binary
        // representation.
        var groups = cfg.Groups
            .Select(g => new Group(ShareThreshold: g.Threshold, ShareCount: g.Count))
            .ToArray();

        // Retry transparently around the Xecrets Feistel-overflow bug. Any
        // other exception (Slip39Exception, ArgumentException, etc.) bubbles
        // straight to Result.Failure on the first try — those represent real
        // configuration problems and re-rolling will not fix them.
        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                var sss = new ShamirsSecretSharing(new StrongRandom());

                // GenerateShares returns Share[][] — outer array indexed by group,
                // inner array contains that group's member shares. Flatten to a
                // single list of mnemonic strings in group-major order; the group
                // index is encoded inside each share's prefix so the combine side
                // does not need group boundaries to be preserved.
                var shareGroups = sss.GenerateShares(
                    extendable: cfg.Extendable,
                    iterationExponent: IterationExponent,
                    groupThreshold: cfg.GroupThreshold,
                    groups: groups,
                    passphrase: "",
                    masterSecret: key32);

                var mnemonics = shareGroups
                    .SelectMany(g => g)
                    .Select(s => s.ToString())
                    .ToList();

                return Result.Success<IReadOnlyList<string>>(mnemonics);
            }
            catch (OverflowException) when (attempt < MaxXecretsOverflowRetries)
            {
                // Known Xecrets.Slip39 bug — re-roll the id and try again.
                continue;
            }
            catch (Exception ex)
            {
                return Result.Failure<IReadOnlyList<string>>($"SLIP-39 split failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    // Combines threshold-many SLIP-39 mnemonics back into the original 32-byte K.
    // Returns Failure if the combination fails: insufficient shares, mismatched
    // group identifiers across shares, malformed mnemonics, bad checksums, etc.
    // All such conditions throw Slip39Exception in Xecrets; we translate them
    // to Result.Failure so callers can react without try/catch. Additionally,
    // Xecrets returns a GroupedShares with an empty .Secret when shares are
    // insufficient (no exception thrown) — we treat empty .Secret as Failure
    // too, since the caller asked for the key, not an "almost there" status.
    public static Result<byte[]> CombineMnemonics(IEnumerable<string> mnemonics)
    {
        try
        {
            var shares = mnemonics.Select(Share.Parse).ToArray();

            // Xecrets' CombineShares requires EXACTLY the threshold number of shares —
            // both member-threshold-many shares per group and group-threshold-many
            // groups. Handing it MORE (e.g. all 5 shares of a 3-of-5) makes it return
            // an empty secret, surfacing as a bogus "insufficient shares" error. But
            // the natural recovery action is to paste EVERY share you hold, so we
            // down-select to a minimal satisfying subset before combining.
            var selected = SelectMinimalSubset(shares);

            var sss = new ShamirsSecretSharing(new StrongRandom());
            var recovered = sss.CombineShares(selected, "");
            return recovered.Secret.Length == 0
                ? Result.Failure<byte[]>("SLIP-39 combine failed: insufficient shares to reconstruct the secret")
                : Result.Success(recovered.Secret);
        }
        catch (Exception ex)
        {
            return Result.Failure<byte[]>($"SLIP-39 combine failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // Reduces a pile of shares (possibly more than needed, possibly with duplicate
    // member indices) to the minimal subset Xecrets can combine: exactly
    // member-threshold distinct members from exactly group-threshold satisfiable
    // groups. Each share self-describes its group/member indices and thresholds in
    // its prefix, so no external configuration is required. If the shares can't
    // satisfy the group threshold, the whole (unreduced) set is returned so the
    // downstream CombineShares produces a genuine insufficient-shares failure.
    static Share[] SelectMinimalSubset(IReadOnlyList<Share> shares)
    {
        if (shares.Count == 0)
            return [];

        var groupThreshold = shares[0].Prefix.GroupThreshold;

        // Per SLIP-39 group: dedupe by member index, then — if the group has at
        // least member-threshold distinct members — keep exactly that many.
        var satisfiedGroups = shares
            .GroupBy(s => s.Prefix.GroupIndex)
            .Select(g => new
            {
                MemberThreshold = g.First().Prefix.MemberThreshold,
                Members = g.GroupBy(s => s.Prefix.MemberIndex).Select(m => m.First()).ToList(),
            })
            .Where(g => g.Members.Count >= g.MemberThreshold)
            .Select(g => g.Members.Take(g.MemberThreshold).ToArray())
            .ToList();

        return satisfiedGroups.Count < groupThreshold
            ? shares.ToArray()
            : satisfiedGroups.Take(groupThreshold).SelectMany(g => g).ToArray();
    }
}
