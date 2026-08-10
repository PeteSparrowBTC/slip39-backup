using System.Text.Json;
using FluentAssertions;
using Slip39Demo.Tests.Ui;
using Xunit;

namespace Slip39Demo.Tests.Packaging;

// Pins the parts of src-tauri/tauri.conf.json that the application cannot run without, and
// that nothing else would catch.
//
// WHY THIS FILE EXISTS. The AppImage's smoke test in CI starts the artifact under a virtual
// display and waits for a window with the expected title to appear. That proves the AppImage
// mounts, that WebKitGTK initialises, and that a window exists. It does NOT prove the .NET
// WebAssembly runtime started inside that window: a CSP missing 'wasm-unsafe-eval' produces
// a window with the right title and nothing in it, and the check would pass. The Photino
// shell this replaced had a hook that exited 0 once Blazor had rendered, which did prove it,
// and nothing replaces that hook.
//
// The honest options were a browser-automation check against the running window, which is
// worth doing and is a piece of work in itself, or a test hook compiled into the artifact
// people run against real seed phrases, which is not a trade worth making for this. So this
// covers the specific regression instead: the settings whose absence produces the empty
// window. It is not a substitute for the render check, and the workflow comment says so
// rather than implying the gap is closed.
public class ShellSecurityConfigTests
{
    static JsonElement Config()
    {
        var path = Path.Combine(StylesheetContractTests.RepoRootPath(), "src-tauri", "tauri.conf.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    }

    static string Csp() =>
        Config().GetProperty("app").GetProperty("security").GetProperty("csp").GetString()!;

    // Without this the runtime cannot compile the WebAssembly module, the window renders
    // empty, and the only symptom is an application that appears to do nothing.
    [Fact]
    public void The_csp_permits_webassembly() =>
        Csp().Should().Contain("'wasm-unsafe-eval'",
            "the .NET WebAssembly runtime cannot start without it, and the window renders empty");

    // The other half of the same requirement: 'wasm-unsafe-eval' is only consulted where
    // script sources are declared, so a default-src-only policy would still block it.
    [Fact]
    public void The_csp_declares_script_sources_explicitly() =>
        Csp().Should().Contain("script-src",
            "wasm-unsafe-eval is read from script-src, so a default-src-only policy blocks WASM");

    // The app must not be able to reach the network at all. This is an offline tool whose
    // whole promise is that a seed phrase typed into it does not leave the machine, and a
    // permissive connect-src would quietly undo that.
    [Fact]
    public void The_csp_confines_connections_to_the_app_itself() =>
        Csp().Should().Contain("connect-src 'self'",
            "an offline tool must not be able to open a connection to anywhere else");

    // Blazor cannot import the Tauri JavaScript API as a module, because it loads no
    // bundler. Without withGlobalTauri there is no window.__TAURI__, and every invoke in
    // the three services fails: the airgap probe reports online (fail-safe, so every backup
    // is watermarked INSECURE-TEST), saving throws, and encryption fails.
    [Fact]
    public void The_global_tauri_object_is_enabled() =>
        Config().GetProperty("app").GetProperty("withGlobalTauri").GetBoolean()
            .Should().BeTrue("Blazor has no bundler, so window.__TAURI__ must exist as a global");

    // Tauri's own bundler copies the WebKitGTK stack into the artifact, tripling its size
    // and pinning a rendering stack that Tails already ships. The AppDir is assembled by
    // packaging/appimage/build-appimage.sh instead.
    [Fact]
    public void The_tauri_bundler_is_off() =>
        Config().GetProperty("bundle").GetProperty("active").GetBoolean()
            .Should().BeFalse("build-appimage.sh assembles the AppDir, and bundling WebKitGTK triples the size");

    // The smoke test in .github/workflows/appimage.yml searches for a window by this title.
    // If the title changes and that step is not changed with it, the step fails on a
    // perfectly good artifact, which is the kind of red check people learn to ignore.
    [Fact]
    public void The_window_title_matches_what_the_smoke_test_searches_for()
    {
        var title = Config().GetProperty("app").GetProperty("windows")[0].GetProperty("title").GetString();

        title.Should().Contain("Seed Phrase Storage",
            "the AppImage smoke test finds the window by searching for this text");
    }
}
