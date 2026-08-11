using Slip39Demo.UI.Services;

namespace Slip39Demo.Tests.Web;

// Scripted stand-in for the GnuPG outer-lock check, shared by the Owner page tests.
// Defaults to Verified, because every test that is about something else needs the gate to
// get out of the way; the tests that are about the gate pass their own outcome.
//
// Records what it was asked, so a test can prove the page hands over the armored envelope
// and the inner age file rather than, say, the same bytes twice.
sealed class FakeOuterLockVerifier(OuterLockOutcome outcome = OuterLockOutcome.Verified, string detail = "stub gpg")
    : IOuterLockVerifier
{
    public List<(string Armored, int ExpectedInnerLength, int KeyLength)> Calls { get; } = new();

    public Task<OuterLockVerification> VerifyAsync(string armoredEnvelope, byte[] expectedInner, byte[] key32)
    {
        Calls.Add((armoredEnvelope, expectedInner.Length, key32.Length));
        return Task.FromResult(new OuterLockVerification(outcome, detail, []));
    }
}
