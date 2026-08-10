using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Slip39Demo.Tauri.Services;
using Slip39Demo.UI.Services;

// The AppImage frontend. Tasks 3 and 5 of docs/plans/2026-08-09-tauri-shell.md replace
// the remaining NotWiredYet registrations below with a real implementation.
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Slip39Demo.UI.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IIndependentVerifier, Slip39Demo.UI.Services.JsIndependentVerifier>();
builder.Services.AddScoped<IFileDownloader, NotWiredYet>();
builder.Services.AddScoped<ITauriInterop, TauriInterop>();
builder.Services.AddScoped<IConnectivityProbe, TauriConnectivityProbe>();
builder.Services.AddScoped<IPayloadEncryptor, NotWiredYet>();

await builder.Build().RunAsync();

// Temporary. Throws rather than silently doing the wrong thing.
sealed class NotWiredYet : IFileDownloader, IPayloadEncryptor
{
    public ValueTask DownloadAsync(string filename, byte[] bytes, string mimeType) =>
        throw new NotSupportedException("the Tauri file downloader is not wired up yet");

    public Task<Result<EncryptionOutcome>> EncryptAsync(byte[] plaintext, byte[] key32) =>
        Task.FromResult(Result.Failure<EncryptionOutcome>("the Tauri encryptor is not wired up yet"));
}
