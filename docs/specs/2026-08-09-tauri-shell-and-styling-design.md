# Replacing the Photino shell with Tauri, and adopting the dice-to-seed styling

Date: 2026-08-09
Status: approved, not yet implemented
Supersedes the shell half of [2026-08-01-native-window-appimage-design.md](2026-08-01-native-window-appimage-design.md)

## Why

Two reasons, and they are unrelated to each other. They share a design document
because they touch the same files, not because one requires the other.

**The shell.** `Slip39Demo.Desktop` hosts the UI in Photino.Blazor. Photino's last
release was January 2025 and it is published as ".NET 8/9 only", while this solution
targets net10.0. That is the reason to move: the shell holding an offline backup tool
should be something still receiving fixes.

Size is a secondary reason, and the numbers below are measured rather than assumed,
because each was measured on a different application:

| | measured | what it was |
| --- | --- | --- |
| this repo, Photino | **41.7 MB** | `dist/SPS-SLIP39-x86_64.AppImage`, built 2026-08-01, before the `age` bundle was added |
| dice-to-seed, Tauri, nothing bundled | 11.4 MB | a smaller app, recorded in tails-appimage |
| dice-to-seed, Tauri's own bundler | 83 MB | same app, WebKitGTK copied in |

Most of this repo's 41.7 MB is the self-contained .NET runtime, which Tauri removes
by running the app as WebAssembly instead. What replaces it is the WASM payload,
which is larger here than in dice-to-seed. **No size target is set for this work**,
and the result should be measured rather than predicted.

