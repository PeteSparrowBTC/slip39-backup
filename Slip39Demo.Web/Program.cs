using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Slip39Demo.UI.Services;
using Slip39Demo.Web.Services;

// WASM shell for the hosted online demo. The UI lives in Slip39Demo.UI; this
// project only wires the browser-specific service implementations.
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Slip39Demo.UI.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IFileDownloader, BrowserFileDownloader>();
// Post-generation gate: backups must round-trip through independent JS
// implementations (slip39-js + typage) before the tool hands them out.
builder.Services.AddScoped<IIndependentVerifier, JsIndependentVerifier>();
// The browser cannot start a subprocess, so this build encrypts in-process with
// AgeSharp. It is the DEMONSTRATION build and watermarks its output
// INSECURE-TEST; the Tails AppImage runs the official age binary instead.
builder.Services.AddScoped<IPayloadEncryptor, AgeSharpPayloadEncryptor>();
// The outer OpenPGP lock cannot be checked here: a browser cannot run GnuPG, and asking
// BouncyCastle to open the envelope it just wrote would prove only self-consistency. This
// reports Unavailable, which limits this build to watermarked INSECURE-TEST backups.
builder.Services.AddScoped<IOuterLockVerifier, BrowserOuterLockVerifier>();
// Airgap indicator + INSECURE-TEST watermark gate.
builder.Services.AddScoped<IConnectivityProbe, JsConnectivityProbe>();

await builder.Build().RunAsync();
