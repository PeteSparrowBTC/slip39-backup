using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using Microsoft.JSInterop;

namespace Slip39Demo.Web.Services;

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
}
