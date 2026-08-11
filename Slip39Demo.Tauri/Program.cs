using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Slip39Demo.Tauri.Services;
using Slip39Demo.UI.Services;

// The AppImage frontend.
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Slip39Demo.UI.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IIndependentVerifier, Slip39Demo.UI.Services.JsIndependentVerifier>();
builder.Services.AddScoped<IFileDownloader, TauriFileDownloader>();
builder.Services.AddScoped<ITauriInterop, TauriInterop>();
builder.Services.AddScoped<IConnectivityProbe, TauriConnectivityProbe>();
builder.Services.AddScoped<IPayloadEncryptor, TauriAgeEncryptor>();
// The outer OpenPGP lock is opened by the system's own GnuPG before the backup is handed
// over, so the one layer written in-process is not the one layer nothing independent has
// ever checked. Tails ships GnuPG; where it is missing, a real backup is refused.
builder.Services.AddScoped<IOuterLockVerifier, TauriPgpVerifier>();

await builder.Build().RunAsync();
