using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Slip39Demo.Tauri.Services;
using Slip39Demo.UI.Services;

// The AppImage frontend. Task 5 of docs/plans/2026-08-09-tauri-shell.md replaces the
// remaining NotWiredYet registration below with a real implementation.
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Slip39Demo.UI.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IIndependentVerifier, Slip39Demo.UI.Services.JsIndependentVerifier>();
builder.Services.AddScoped<IFileDownloader, TauriFileDownloader>();
builder.Services.AddScoped<ITauriInterop, TauriInterop>();
builder.Services.AddScoped<IConnectivityProbe, TauriConnectivityProbe>();
builder.Services.AddScoped<IPayloadEncryptor, NotWiredYet>();

await builder.Build().RunAsync();

// Temporary. Throws rather than silently doing the wrong thing.
sealed class NotWiredYet : IPayloadEncryptor
{
    public Task<Result<EncryptionOutcome>> EncryptAsync(byte[] plaintext, byte[] key32) =>
        Task.FromResult(Result.Failure<EncryptionOutcome>("the Tauri encryptor is not wired up yet"));
}
