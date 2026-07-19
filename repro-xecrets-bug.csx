#r "nuget: Xecrets.Slip39, 2.3.1315"
#nullable enable

using Xecrets.Slip39;

// Reproduces the Xecrets.Slip39 2.3.1315 extendable=false bug.
// Per the design-slip39-age-redesign-design Task 6 discovery:
//   extendable=true  -> 100% round-trip success
//   extendable=false -> ~50% OverflowException, ~25% silent wrong-byte recovery
//
// We run 100 trials per (extendable, iterationExponent) combination and print
// a table. The seed is fixed so the only randomness comes from Xecrets' internal
// StrongRandom (driven by the system CSPRNG).

var masterSecret = Enumerable.Range(0, 32).Select(i => (byte)(i * 7 & 0xff)).ToArray();
var groups = new[] { new Group(ShareThreshold: 2, ShareCount: 3) };
const int Trials = 100;

string Run(bool extendable, int iterationExponent)
{
    var sss = new ShamirsSecretSharing(new StrongRandom());
    int ok = 0, overflow = 0, silent = 0, other = 0;
    string? otherEx = null;

    for (var t = 0; t < Trials; t++)
    {
        try
        {
            var shares = sss.GenerateShares(
                extendable: extendable,
                iterationExponent: iterationExponent,
                groupThreshold: 1,
                groups: groups,
                passphrase: "",
                masterSecret: masterSecret);

            var recovered = sss.CombineShares([shares[0][0], shares[0][1]], "");
            if (recovered.Secret.AsSpan().SequenceEqual(masterSecret))
                ok++;
            else
                silent++;
        }
        catch (OverflowException)
        {
            overflow++;
        }
        catch (Exception ex)
        {
            other++;
            otherEx ??= $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    var extra = otherEx is null ? "" : $"   (other ex: {otherEx})";
    return $"  extendable={extendable,-5}  e={iterationExponent}   ok={ok,3}   overflow={overflow,3}   silent_mismatch={silent,3}   other={other,3}{extra}";
}

Console.WriteLine($"Xecrets.Slip39 2.3.1315  --  {Trials} trials per row");
Console.WriteLine($"Single group, ShareThreshold=2, ShareCount=3, fixed 32-byte masterSecret, passphrase=\"\"");
Console.WriteLine();

foreach (var e in new[] { 0, 1, 2 })
    Console.WriteLine(Run(extendable: true, iterationExponent: e));
Console.WriteLine();
foreach (var e in new[] { 0, 1, 2 })
    Console.WriteLine(Run(extendable: false, iterationExponent: e));
