using Photino.Blazor;
using SpikePhotino;

// Native WebKitGTK window hosting Blazor — no browser, no localhost port, no Tor.
// Blazor runs in-process on .NET; the webview only renders and bridges JS interop.
var builder = PhotinoBlazorAppBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("app");

var app = builder.Build();
app.MainWindow
    .SetTitle("SPS Photino Spike — Tails WebKitGTK checks")
    .SetUseOsDefaultSize(false)
    .SetSize(900, 700);

app.Run();
