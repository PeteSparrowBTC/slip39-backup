using Slip39Demo.UI.Services;

namespace Slip39Demo.Web.Services;

// The browser cannot start a subprocess, so it cannot ask GnuPG anything. This reports
// Unavailable and nothing else.
//
// WHAT THAT COSTS, AND WHY IT IS NOT PAPERED OVER
// Owner refuses a REAL backup on Unavailable, so this build can only produce watermarked
// INSECURE-TEST backups. That is not a regression: the hosted demo is already marked
// DEMONSTRATION AND TESTING ONLY, and a page saved and re-opened offline would otherwise
// be able to produce a real backup whose outer lock nothing independent had ever opened.
//
// The tempting alternative was to verify with BouncyCastle, which is present here and
// would make this method return Verified. It is not done, deliberately: BouncyCastle
// wrote the envelope, so asking it to open the envelope proves only that it is
// self-consistent. A check that always passes is worse than a missing one, because it
// reads as evidence.
public sealed class BrowserOuterLockVerifier : IOuterLockVerifier
{
    public Task<OuterLockVerification> VerifyAsync(
        string armoredEnvelope, byte[] expectedInner, byte[] key32) =>
        Task.FromResult(OuterLockVerification.Unavailable(
            "A browser cannot run GnuPG, so the outer OpenPGP lock cannot be checked by an "
            + "implementation independent of the one that wrote it. Real backups need the "
            + "AppImage on Tails, where GnuPG is already installed."));
}
