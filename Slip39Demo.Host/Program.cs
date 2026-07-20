// Minimal offline host for the SLIP-39 backup tool. Serves the published Blazor
// WASM app (the `wwwroot` folder sitting NEXT TO this executable) on loopback
// only, so the AppImage can run fully airgapped on Tails/any Linux:
//
//     ./SPS-SLIP39.AppImage            -> http://127.0.0.1:9876
//     ./SPS-SLIP39.AppImage --port N   -> http://127.0.0.1:N
//
// Design notes:
//  - 127.0.0.1 binding only — the tool must never be reachable from the network.
//  - Port 9876 matches the Tails runbook in the design doc (§6.4).
//  - No logging of requests: nothing about backup activity should be written
//    anywhere (the machine may be amnesic, but belt-and-braces).
using Microsoft.Extensions.FileProviders;

var port = args.SkipWhile(a => a != "--port").Skip(1).Select(int.Parse).FirstOrDefault(9876);

// The static app lives next to the (single-file) executable: <dir>/wwwroot.
var baseDir = AppContext.BaseDirectory;
var webRoot = Path.Combine(baseDir, "wwwroot");
if (!Directory.Exists(webRoot))
{
    Console.Error.WriteLine($"error: wwwroot not found next to the executable ({webRoot})");
    return 1;
}

var builder = WebApplication.CreateSlimBuilder();
builder.Logging.ClearProviders(); // no request/activity logging, ever
builder.WebHost.UseKestrel(o => o.ListenLocalhost(port));

var app = builder.Build();

var provider = new PhysicalFileProvider(webRoot);
// ServeUnknownFileTypes: Blazor's runtime assets include extensions with no
// registered MIME type (e.g. the ICU data file *.dat) — without this the boot
// fails with "Failed to fetch icudt_*.dat".
var staticOptions = new StaticFileOptions
{
    FileProvider = provider,
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream",
};
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = provider });
app.UseStaticFiles(staticOptions);
// SPA fallback: deep links like /owner and /recoverer resolve to index.html.
app.MapFallbackToFile("index.html", staticOptions);

Console.WriteLine($"SLIP-39 backup tool serving OFFLINE at: http://127.0.0.1:{port}");
Console.WriteLine("Open that address in the browser. Ctrl+C stops the tool.");

app.Run();
return 0;
