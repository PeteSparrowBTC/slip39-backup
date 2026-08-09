using CSharpFunctionalExtensions;

namespace Slip39Demo.UI.Services;

// Post-generation gate: a freshly generated backup must be round-tripped by
// implementations INDEPENDENT of the C# generation stack (slip39-js + typage in
// the browser) before the tool hands it to the user. Owner mode refuses the
// download when this fails — a generation-stack bug must not vouch for itself.
public interface IIndependentVerifier
{
    // subsets: share subsets covering every share (see VerificationSubsets).
    // payloadAge: the binary payload.age. expectedPayloadText: what it must
    // decrypt to. Success = every subset reconstructs the same key AND that key
    // decrypts the payload to exactly the expected text.
    Task<Result> VerifyAsync(
        IReadOnlyList<IReadOnlyList<string>> subsets,
        byte[] payloadAge,
        string expectedPayloadText);

    // The other direction, and the one Recoverer mode actually performs: a backup
    // produced entirely by the third-party implementations, which OUR stack then
    // has to read. Owner mode runs this too, because a tool that can write a
    // backup nobody else can read and a tool that cannot read anybody else's
    // backup are both broken, and only the first is caught by VerifyAsync.
    //
    // Runs against a throwaway probe with fresh randomness, so it tests the
    // binary on the machine in front of the user rather than a fixture. The
    // committed-fixture equivalent lives in
    // Slip39Demo.Tests/Interop/ForeignBackupRoundTripTests.cs and guards CI.
    Task<Result> VerifyForeignReadableAsync();
}
