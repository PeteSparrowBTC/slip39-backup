using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Slip39Demo.Web;
using Slip39Demo.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IFileDownloader, BrowserFileDownloader>();
// Post-generation gate: backups must round-trip through independent JS
// implementations (slip39-js + typage) before the tool hands them out.
builder.Services.AddScoped<IIndependentVerifier, JsIndependentVerifier>();
// Airgap indicator + INSECURE-TEST watermark gate.
builder.Services.AddScoped<IConnectivityProbe, JsConnectivityProbe>();

await builder.Build().RunAsync();
