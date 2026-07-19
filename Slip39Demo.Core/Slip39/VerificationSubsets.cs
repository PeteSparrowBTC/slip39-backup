namespace Slip39Demo.Core.Slip39;

// Builds the share subsets used for independent post-generation verification.
//
// Verifying ONE threshold subset would leave the remaining shares untested — a
// corrupt share #5 of a 3-of-5 would ship unverified. So we build a covering set:
// every share appears in at least one subset, and every subset satisfies the
// threshold structure on its own (member threshold within its group + group
// threshold across groups).
//
// Subsets are exact-size on purpose: the independent verifier (slip39-js) rejects
// more-than-threshold share sets, the same convention Xecrets follows.
public static class VerificationSubsets
{
    // flatMnemonics must be in group-flat order (group 0's shares first, then
    // group 1's, ...) — exactly what Slip39Wrapping.SplitKey returns.
    public static IReadOnlyList<IReadOnlyList<string>> BuildCoveringSubsets(
        GroupConfig cfg, IReadOnlyList<string> flatMnemonics)
    {
        // Slice the flat list back into per-group member lists.
        var groups = new List<(ShareGroup G, List<string> Members)>();
        var idx = 0;
        foreach (var g in cfg.Groups)
        {
            groups.Add((g, flatMnemonics.Skip(idx).Take(g.Count).ToList()));
            idx += g.Count;
        }

        var subsets = new List<IReadOnlyList<string>>();
        for (var gi = 0; gi < groups.Count; gi++)
        {
            var (g, members) = groups[gi];

            // Filler shares so the subset meets the group threshold: the first
            // member-threshold shares of (groupThreshold - 1) OTHER groups. The
            // fillers repeat across subsets — only the target group's chunk
            // varies, which is what walks every share through a verification.
            var fillers = groups
                .Where((_, j) => j != gi)
                .Take(cfg.GroupThreshold - 1)
                .SelectMany(x => x.Members.Take(x.G.Threshold))
                .ToList();

            // Chunk the group's members into windows of exactly Threshold. The
            // final window is the LAST Threshold members (windows may overlap —
            // coverage matters, disjointness doesn't).
            for (var start = 0; ; start += g.Threshold)
            {
                var chunk = start + g.Threshold <= members.Count
                    ? members.Skip(start).Take(g.Threshold)
                    : members.Skip(Math.Max(0, members.Count - g.Threshold));

                subsets.Add(chunk.Concat(fillers).ToList());

                if (start + g.Threshold >= members.Count)
                    break;
            }
        }

        return subsets;
    }
}
