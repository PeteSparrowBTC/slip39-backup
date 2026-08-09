using System.Text;
using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using Microsoft.JSInterop;
using Slip39Demo.Core.Age;
using Slip39Demo.Core.Slip39;

namespace Slip39Demo.UI.Services;

// Calls window.SPSVerify.verifyBackup (wwwroot/js/independent-verify.min.js — a
// locally bundled slip39-js + typage, see tools/independent-verify). The JS side
// never throws for verification failures; it returns { ok, kHex, error }, which we
// translate to Result. Interop/script-loading exceptions also become Failure —
// "could not verify" must block the download exactly like "verified wrong".
public sealed class JsIndependentVerifier(IJSRuntime js) : IIndependentVerifier
{
    // DTO for the JS result. kHex intentionally unused: the recovered key must not
    // spread further than the verification that needed it.
    sealed record VerifyResult(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")] string? Error);

    public async Task<Result> VerifyAsync(
        IReadOnlyList<IReadOnlyList<string>> subsets,
        byte[] payloadAge,
        string expectedPayloadText)
    {
        try
        {
            var result = await js.InvokeAsync<VerifyResult>("SPSVerify.verifyBackup", new
            {
                subsets,
                payloadAgeB64 = Convert.ToBase64String(payloadAge),
                expectedPayloadText,
            });

            return result.Ok
                ? Result.Success()
                : Result.Failure($"independent verification failed: {result.Error}");
        }
        catch (Exception ex)
        {
            return Result.Failure($"independent verification could not run: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // DTO for the foreign probe. No key field by design: the C# side must
    // reconstruct it from the mnemonics or the SLIP-39 half of this gate would be
    // testing nothing.
    sealed record ForeignProbe(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("mnemonics")] string[]? Mnemonics,
        [property: JsonPropertyName("payloadAgeB64")] string? PayloadAgeB64,
        [property: JsonPropertyName("expectedPayloadText")] string? ExpectedPayloadText,
        [property: JsonPropertyName("error")] string? Error);

    public async Task<Result> VerifyForeignReadableAsync()
    {
        ForeignProbe probe;
        try
        {
            probe = await js.InvokeAsync<ForeignProbe>("SPSVerify.produceForeignProbe");
        }
        catch (Exception ex)
        {
            return Result.Failure($"foreign-implementation probe could not run: {ex.GetType().Name}: {ex.Message}");
        }

        if (!probe.Ok || probe.Mnemonics is null || probe.PayloadAgeB64 is null || probe.ExpectedPayloadText is null)
            return Result.Failure($"foreign-implementation probe could not be produced: {probe.Error}");

        // Xecrets must combine mnemonics slip39-js wrote. Combining is a separate
        // code path from splitting, and it is the one Recoverer mode depends on.
        var key = Slip39Wrapping.CombineMnemonics(probe.Mnemonics);
        if (key.IsFailure)
            return Result.Failure($"this build cannot combine SLIP-39 shares written by another implementation: {key.Error}");

        // AgeSharp must decrypt a blob typage wrote, keyed by what Xecrets just
        // recovered. A wrong key and a wire-format divergence both land here; the
        // plaintext comparison below separates "decrypted something" from
        // "decrypted the right thing".
        var plaintext = AgePassphrase.Decrypt(Convert.FromBase64String(probe.PayloadAgeB64), key.Value);
        if (plaintext.IsFailure)
            return Result.Failure($"this build cannot decrypt an age file written by another implementation: {plaintext.Error}");

        return Encoding.UTF8.GetString(plaintext.Value) == probe.ExpectedPayloadText
            ? Result.Success()
            : Result.Failure("foreign-implementation probe decrypted to the wrong plaintext");
    }
}