Tauri v2 is actively maintained and links the same `webkit2gtk-4.1` that Tails ships.
The packaging facts behind all of this are recorded in
[PeteSparrowBTC/tails-appimage](https://github.com/PeteSparrowBTC/tails-appimage),
which is the reference for anything AppImage-shaped in this organisation, and the
worked example is
[PeteSparrowBTC/dice-to-seed](https://github.com/PeteSparrowBTC/dice-to-seed).

**The styling.** The UI is the Blazor template's Bootstrap 5 with a 273-line dark
override sheet layered on top. It ships roughly 400 KB of vendored CSS and JavaScript
that an offline tool never needed, and the override sheet fights the framework
underneath it in places (`app.css` defines `.btn-primary` twice, with different
colours). dice-to-seed replaced the whole thing with about 450 lines of hand-written
CSS and system fonts.

## What is not changing

- `Slip39Demo.Core`. No cryptography, payload format or SLIP-39 logic is touched.
- The independent-verify gate. Every generated backup still round-trips through
  `slip39-js` and `typage` before the tool hands it out, from the same locally
  bundled `independent-verify.min.js`.
- `Slip39Demo.Web` as the GitHub Pages demo, including its `INSECURE-TEST`
  watermark and its in-process `AgeSharpPayloadEncryptor`.
- The decision that the artifact touching real seed phrases encrypts by running the
  reference `age` binary rather than a library linked into the app.

## Architecture

### Projects

| project | change |
| --- | --- |
| `Slip39Demo.Core` | untouched |
| `Slip39Demo.UI` | Bootstrap removed, `app.css` rewritten, markup rewritten in 11 `.razor` files |
| `Slip39Demo.Web` | service wiring untouched; inherits the restyle |
| `Slip39Demo.Tauri` | **new** Blazor WASM project, the AppImage frontend |
| `Slip39Demo.Desktop` | **deleted** |
| `src-tauri/` | **new** Rust shell |
| `packaging/appimage/` | rewritten for the Rust binary |

### Why a separate WASM project rather than one build that detects Tauri

Under Tauri the .NET code runs as WebAssembly inside the WebView, exactly as it does
in the browser. A single published frontend serving both the Pages demo and the
AppImage would have to choose its encryptor at runtime by looking for
`window.__TAURI__`.

`IPayloadEncryptor` already states the rule that forbids this:

> There is deliberately NO fallback from native to in-process. If the bundled binary
> is missing or misbehaves, generation fails and says so. Silently dropping back to
> the implementation we were trying to avoid would make the whole exercise decorative.

A runtime sniff is that fallback with extra steps: one wrong branch, one detection
quirk in a future WebView, and the AppImage encrypts in-process while displaying a
transcript that says otherwise. `Slip39Demo.Tauri` registers only
`TauriAgeEncryptor`, so `AgeSharpPayloadEncryptor` is not compiled into the AppImage
at all and the failure mode cannot exist.

The cost is a second WASM publish in CI and one more `Program.cs`. Both are small,
and the second publish is the artifact people actually download.

### The Rust shell

`src-tauri/src/main.rs` exposes three commands and nothing else.

```rust
#[tauri::command] age_encrypt(plaintext_b64: String, passphrase_hex: String) -> Result<AgeResult, String>
#[tauri::command] is_online() -> Result<bool, String>
#[tauri::command] save_file(suggested_name: String, bytes_b64: String) -> Result<Option<String>, String>
```

Each is deliberately thin, because Rust here is a capability layer, not a place for
application logic.

`age_encrypt` locates the bundled binary at `current_exe().parent()/age/age`, which
is the same convention `NativeAgeEncryptor` uses today via
`AppContext.BaseDirectory + "age"`, and which resolves correctly inside a mounted
AppImage. It runs `age` with the `age-plugin-batchpass` plugin, and returns
everything a reader could observe:

```rust
struct AgeResult {
    ciphertext_b64: String,
    binary_path:    String,
    version:        String,   // what the bundled binary reports
    command_line:   String,   // what was actually executed
    stderr:         String,
    exit_code:      i32,
}
```

It applies no policy. It does not decide whether an exit code is acceptable, does not
build a transcript, and does not fall back to anything. Those decisions stay in C#,
where the test suite is.

`is_online` reads `/sys/class/net`, the same kernel state
`LinuxConnectivityProbe` reads today, and returns `true` when the check cannot run,
per `IConnectivityProbe`'s requirement to fail safe toward watermarking.

`save_file` opens a native save dialog through `tauri-plugin-dialog` and writes the
bytes, returning the chosen path or `None` if the user cancelled.

Tauri embeds the published frontend into the executable at compile time through
`generate_context!()`, so the AppImage binds no port and listens on no interface.

### The C# side

`Slip39Demo.Tauri/Services/`:

| class | implements | notes |
| --- | --- | --- |
| `TauriAgeEncryptor` | `IPayloadEncryptor` | transcript building and the fail-closed rules, ported from `NativeAgeEncryptor` |
| `TauriConnectivityProbe` | `IConnectivityProbe` | fails safe to online |
| `TauriFileDownloader` | `IFileDownloader` | |
| `TauriInterop` | | one wrapper over `IJSRuntime` calling `window.__TAURI__.core.invoke` |

`TauriInterop` exists so the three services depend on an interface that a test can
fake, rather than on `IJSRuntime` directly.

`withGlobalTauri: true` is required in `tauri.conf.json` for `window.__TAURI__` to
exist, because Blazor loads no bundler and cannot import the Tauri JavaScript API as
a module.

### What happens to the tests

`NativeAgeEncryptor` is 200 lines, of which roughly 30 are process handling. The
remainder is transcript construction and the rules about what counts as a failure,
and that is the part worth testing. It moves to `TauriAgeEncryptor` behind
`TauriInterop`, so `Slip39Demo.Tests/Desktop/NativeAgeEncryptorTests.cs` becomes
`Slip39Demo.Tests/Tauri/TauriAgeEncryptorTests.cs` and keeps its assertions,
including the ones proving generation fails rather than falls back.

Binary path resolution and argument shape move to Rust and get `cargo test`. CI gains
a `cargo test` step, which is a new gate rather than a replacement for an existing
one.

No test in the suite asserts on a CSS class, verified by grep, so the restyle cannot
break the bUnit tests.

## Styling

`Slip39Demo.UI/wwwroot/css/app.css` is replaced with dice-to-seed's system.

```
--bg          #14161a      --ink       #e6e8ec     --ok    #3fbf7f
--panel       #1c1f26      --ink-dim   #99a1b0     --warn  #ff9f43
--panel-edge  #2c313b      --accent    #ffc53d     --bad   #ff5c5c
```

Fonts are `system-ui` and `ui-monospace` with fallbacks. No web font, no `@import`,
no CDN, so the app renders identically with the network cable out.

`Slip39Demo.UI/wwwroot/lib/bootstrap/**` is deleted, along with the `<link>` tags in
both `index.html` files. The idioms replacing the Bootstrap components are
dice-to-seed's: `panel` for `card`, `banner` and `banner-loud` for `alert`,
`mono-block` for preformatted output, a numbered word grid for mnemonics, and CSS
grid for `row`/`col`.

One rule is carried over deliberately: **colour is never the only encoding.** It
matters more here than in dice-to-seed, because this app has two distinctions a user
must not misread, online versus airgapped and real output versus `INSECURE-TEST`.
Each is signalled by wording and by border style as well as by hue, so it survives a
grayscale photograph, a projector, and a reader who cannot separate the colours.

## Packaging

`packaging/appimage/build-appimage.sh` is rewritten around the Rust binary. What
carries over unchanged from the current script and from PR #10:

- the `age` v1.3.1 bundle, pinned by version and by checksum, with the bundled binary
  executed and its version compared rather than merely present
- `appimagetool` 1.9.1, pinned by version and by checksum
- `--appimage-extract-and-run`, because CI runners have no FUSE
- a published `sha256sum` beside the artifact

What changes:

- no `dotnet publish` of a desktop project, and no self-contained .NET runtime in the
  AppDir. The AppDir holds `AppRun`, the `.desktop` file, the icon,
  `usr/bin/slip39-backup` and `usr/bin/age/`.
- the Photino glibc guard is replaced by the check tails-appimage recommends:
  `readelf -d` on the Rust binary must show `libwebkit2gtk-4.1` and must never show
  `libwebkit2gtk-4.0`, since Debian 13 dropped the 4.0 series and a 4.0 link would
  fail on Tails in a way that looks like an application bug.
- `AppRun` stays as thin as it is now. It sets no library path it does not need and
  runs no pre-flight check, per the tails-appimage finding that an `ldconfig` probe
  refused to start a working application on Tails because `ldconfig` is not on a
  normal user's PATH.

The CSP in `tauri.conf.json` must permit WebAssembly, or the .NET runtime cannot
start and the window renders empty:

```
default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'
```

## Known unknown

Whether an application-defined `#[tauri::command]` requires an entry in
`src-tauri/capabilities/default.json` under Tauri v2, or whether the capability system
gates only plugin commands. The configuration reference is ambiguous on the point and
dice-to-seed exposes no commands, so it does not settle it. The first `invoke` answers
it immediately: either the call returns a result or it returns a permission error
naming what is missing. `tauri-plugin-dialog` and `tauri-plugin-fs` definitely need
capability entries.

This is a first-run detail, not a design risk. It changes one JSON file either way.

## Delivery

Two pull requests, independent of each other:

**A. The restyle.** `Slip39Demo.UI` only. No shell change, no Rust, no packaging
change. Ships on its own and is verifiable in the browser against the existing Pages
demo.

**B. The Tauri swap.** The new project, the Rust shell, the deleted Photino project,
the rewritten packaging and the CI changes.

A lands first so that B's diff contains no styling noise.

*Collaboration by Claude*
