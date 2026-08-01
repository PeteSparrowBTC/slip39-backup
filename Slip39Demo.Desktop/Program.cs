using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;
using Slip39Demo.Desktop;
using Slip39Demo.Desktop.Services;
using Slip39Demo.UI.Services;

namespace Slip39Demo.Desktop;

static class Program
{
    // [STAThread] is required for WebView2 (COM) on Windows dev machines —
    // without it the page executes but the window paints black. Harmless on
    // Linux/WebKitGTK, where this app actually ships (Tails AppImage).
    [STAThread]
    static void Main(string[] args)
    {
        var builder = PhotinoBlazorAppBuilder.CreateDefault(args);

        // DesktopRoot wraps the shared UI App and adds the CI smoke-test hook
        // (SPS_SMOKE=1 -> exit 0 once Blazor has rendered in the webview).
        builder.RootComponents.Add<DesktopRoot>("#app");

        // Native save dialog instead of the browser Blob/<a download> mechanism.
        builder.Services.AddScoped<IFileDownloader, NativeFileDownloader>();
        // Post-generation gate: same independent JS round-trip (slip39-js + typage)
        // as the web demo — WebKitGTK provides the JS engine and WebCrypto.
        builder.Services.AddScoped<IIndependentVerifier, JsIndependentVerifier>();
        // Airgap gate from kernel state (/sys/class/net), not webview state.
        builder.Services.AddScoped<IConnectivityProbe, LinuxConnectivityProbe>();

        var app = builder.Build();
        app.MainWindow
            .SetTitle("Seed Phrase Storage — SLIP-39")
            .SetUseOsDefaultSize(false)
            .SetSize(1100, 800);

        app.Run();
    }
}
