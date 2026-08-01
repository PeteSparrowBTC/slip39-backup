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
}
