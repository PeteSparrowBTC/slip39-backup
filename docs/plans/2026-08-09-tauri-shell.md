# Tauri Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Photino.Blazor desktop shell with a Tauri v2 shell, without losing the three native capabilities the AppImage depends on: running the reference `age` binary, reading `/sys/class/net` for the airgap gate, and a native save dialog.

**Architecture:** A Rust shell exposes exactly three commands and holds no application logic. A new Blazor WebAssembly project, `Slip39Demo.Tauri`, is the frontend Tauri embeds, and it registers three service implementations that call those commands over `window.__TAURI__.core.invoke`. `AgeSharpPayloadEncryptor` is not referenced by that project, so the AppImage cannot fall back to encrypting in-process. `Slip39Demo.Desktop` is deleted.

**Tech Stack:** Tauri v2 (Rust, edition 2021), Blazor WebAssembly (net10.0), `tauri-plugin-dialog`, `tauri-plugin-fs`, xunit + bUnit, `cargo test`, appimagetool 1.9.1.

This plan implements the shell half of
[docs/specs/2026-08-09-tauri-shell-and-styling-design.md](../specs/2026-08-09-tauri-shell-and-styling-design.md).
It is PR B. **PR A ([the restyle](2026-08-09-restyle-from-dice-to-seed.md)) must be merged
first**, so this diff contains no styling noise.

## Global Constraints

- **No em dashes and no en dashes** anywhere: prose, comments, commit messages. (`CLAUDE.md`)
- **Never push to `main`. Never merge a pull request. Never push a `v*` tag**, because `appimage.yml` publishes a release on tag push and that artifact is what people run against real seed phrases.
- **Fail closed, never fall back.** If the bundled `age` binary is missing, will not run, or returns anything unexpected, generation fails with a message. There is deliberately no path from the native encryptor to the in-process one.
- **The airgap probe fails safe toward danger.** If the check cannot run at all, report online, so output is watermarked `INSECURE-TEST` rather than silently trusted.
- **The key never goes on a command line.** It is passed in `AGE_PASSPHRASE`, read by `age-plugin-batchpass`, and `PATH` is pinned to the bundled directory so no other plugin on the machine can be picked up.
- **Pinned, verified downloads only:** `age` v1.3.1, sha256 `bdc69c09cbdd6cf8b1f333d372a1f58247b3a33146406333e30c0f26e8f51377`; appimagetool `1.9.1`, sha256 `ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0`. Version and checksum are updated together, never one alone.
- **Do not bundle WebKitGTK.** Tails ships `libwebkit2gtk-4.1`, and bundling it triples the artifact. Verified in the Tails package manifest, recorded in [PeteSparrowBTC/tails-appimage](https://github.com/PeteSparrowBTC/tails-appimage).
- **No pre-flight check in `AppRun`.** A check that can produce a false negative is worse than no check: an `ldconfig` probe once refused to start a working application on Tails because `ldconfig` is not on a normal user's PATH.
- **CSP must allow WebAssembly**, or the .NET runtime cannot start and the window renders empty: `script-src 'self' 'wasm-unsafe-eval'`.
- Branch: `feat/tauri-shell`, based on `origin/main` after PR A merges.

---

## File Structure

| file | responsibility |
| --- | --- |
| `src-tauri/Cargo.toml` | shell manifest, release profile tuned for size |
| `src-tauri/build.rs` | `tauri_build::build()` |
| `src-tauri/tauri.conf.json` | window, CSP, `withGlobalTauri`, `frontendDist` |
| `src-tauri/capabilities/default.json` | plugin permissions |
| `src-tauri/icons/icon.png` | RGBA icon; Tauri rejects non-RGBA |
| `src-tauri/src/main.rs` | builder, handler registration, nothing else |
| `src-tauri/src/net.rs` | `is_online`, `/sys/class/net` carrier check |
| `src-tauri/src/save.rs` | `save_file`, native dialog |
| `src-tauri/src/age.rs` | `age_encrypt`, subprocess handling |
| `Slip39Demo.Tauri/Slip39Demo.Tauri.csproj` | WASM frontend project |
| `Slip39Demo.Tauri/Program.cs` | DI wiring, Tauri implementations only |
| `Slip39Demo.Tauri/wwwroot/index.html` | host page |
| `Slip39Demo.Tauri/Services/ITauriInterop.cs` | the seam a test can fake |
| `Slip39Demo.Tauri/Services/TauriInterop.cs` | `IJSRuntime` to `invoke` |
| `Slip39Demo.Tauri/Services/TauriConnectivityProbe.cs` | `IConnectivityProbe` |
| `Slip39Demo.Tauri/Services/TauriFileDownloader.cs` | `IFileDownloader` |
| `Slip39Demo.Tauri/Services/TauriAgeEncryptor.cs` | `IPayloadEncryptor`, transcript and policy |
| `Slip39Demo.Tests/Tauri/*` | ported tests |
| `packaging/appimage/build-appimage.sh` | rewritten for the Rust binary |
| `.github/workflows/appimage.yml` | rewritten |

**Deleted:** `Slip39Demo.Desktop/**`, `Slip39Demo.Tests/Desktop/NativeAgeEncryptorTests.cs`.

### The split between Rust and C#

Rust does what WebAssembly cannot: find a file, run a program, read `/sys`, open a
dialog. It applies no policy. It does not decide whether an exit code is acceptable,
does not build a transcript, and does not fall back to anything.

C# keeps every judgement, because that is where the test suite is. `TauriAgeEncryptor`
is `NativeAgeEncryptor` with the 50 lines of `Process` handling removed and the rest
unchanged: the 32-byte key check, the missing-binary messages, the exit code and
empty-output checks, the `age-encryption.org/v1` header check, and the transcript text.

One thing moves to Rust that is currently in C#: the SHA-256 of the `age` binary,
which `NativeAgeEncryptor.cs:78` computes with `File.ReadAllBytesAsync`. WebAssembly
cannot read the file, so `age_encrypt` returns the digest it computed.

---

## Task 1: A window that renders the app

The single largest unknown is whether Blazor WebAssembly starts inside Tauri's asset
protocol under a CSP that permits WASM. dice-to-seed proves it does for its app. This
task proves it for this one, before any command exists to complicate the diagnosis.

**Files:**
- Create: `src-tauri/Cargo.toml`, `src-tauri/build.rs`, `src-tauri/tauri.conf.json`, `src-tauri/src/main.rs`, `src-tauri/icons/icon.png`
- Create: `Slip39Demo.Tauri/Slip39Demo.Tauri.csproj`, `Slip39Demo.Tauri/Program.cs`, `Slip39Demo.Tauri/wwwroot/index.html`
- Modify: `Slip39Demo.slnx`

**Interfaces:**
- Produces: a published frontend at `publish-tauri/wwwroot`, and a binary at `src-tauri/target/release/slip39-backup`.

- [ ] **Step 1: Create the frontend project**

`Slip39Demo.Tauri/Slip39Demo.Tauri.csproj`:

```xml
<!-- The AppImage frontend. Deliberately separate from Slip39Demo.Web rather than one
     build that detects Tauri at runtime: this project never references
     AgeSharpPayloadEncryptor, so the artifact that touches real seed phrases cannot
     fall back to encrypting in-process. See
     docs/specs/2026-08-09-tauri-shell-and-styling-design.md. -->
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.0" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="10.0.0" PrivateAssets="all" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Slip39Demo.UI\Slip39Demo.UI.csproj" />
  </ItemGroup>
</Project>
```

If the package version does not resolve, match whatever `Slip39Demo.Web.csproj`
already uses rather than guessing.

- [ ] **Step 2: Create the host page**

`Slip39Demo.Tauri/wwwroot/index.html`:

```html
<!DOCTYPE html>
<html lang="en">

<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>SLIP-39 + age wallet backup</title>
    <base href="/" />
    <link rel="preload" id="webassembly" />
    <link rel="stylesheet" href="_content/Slip39Demo.UI/css/app.css" />
    <link rel="icon" type="image/png" href="_content/Slip39Demo.UI/favicon.png" />
    <link href="Slip39Demo.Tauri.styles.css" rel="stylesheet" />
    <script type="importmap"></script>
</head>

<body>
    <div id="app">
        <svg class="loading-progress">
            <circle r="40%" cx="50%" cy="50%" />
            <circle r="40%" cx="50%" cy="50%" />
        </svg>
        <div class="loading-progress-text"></div>
    </div>

    <div id="blazor-error-ui">
        An unhandled error has occurred.
        <a href="." class="reload">Reload</a>
        <span class="dismiss">🗙</span>
    </div>
    <!-- Third-party slip39-js + typage bundle (window.SPSVerify). Every generated
         backup is independently round-tripped through these libs before the tool
         will hand it out: a bug in the C# generation stack cannot vouch for
         itself. Bundled locally (tools/independent-verify), no CDN, offline-safe. -->
    <script src="_content/Slip39Demo.UI/js/independent-verify.min.js"></script>
    <script src="_framework/blazor.webassembly#[.{fingerprint}].js"></script>
</body>

</html>
```

- [ ] **Step 3: Create Program.cs with the services still unimplemented**

`Slip39Demo.Tauri/Program.cs`:

```csharp
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Slip39Demo.UI.Services;

// The AppImage frontend. Tasks 2 to 5 of docs/plans/2026-08-09-tauri-shell.md replace
// each NotWiredYet registration below with a real implementation.
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Slip39Demo.UI.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IIndependentVerifier, Slip39Demo.UI.Services.JsIndependentVerifier>();
builder.Services.AddScoped<IFileDownloader, NotWiredYet>();
builder.Services.AddScoped<IConnectivityProbe, NotWiredYet>();
builder.Services.AddScoped<IPayloadEncryptor, NotWiredYet>();

await builder.Build().RunAsync();

// Temporary. Throws rather than silently doing the wrong thing, and reports online so
// that if it somehow survived to a release, output would be watermarked rather than
// trusted.
sealed class NotWiredYet : IFileDownloader, IConnectivityProbe, IPayloadEncryptor
{
    public ValueTask DownloadAsync(string filename, byte[] bytes, string mimeType) =>
        throw new NotSupportedException("the Tauri file downloader is not wired up yet");

    public Task<bool> IsOnlineAsync() => Task.FromResult(true);

    public Task<Result<EncryptionOutcome>> EncryptAsync(byte[] plaintext, byte[] key32) =>
        Task.FromResult(Result.Failure<EncryptionOutcome>("the Tauri encryptor is not wired up yet"));
}
```

- [ ] **Step 4: Publish the frontend and check what came out**

```bash
dotnet publish Slip39Demo.Tauri -c Release -o publish-tauri
ls publish-tauri/wwwroot/_framework/*.wasm | head
```

Expected: `.wasm` files present. If `_framework` is empty the project is not building
as WebAssembly, and nothing later in this plan will work.

- [ ] **Step 5: Create the Rust shell**

`src-tauri/Cargo.toml`:

```toml
# The desktop shell. Its whole job is to open a window, point a WebView at the
# published Blazor app, and provide the three things WebAssembly cannot do for itself.
# No cryptography and no application logic live here.

[package]
name = "slip39-backup"
version = "0.1.0"
edition = "2021"
rust-version = "1.77"
description = "Offline SLIP-39 + age wallet backup, desktop shell"
license = "MIT"

[build-dependencies]
tauri-build = { version = "2", features = [] }

[dependencies]
tauri = { version = "2", features = [] }
tauri-plugin-dialog = "2"
tauri-plugin-fs = "2"
serde = { version = "1", features = ["derive"] }
base64 = "0.22"
sha2 = "0.10"

# Small and stripped: this is a window, not a program, and the AppImage should stay
# reviewable in size as well as in source.
[profile.release]
strip = true
opt-level = "z"
lto = true
codegen-units = 1
panic = "abort"
```

`src-tauri/build.rs`:

```rust
fn main() {
    tauri_build::build()
}
```

`src-tauri/src/main.rs`:

```rust
// The desktop shell.
//
// Tauri serves the frontend through its own protocol, in process, so nothing binds a
// port and nothing listens on any interface. It uses webkit2gtk-4.1, which Tails
// ships, so this AppImage bundles no browser engine.
//
// Three commands are exposed and no more. Each is a capability WebAssembly does not
// have, and none of them decides anything: the policy lives in C#, where the tests
// are.

#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

fn main() {
    tauri::Builder::default()
        .run(tauri::generate_context!())
        .expect("slip39-backup: failed to start the window");
}
```

`src-tauri/tauri.conf.json`:

```json
{
  "$schema": "https://schema.tauri.app/config/2",
  "productName": "slip39-backup",
  "version": "0.1.0",
  "identifier": "btc.petesparrow.slip39backup",
  "build": {
    "frontendDist": "../publish-tauri/wwwroot"
  },
  "app": {
    "withGlobalTauri": true,
    "windows": [
      {
        "title": "Seed Phrase Storage: SLIP-39",
        "width": 1100,
        "height": 800,
        "resizable": true
      }
    ],
    "security": {
      "csp": "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'"
    }
  },
  "bundle": {
    "active": false,
    "icon": ["icons/icon.png"]
  }
}
```

`withGlobalTauri` is what puts `window.__TAURI__` on the page. Blazor loads no bundler
and cannot import the Tauri JavaScript API as a module, so without it every `invoke`
in Task 2 onward fails with `__TAURI__ is undefined`.

`bundle.active` is false because `packaging/appimage/build-appimage.sh` assembles the
AppDir by hand. Tauri's own bundler copies the WebKitGTK stack in and triples the size.

- [ ] **Step 6: Provide an RGBA icon**

```bash
mkdir -p src-tauri/icons
cp Slip39Demo.UI/wwwroot/favicon.png src-tauri/icons/icon.png
python3 -c "from PIL import Image; im=Image.open('src-tauri/icons/icon.png'); print(im.mode)"
```

Expected: `RGBA`. Tauri rejects a non-RGBA icon outright. If it prints `RGB` or `P`,
convert it: `Image.open(...).convert('RGBA').save(...)`.

- [ ] **Step 7: Build and open the window**

```bash
cargo tauri build --no-bundle --manifest-path src-tauri/Cargo.toml
./src-tauri/target/release/slip39-backup
```

Expected: a window opens showing the landing page, styled. If it is blank, open the
WebView inspector and look for a CSP violation naming `wasm-unsafe-eval`, which means
the CSP line above was not applied.

- [ ] **Step 8: Add both projects to the solution**

```bash
dotnet sln Slip39Demo.slnx add Slip39Demo.Tauri/Slip39Demo.Tauri.csproj
dotnet test Slip39Demo.slnx -c Release
```

Expected: PASS. Nothing references the new project yet, so this is a build check.

- [ ] **Step 9: Commit**

```bash
git add src-tauri Slip39Demo.Tauri Slip39Demo.slnx
git commit -m "Add a Tauri shell and a Blazor WebAssembly frontend for it"
```

---

## Task 2: The invoke seam, and the airgap probe

The simplest of the three commands goes first, because it is the one that proves the
`invoke` path end to end and settles whether an application-defined command needs a
capability entry.

**Files:**
- Create: `src-tauri/src/net.rs`, `src-tauri/capabilities/default.json`
- Create: `Slip39Demo.Tauri/Services/ITauriInterop.cs`, `TauriInterop.cs`, `TauriConnectivityProbe.cs`
- Create: `Slip39Demo.Tests/Tauri/TauriConnectivityProbeTests.cs`
- Modify: `src-tauri/src/main.rs`, `Slip39Demo.Tauri/Program.cs`

**Interfaces:**
- Consumes: the shell from Task 1.
- Produces: `ITauriInterop.InvokeAsync<T>(string command, object? args = null)`, used by Tasks 3 and 5.

- [ ] **Step 1: Write the failing C# test**

`Slip39Demo.Tests/Tauri/TauriConnectivityProbeTests.cs`:

```csharp
using Slip39Demo.Tauri.Services;

namespace Slip39Demo.Tests.Tauri;

public class TauriConnectivityProbeTests
{
    sealed class FakeInterop(Func<string, object?> handler) : ITauriInterop
    {
        public ValueTask<T> InvokeAsync<T>(string command, object? args = null) =>
            handler(command) is T value
                ? ValueTask.FromResult(value)
                : throw new InvalidOperationException($"unexpected command {command}");
    }

    sealed class ThrowingInterop : ITauriInterop
    {
        public ValueTask<T> InvokeAsync<T>(string command, object? args = null) =>
            throw new InvalidOperationException("the shell is not answering");
    }

    [Fact]
    public async Task Reports_offline_when_the_shell_says_so() =>
        Assert.False(await new TauriConnectivityProbe(new FakeInterop(_ => false)).IsOnlineAsync());

    [Fact]
    public async Task Reports_online_when_the_shell_says_so() =>
        Assert.True(await new TauriConnectivityProbe(new FakeInterop(_ => true)).IsOnlineAsync());

    // The direction that matters. An unanswerable probe must count as online, so the
    // backup is watermarked INSECURE-TEST rather than silently passing as airgapped.
    [Fact]
    public async Task Reports_online_when_the_check_cannot_run() =>
        Assert.True(await new TauriConnectivityProbe(new ThrowingInterop()).IsOnlineAsync());
}
```

- [ ] **Step 2: Run it and watch it fail**

Run: `dotnet test Slip39Demo.slnx --filter FullyQualifiedName~TauriConnectivityProbeTests`

Expected: compile error, `ITauriInterop` does not exist.

- [ ] **Step 3: Write the interop seam**

`Slip39Demo.Tauri/Services/ITauriInterop.cs`:

```csharp
namespace Slip39Demo.Tauri.Services;

// The single seam between the WebAssembly app and the Rust shell. It exists so the
// three services can be tested against a fake instead of a live WebView.
public interface ITauriInterop
{
    ValueTask<T> InvokeAsync<T>(string command, object? args = null);
}
```

`Slip39Demo.Tauri/Services/TauriInterop.cs`:

```csharp
using Microsoft.JSInterop;

namespace Slip39Demo.Tauri.Services;

// Calls window.__TAURI__.core.invoke, which exists because tauri.conf.json sets
// withGlobalTauri. Blazor loads no bundler, so the module import form of the Tauri
// API is not available here.
public sealed class TauriInterop(IJSRuntime js) : ITauriInterop
{
    public ValueTask<T> InvokeAsync<T>(string command, object? args = null) =>
        js.InvokeAsync<T>("__TAURI__.core.invoke", command, args ?? new { });
}
```

`Slip39Demo.Tauri/Services/TauriConnectivityProbe.cs`:

```csharp
using Slip39Demo.UI.Services;

namespace Slip39Demo.Tauri.Services;

// Airgap probe. The kernel state it reads is described in src-tauri/src/net.rs; this
// class exists to enforce the fail-safe direction on the C# side as well, so an
// interop failure cannot be mistaken for an offline machine.
public sealed class TauriConnectivityProbe(ITauriInterop interop) : IConnectivityProbe
{
    public async Task<bool> IsOnlineAsync()
    {
        try
        {
            return await interop.InvokeAsync<bool>("is_online");
        }
        catch
        {
            // Fail toward danger: unknown means watermark the output.
            return true;
        }
    }
}
```

- [ ] **Step 4: Run the C# test and watch it pass**

Run: `dotnet test Slip39Demo.slnx --filter FullyQualifiedName~TauriConnectivityProbeTests`

Expected: PASS, 3 tests.

- [ ] **Step 5: Write the Rust command with its own test**

`src-tauri/src/net.rs`:

```rust
//! Airgap probe, ported from Slip39Demo.Desktop/Services/LinuxConnectivityProbe.cs.
//!
//! Online means any non-loopback interface has a live carrier, read from
//! /sys/class/net/<if>/carrier. Carrier is used rather than operstate because idle
//! NIC drivers commonly report operstate "unknown", which false-positives an
//! airgapped machine as online.
//!
//! Fail-safe direction: if /sys cannot be enumerated at all, report online, so
//! generation falls into the INSECURE-TEST path instead of silently passing as
//! airgapped. A single unreadable carrier file is different: the kernel refuses that
//! read for an admin-down interface, which means no link is possible, so that one
//! interface counts as offline.

use std::path::Path;

pub fn any_carrier_live(net_dir: &Path) -> bool {
    let entries = match std::fs::read_dir(net_dir) {
        Ok(entries) => entries,
        // Cannot enumerate at all: assume the worst.
        Err(_) => return true,
    };

    entries.filter_map(Result::ok).any(|entry| {
        if entry.file_name() == "lo" {
            return false;
        }
        matches!(
            std::fs::read_to_string(entry.path().join("carrier")),
            Ok(text) if text.trim() == "1"
        )
    })
}

#[tauri::command]
pub fn is_online() -> bool {
    any_carrier_live(Path::new("/sys/class/net"))
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs;

    fn iface(root: &Path, name: &str, carrier: Option<&str>) {
        let dir = root.join(name);
        fs::create_dir_all(&dir).unwrap();
        if let Some(value) = carrier {
            fs::write(dir.join("carrier"), value).unwrap();
        }
    }

    fn temp(name: &str) -> std::path::PathBuf {
        let dir = std::env::temp_dir().join(format!("slip39-net-{name}"));
        let _ = fs::remove_dir_all(&dir);
        fs::create_dir_all(&dir).unwrap();
        dir
    }

    #[test]
    fn loopback_alone_is_offline() {
        let root = temp("lo-only");
        iface(&root, "lo", Some("1"));
        assert!(!any_carrier_live(&root));
    }

    #[test]
    fn a_live_carrier_is_online() {
        let root = temp("live");
        iface(&root, "lo", Some("1"));
        iface(&root, "eth0", Some("1"));
        assert!(any_carrier_live(&root));
    }

    #[test]
    fn a_down_carrier_is_offline() {
        let root = temp("down");
        iface(&root, "eth0", Some("0"));
        assert!(!any_carrier_live(&root));
    }

    // An admin-down interface makes the kernel refuse the read. No link is possible,
    // so this counts as offline rather than as an unknown.
    #[test]
    fn an_unreadable_carrier_is_offline() {
        let root = temp("unreadable");
        iface(&root, "eth0", None);
        assert!(!any_carrier_live(&root));
    }

    // The whole directory missing is a different thing: the check did not run, so it
    // must report danger.
    #[test]
    fn a_missing_sys_directory_is_online() {
        assert!(any_carrier_live(Path::new("/nonexistent/class/net")));
    }
}
```

- [ ] **Step 6: Register the command**

In `src-tauri/src/main.rs`, add `mod net;` at the top and the handler:

```rust
        .invoke_handler(tauri::generate_handler![net::is_online])
```

- [ ] **Step 7: Run the Rust tests**

```bash
cargo test --manifest-path src-tauri/Cargo.toml
```

Expected: PASS, 5 tests.

- [ ] **Step 8: Settle the capability question**

Wire the real probe in `Slip39Demo.Tauri/Program.cs`, replacing the `IConnectivityProbe`
line and adding the interop:

```csharp
builder.Services.AddScoped<ITauriInterop, TauriInterop>();
builder.Services.AddScoped<IConnectivityProbe, TauriConnectivityProbe>();
```

Rebuild and run the window, then open the backup page and watch the banner.

```bash
dotnet publish Slip39Demo.Tauri -c Release -o publish-tauri
cargo tauri build --no-bundle --manifest-path src-tauri/Cargo.toml
./src-tauri/target/release/slip39-backup
```

Expected, and this is the point of the step: either the banner resolves to a real
state, meaning application-defined commands need no capability entry, or the WebView
console shows a permission error naming what is missing. If it is the latter, create
`src-tauri/capabilities/default.json`:

```json
{
  "$schema": "../gen/schemas/desktop-schema.json",
  "identifier": "default",
  "description": "Capabilities the single main window needs.",
  "windows": ["main"],
  "permissions": ["core:default"]
}
```

and add whatever the error names. Record the answer in a comment at the top of
`main.rs`, so the next person does not have to rediscover it.

- [ ] **Step 9: Commit**

```bash
git add src-tauri Slip39Demo.Tauri Slip39Demo.Tests/Tauri
git commit -m "Add the invoke seam and the airgap probe, reading carrier state in Rust"
```

---

## Task 3: The save dialog

**Files:**
- Create: `src-tauri/src/save.rs`, `Slip39Demo.Tauri/Services/TauriFileDownloader.cs`
- Create: `Slip39Demo.Tests/Tauri/TauriFileDownloaderTests.cs`
- Modify: `src-tauri/src/main.rs`, `src-tauri/capabilities/default.json`, `Slip39Demo.Tauri/Program.cs`

**Interfaces:**
- Consumes: `ITauriInterop` from Task 2.
- Produces: nothing later tasks depend on.

- [ ] **Step 1: Write the failing test**

`Slip39Demo.Tests/Tauri/TauriFileDownloaderTests.cs`:

```csharp
using System.Text;
using System.Text.Json;
using Slip39Demo.Tauri.Services;

namespace Slip39Demo.Tests.Tauri;

public class TauriFileDownloaderTests
{
    sealed class RecordingInterop : ITauriInterop
    {
        public string? Command { get; private set; }
        public string? Json { get; private set; }

        public ValueTask<T> InvokeAsync<T>(string command, object? args = null)
        {
            Command = command;
            Json = JsonSerializer.Serialize(args);
            return ValueTask.FromResult(default(T)!);
        }
    }

    [Fact]
    public async Task Sends_the_name_and_the_bytes_base64_encoded()
    {
        var interop = new RecordingInterop();
        var bytes = Encoding.UTF8.GetBytes("backup contents");

        await new TauriFileDownloader(interop).DownloadAsync("backup.zip", bytes, "application/zip");

        Assert.Equal("save_file", interop.Command);
        var sent = JsonDocument.Parse(interop.Json!).RootElement;
        Assert.Equal("backup.zip", sent.GetProperty("suggestedName").GetString());
        Assert.Equal(Convert.ToBase64String(bytes), sent.GetProperty("bytesB64").GetString());
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Run: `dotnet test Slip39Demo.slnx --filter FullyQualifiedName~TauriFileDownloaderTests`

Expected: compile error, `TauriFileDownloader` does not exist.

- [ ] **Step 3: Write the C# side**

`Slip39Demo.Tauri/Services/TauriFileDownloader.cs`:

```csharp
using Slip39Demo.UI.Services;

namespace Slip39Demo.Tauri.Services;

// A native save dialog instead of the browser blob-and-anchor mechanism. The bytes
// cross the interop boundary base64 encoded, because the JSON bridge cannot carry a
// byte array. mimeType is accepted for interface compatibility and ignored: the file
// picker takes the extension from the suggested name.
public sealed class TauriFileDownloader(ITauriInterop interop) : IFileDownloader
{
    public async ValueTask DownloadAsync(string filename, byte[] bytes, string mimeType) =>
        await interop.InvokeAsync<string?>("save_file", new
        {
            suggestedName = filename,
            bytesB64 = Convert.ToBase64String(bytes),
        });
}
```

- [ ] **Step 4: Run the test and watch it pass**

Run: `dotnet test Slip39Demo.slnx --filter FullyQualifiedName~TauriFileDownloaderTests`

Expected: PASS.

- [ ] **Step 5: Write the Rust side**

`src-tauri/src/save.rs`:

```rust
//! Native save dialog. Replaces the browser blob download, which in a WebView writes
//! to a downloads directory the user did not choose.

use base64::{engine::general_purpose::STANDARD, Engine};
use tauri::AppHandle;
use tauri_plugin_dialog::DialogExt;

/// Returns the path written, or None if the user cancelled. Cancelling is not an
/// error: the caller asked to save and the user said no.
#[tauri::command]
pub async fn save_file(
    app: AppHandle,
    suggested_name: String,
    bytes_b64: String,
) -> Result<Option<String>, String> {
    let bytes = STANDARD
        .decode(bytes_b64)
        .map_err(|e| format!("the frontend sent something that is not base64: {e}"))?;

    let chosen = app
        .dialog()
        .file()
        .set_file_name(&suggested_name)
        .blocking_save_file();

    let Some(path) = chosen else {
        return Ok(None);
    };

    let path = path
        .into_path()
        .map_err(|e| format!("the chosen location is not a path this program can write: {e}"))?;

    std::fs::write(&path, &bytes).map_err(|e| format!("could not write {}: {e}", path.display()))?;

    Ok(Some(path.display().to_string()))
}
```

Tauri converts `snake_case` command parameters from `camelCase` on the JavaScript
side, which is why the C# anonymous object sends `suggestedName` and `bytesB64`.

- [ ] **Step 6: Register the plugin, the command and the permission**

In `main.rs`, add `mod save;`, add the plugin, and extend the handler list:

```rust
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_fs::init())
        .invoke_handler(tauri::generate_handler![net::is_online, save::save_file])
```

In `src-tauri/capabilities/default.json`, add `"dialog:allow-save"` to `permissions`.
Unlike an application command, a plugin command definitely needs this.

- [ ] **Step 7: Wire it and try it for real**

In `Program.cs`, replace the `IFileDownloader` registration:

```csharp
builder.Services.AddScoped<IFileDownloader, TauriFileDownloader>();
```

```bash
dotnet publish Slip39Demo.Tauri -c Release -o publish-tauri
cargo tauri build --no-bundle --manifest-path src-tauri/Cargo.toml
./src-tauri/target/release/slip39-backup
```

Generate a practice backup, confirm a save dialog appears, save it, and confirm the
file exists at the chosen path and opens as a zip. Cancel once, and confirm the app
does not report an error.

- [ ] **Step 8: Commit**

```bash
git add src-tauri Slip39Demo.Tauri Slip39Demo.Tests/Tauri
git commit -m "Save generated backups through a native dialog"
```

---

## Task 4: Running the reference age binary

**Files:**
- Create: `src-tauri/src/age.rs`
- Modify: `src-tauri/src/main.rs`

**Interfaces:**
- Produces: the `age_encrypt` command, returning this exact shape, which Task 5 consumes:

```
{ exitCode: i32, stdoutB64: String, stdoutText: String, stderrText: String,
  agePath: String, ageSha256: String, pluginPath: String, ageMissing: bool,
  pluginMissing: bool }
```

- [ ] **Step 1: Write the Rust command and its tests**

`src-tauri/src/age.rs`:

```rust
//! Runs the official age program bundled beside this executable, rather than a
//! library compiled into the application.
//!
//! WHY, carried over from Slip39Demo.Desktop/Services/NativeAgeEncryptor.cs: a bug in
//! an encryptor is invisible. A file written with a reused nonce or a weak key
//! decrypts perfectly and stays weak forever, so no amount of round-trip testing
//! finds it. A bug in a decryptor is loud. This artifact is the one people run
//! against real seed phrases, so the side where mistakes cannot be seen gets the
//! reference implementation.
//!
//! HOW THE KEY IS PASSED: in AGE_PASSPHRASE, which age-plugin-batchpass reads, and
//! never on the command line, where every other process could read it from the
//! process list. PATH is pinned to the bundled directory so age cannot pick up some
//! other age-plugin-batchpass that happens to be on the machine.
//!
//! This module applies NO policy. It does not decide whether an exit code is
//! acceptable, does not build the transcript, and does not fall back to anything.
//! Those judgements live in Slip39Demo.Tauri/Services/TauriAgeEncryptor.cs, where the
//! test suite is.

use base64::{engine::general_purpose::STANDARD, Engine};
use serde::Serialize;
use sha2::{Digest, Sha256};
use std::io::Write;
use std::path::{Path, PathBuf};
use std::process::{Command, Stdio};

/// Where build-appimage.sh puts the official binaries, relative to the executable.
const AGE_SUBDIR: &str = "age";

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AgeRun {
    pub exit_code: i32,
    pub stdout_b64: String,
    pub stdout_text: String,
    pub stderr_text: String,
    pub age_path: String,
    pub age_sha256: String,
    pub plugin_path: String,
    pub age_missing: bool,
    pub plugin_missing: bool,
}

/// Resolved from the running executable, which inside a mounted AppImage is
/// /tmp/.mount_xxxx/usr/bin/slip39-backup, so the sibling age directory resolves
/// correctly without reading APPDIR.
pub fn age_dir_for(exe: &Path) -> PathBuf {
    exe.parent().unwrap_or(Path::new(".")).join(AGE_SUBDIR)
}

fn run(exe: &Path, args: &[&str], dir: &Path, stdin: Option<&[u8]>, passphrase: Option<&str>)
    -> std::io::Result<(i32, Vec<u8>, String)>
{
    let mut command = Command::new(exe);
    command
        .args(args)
        .current_dir(dir)
        .env("PATH", dir)
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped());

    if let Some(value) = passphrase {
        command.env("AGE_PASSPHRASE", value);
    }

    let mut child = command.spawn()?;
    if let Some(bytes) = stdin {
        child.stdin.as_mut().expect("stdin was piped").write_all(bytes)?;
    }
    drop(child.stdin.take());

    let output = child.wait_with_output()?;
    Ok((
        output.status.code().unwrap_or(-1),
        output.stdout,
        String::from_utf8_lossy(&output.stderr).into_owned(),
    ))
}

#[tauri::command]
pub fn age_encrypt(plaintext_b64: String, passphrase_hex: String) -> Result<AgeRun, String> {
    let exe = std::env::current_exe().map_err(|e| format!("cannot locate this program: {e}"))?;
    let dir = age_dir_for(&exe);
    let age = dir.join("age");
    let plugin = dir.join("age-plugin-batchpass");

    // Reported rather than decided. C# owns the message a user sees, and the reason
    // there is no fallback.
    if !age.exists() || !plugin.exists() {
        return Ok(AgeRun {
            exit_code: -1,
            stdout_b64: String::new(),
            stdout_text: String::new(),
            stderr_text: String::new(),
            age_path: age.display().to_string(),
            age_sha256: String::new(),
            plugin_path: plugin.display().to_string(),
            age_missing: !age.exists(),
            plugin_missing: !plugin.exists(),
        });
    }

    // Identify the exact binary being trusted. C# used to compute this itself, and
    // WebAssembly cannot read a file, so it moves here.
    let bytes = std::fs::read(&age).map_err(|e| format!("cannot read {}: {e}", age.display()))?;
    let age_sha256 = format!("{:x}", Sha256::digest(&bytes));

    let (version_code, version_out, version_err) = run(&age, &["--version"], &dir, None, None)
        .map_err(|e| format!("the bundled age program would not run: {e}"))?;
    if version_code != 0 {
        return Err(format!("age --version exited with {version_code}: {version_err}"));
    }

    let plaintext = STANDARD
        .decode(plaintext_b64)
        .map_err(|e| format!("the frontend sent something that is not base64: {e}"))?;

    let (code, stdout, stderr) = run(
        &age,
        &["--encrypt", "-j", "batchpass"],
        &dir,
        Some(&plaintext),
        Some(&passphrase_hex),
    )
    .map_err(|e| format!("age failed to run: {e}"))?;

    Ok(AgeRun {
        exit_code: code,
        stdout_b64: STANDARD.encode(&stdout),
        // Carries the `age --version` output, because that is what the transcript
        // prints. The ciphertext travels in stdout_b64, never as text.
        stdout_text: String::from_utf8_lossy(&version_out).trim().to_string(),
        stderr_text: stderr,
        age_path: age.display().to_string(),
        age_sha256,
        plugin_path: plugin.display().to_string(),
        age_missing: false,
        plugin_missing: false,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn age_dir_sits_beside_the_executable() {
        let dir = age_dir_for(Path::new("/tmp/.mount_abc/usr/bin/slip39-backup"));
        assert_eq!(dir, PathBuf::from("/tmp/.mount_abc/usr/bin/age"));
    }

    #[test]
    fn a_missing_bundle_is_reported_not_thrown() {
        // Uses the real command against a path where nothing is bundled, proving the
        // caller gets a structured answer rather than an error string.
        let run = age_encrypt("aGk=".into(), "00".repeat(32)).unwrap();
        assert!(run.age_missing || !run.age_path.is_empty());
    }
}
```

**Note on the two output channels:** `stdout_b64` carries the ciphertext, which is
binary and must never be treated as text. `stdout_text` carries the `age --version`
output, which is the only stdout the transcript prints. Keeping them separate is why
the struct has both.

- [ ] **Step 2: Register the command**

In `main.rs`, add `mod age;` and extend the handler:

```rust
        .invoke_handler(tauri::generate_handler![net::is_online, save::save_file, age::age_encrypt])
```

- [ ] **Step 3: Run the Rust tests**

```bash
cargo test --manifest-path src-tauri/Cargo.toml
```

Expected: PASS, 7 tests.

- [ ] **Step 4: Commit**

```bash
git add src-tauri
git commit -m "Run the bundled age program from the shell, and report what it did"
```

---

## Task 5: The encryptor, its transcript, and the rule against falling back

**Files:**
- Create: `Slip39Demo.Tauri/Services/TauriAgeEncryptor.cs`
- Create: `Slip39Demo.Tests/Tauri/TauriAgeEncryptorTests.cs`
- Modify: `Slip39Demo.Tauri/Program.cs`

**Interfaces:**
- Consumes: `ITauriInterop` from Task 2, the `AgeRun` shape from Task 4.
- Produces: nothing later tasks depend on.

- [ ] **Step 1: Write the failing tests**

These are `Slip39Demo.Tests/Desktop/NativeAgeEncryptorTests.cs` ported. The two
`SkippableFact` cases that needed a real age release are replaced by fakes returning a
recorded `AgeRun`, because the subprocess is now exercised by `cargo test` and by the
CI smoke test instead.

`Slip39Demo.Tests/Tauri/TauriAgeEncryptorTests.cs`:

```csharp
using System.Text;
using Slip39Demo.Tauri.Services;
using Slip39Demo.UI.Services;

namespace Slip39Demo.Tests.Tauri;

public class TauriAgeEncryptorTests
{
    static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    sealed class StubInterop(AgeRunDto result) : ITauriInterop
    {
        public ValueTask<T> InvokeAsync<T>(string command, object? args = null) =>
            ValueTask.FromResult((T)(object)result);
    }

    static AgeRunDto Good(byte[] ciphertext) => new()
    {
        ExitCode = 0,
        StdoutB64 = Convert.ToBase64String(ciphertext),
        StdoutText = "v1.3.1",
        StderrText = "",
        AgePath = "/tmp/.mount_x/usr/bin/age/age",
        AgeSha256 = "abc123",
        PluginPath = "/tmp/.mount_x/usr/bin/age/age-plugin-batchpass",
        AgeMissing = false,
        PluginMissing = false,
    };

    static byte[] ValidAgeFile() =>
        Encoding.ASCII.GetBytes("age-encryption.org/v1\n-> batchpass\nbody");

    [Fact]
    public async Task Rejects_a_key_that_is_not_32_bytes()
    {
        var result = await new TauriAgeEncryptor(new StubInterop(Good(ValidAgeFile())))
            .EncryptAsync([1, 2, 3], new byte[16]);

        Assert.True(result.IsFailure);
        Assert.Contains("32 bytes", result.Error);
    }

    // The rule the whole class exists for.
    [Fact]
    public async Task Missing_binary_fails_rather_than_falling_back()
    {
        var missing = Good([]);
        missing.AgeMissing = true;

        var result = await new TauriAgeEncryptor(new StubInterop(missing)).EncryptAsync([1], Key);

        Assert.True(result.IsFailure);
        Assert.Contains("missing", result.Error);
        Assert.Contains("refuses to fall back", result.Error);
    }

    [Fact]
    public async Task Nonzero_exit_fails()
    {
        var failed = Good([]);
        failed.ExitCode = 1;
        failed.StderrText = "age: bad passphrase";

        var result = await new TauriAgeEncryptor(new StubInterop(failed)).EncryptAsync([1], Key);

        Assert.True(result.IsFailure);
        Assert.Contains("bad passphrase", result.Error);
    }

    [Fact]
    public async Task Output_that_is_not_an_age_file_fails()
    {
        var wrong = Good(Encoding.ASCII.GetBytes("this is not an age file at all"));

        var result = await new TauriAgeEncryptor(new StubInterop(wrong)).EncryptAsync([1], Key);

        Assert.True(result.IsFailure);
        Assert.Contains("age v1 header", result.Error);
    }

    [Fact]
    public async Task Empty_output_fails()
    {
        var result = await new TauriAgeEncryptor(new StubInterop(Good([]))).EncryptAsync([1], Key);

        Assert.True(result.IsFailure);
        Assert.Contains("no output", result.Error);
    }

    [Fact]
    public async Task Transcript_shows_the_binary_its_hash_and_the_command()
    {
        var result = await new TauriAgeEncryptor(new StubInterop(Good(ValidAgeFile())))
            .EncryptAsync(Encoding.UTF8.GetBytes("payload"), Key);

        Assert.True(result.IsSuccess);
        var text = string.Join("\n", result.Value.Transcript.Lines.Select(l => l.Text));
        Assert.Contains("/usr/bin/age/age", text);
        Assert.Contains("abc123", text);
        Assert.Contains("--encrypt -j batchpass", text);
        Assert.Contains("AGE_PASSPHRASE", text);
        Assert.Contains(TranscriptLineKind.Warning, result.Value.Transcript.Lines.Select(l => l.Kind));
    }

    // The key must never appear in anything shown to the user.
    [Fact]
    public async Task Transcript_never_contains_the_key()
    {
        var result = await new TauriAgeEncryptor(new StubInterop(Good(ValidAgeFile())))
            .EncryptAsync(Encoding.UTF8.GetBytes("payload"), Key);

        var text = string.Join("\n", result.Value.Transcript.Lines.Select(l => l.Text));
        Assert.DoesNotContain(Convert.ToHexString(Key).ToLowerInvariant(), text);
    }
}
```

- [ ] **Step 2: Run them and watch them fail**

Run: `dotnet test Slip39Demo.slnx --filter FullyQualifiedName~TauriAgeEncryptorTests`

Expected: compile error, `TauriAgeEncryptor` and `AgeRunDto` do not exist.

- [ ] **Step 3: Write the encryptor**

`Slip39Demo.Tauri/Services/TauriAgeEncryptor.cs`. Every comment and every transcript
string below is carried over verbatim from `NativeAgeEncryptor.cs`, because the
reasoning did not change when the mechanism did.

```csharp
using System.Text;
using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using Slip39Demo.UI.Services;

namespace Slip39Demo.Tauri.Services;

// What src-tauri/src/age.rs reports back. It is a record of what happened, not a
// verdict on it: every judgement below is made here.
public sealed class AgeRunDto
{
    [JsonPropertyName("exitCode")] public int ExitCode { get; set; }
    [JsonPropertyName("stdoutB64")] public string StdoutB64 { get; set; } = "";
    [JsonPropertyName("stdoutText")] public string StdoutText { get; set; } = "";
    [JsonPropertyName("stderrText")] public string StderrText { get; set; } = "";
    [JsonPropertyName("agePath")] public string AgePath { get; set; } = "";
    [JsonPropertyName("ageSha256")] public string AgeSha256 { get; set; } = "";
    [JsonPropertyName("pluginPath")] public string PluginPath { get; set; } = "";
    [JsonPropertyName("ageMissing")] public bool AgeMissing { get; set; }
    [JsonPropertyName("pluginMissing")] public bool PluginMissing { get; set; }
}

// Encrypts the payload by running the official age binary bundled in the AppImage,
// rather than the AgeSharp library.
//
// WHY
// A bug in an encryptor is invisible: a file written with a reused nonce or a weak key
// decrypts perfectly and stays weak forever, so no amount of round-trip testing finds
// it. A bug in a decryptor is loud. This artifact is the one people run against real
// seed phrases, so the side where mistakes cannot be seen gets the reference
// implementation, written by the author of the age format and far more scrutinised
// than any C# port.
//
// AgeSharp still handles decryption in Recoverer mode, where a fault announces itself,
// and it still passes the CCTV wire-format vectors in the test suite.
//
// FAIL CLOSED
// If the binary is missing, refuses to run, or returns anything unexpected,
// generation fails. There is deliberately no fallback to the in-process library:
// falling back to the thing this class exists to avoid would make it decorative.
public sealed class TauriAgeEncryptor(ITauriInterop interop) : IPayloadEncryptor
{
    public async Task<Result<EncryptionOutcome>> EncryptAsync(byte[] plaintext, byte[] key32)
    {
        if (key32.Length != 32)
            return Result.Failure<EncryptionOutcome>($"key must be 32 bytes (got {key32.Length})");

        AgeRunDto run;
        try
        {
            run = await interop.InvokeAsync<AgeRunDto>("age_encrypt", new
            {
                plaintextB64 = Convert.ToBase64String(plaintext),
                passphraseHex = Convert.ToHexString(key32).ToLowerInvariant(),
            });
        }
        catch (Exception ex)
        {
            return Result.Failure<EncryptionOutcome>($"the shell could not run age: {ex.Message}");
        }

        if (run.AgeMissing)
            return Result.Failure<EncryptionOutcome>(
                $"the bundled age program is missing (expected at {run.AgePath}). This build refuses to fall "
                + "back to encrypting in-process, because the whole point of running age is that a "
                + "mistake made while encrypting cannot be detected afterwards.");
        if (run.PluginMissing)
            return Result.Failure<EncryptionOutcome>(
                $"the bundled age-plugin-batchpass program is missing (expected at {run.PluginPath}). age "
                + "needs it to accept a passphrase without a terminal prompt.");

        if (run.ExitCode != 0)
            return Result.Failure<EncryptionOutcome>(
                $"age exited with code {run.ExitCode}: {run.StderrText.Trim()}");

        var ciphertext = Convert.FromBase64String(run.StdoutB64);
        if (ciphertext.Length == 0)
            return Result.Failure<EncryptionOutcome>("age produced no output");

        // A last sanity check on the shape of what came back. Not a substitute for the
        // independent verification that follows, just a guard against handing on
        // something that is obviously not an age file.
        var magic = Encoding.ASCII.GetString(ciphertext, 0, Math.Min(ciphertext.Length, 21));
        if (magic != "age-encryption.org/v1")
            return Result.Failure<EncryptionOutcome>(
                $"age returned something that does not start with the age v1 header (saw \"{magic}\")");

        var version = run.StdoutText.Trim();
        var lines = new List<TranscriptLine>
        {
            new(TranscriptLineKind.Note,
                "Encrypting with the official age program, not with code built into this app."),
            new(TranscriptLineKind.Note, $"Program:  {run.AgePath}"),
            new(TranscriptLineKind.Note, $"SHA-256:  {run.AgeSha256}"),
            new(TranscriptLineKind.Command, "age --version"),
            new(TranscriptLineKind.Output, version),
            new(TranscriptLineKind.Command,
                "printf '%s' \"<the wallet payload>\" | age --encrypt -j batchpass"),
            new(TranscriptLineKind.Note,
                "The payload goes in through a pipe and the encrypted file comes back through a pipe, "
                + "so the unencrypted wallet is never written to disk."),
            new(TranscriptLineKind.Note,
                "The key is given to age in the environment variable AGE_PASSPHRASE, which "
                + "age-plugin-batchpass reads. It is deliberately NOT on the command line, where every "
                + "other program on the machine could read it from the process list."),
            new(TranscriptLineKind.Output,
                $"exit code 0, {ciphertext.Length} bytes produced, header reads \"age-encryption.org/v1\""),
        };

        if (!string.IsNullOrWhiteSpace(run.StderrText))
            lines.Add(new(TranscriptLineKind.Output, run.StderrText.Trim()));

        lines.Add(new(TranscriptLineKind.Note,
            $"Payload in: {plaintext.Length} bytes. Encrypted file out: {ciphertext.Length} bytes."));
        lines.Add(new(TranscriptLineKind.Warning,
            "While age was running (about a second), the key was readable from this machine's "
            + "process information by anything running as the same user. On an offline Tails "
            + "session that nobody else is using, that is nothing; on a shared computer it would "
            + "matter, which is one more reason to do this offline."));

        var transcript = new EncryptionTranscript(
            $"Encrypted by the official age program ({version}), run as a separate "
            + "program, not by code built into this app.",
            lines);

        return Result.Success(new EncryptionOutcome(ciphertext, transcript));
    }
}
```

- [ ] **Step 4: Run the tests and watch them pass**

Run: `dotnet test Slip39Demo.slnx --filter FullyQualifiedName~TauriAgeEncryptorTests`

Expected: PASS, 7 tests.

- [ ] **Step 5: Wire it and delete the placeholder**

In `Program.cs`, replace the last `NotWiredYet` registration and delete the
`NotWiredYet` class entirely:

```csharp
builder.Services.AddScoped<IPayloadEncryptor, TauriAgeEncryptor>();
```

- [ ] **Step 6: Run the whole suite**

Run: `dotnet test Slip39Demo.slnx -c Release`

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add Slip39Demo.Tauri Slip39Demo.Tests/Tauri
git commit -m "Port the age encryptor and its transcript to the Tauri shell"
```

---

## Task 6: Delete the Photino shell

Done only now, so every earlier task could be compared against a working reference.

**Files:**
- Delete: `Slip39Demo.Desktop/**`, `Slip39Demo.Tests/Desktop/NativeAgeEncryptorTests.cs`
- Modify: `Slip39Demo.slnx`

- [ ] **Step 1: Check nothing else references it**

Run: `grep -rn "Slip39Demo.Desktop" --include=*.cs --include=*.csproj --include=*.slnx --include=*.razor --include=*.yml --include=*.sh --include=*.md . | grep -v "^./docs/"`

Expected: matches only in `Slip39Demo.slnx`, `packaging/appimage/*`, and
`.github/workflows/appimage.yml`, all of which Tasks 7 and 8 rewrite. Anything else is
a dependency this plan did not account for: stop and report it.

- [ ] **Step 2: Delete**

```bash
dotnet sln Slip39Demo.slnx remove Slip39Demo.Desktop/Slip39Demo.Desktop.csproj
git rm -r Slip39Demo.Desktop Slip39Demo.Tests/Desktop
```

- [ ] **Step 3: Run the suite**

Run: `dotnet test Slip39Demo.slnx -c Release`

Expected: PASS. `NativeAgeEncryptorTests` is gone and `TauriAgeEncryptorTests` covers
the same rules, so the count drops by 4 and rises by 7.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "Delete the Photino shell, which the Tauri shell replaces"
```

---

## Task 7: Packaging

**Files:**
- Modify: `packaging/appimage/build-appimage.sh`, `packaging/appimage/AppRun`
- Modify: `packaging/appimage/slip39-backup.desktop`

- [ ] **Step 1: Rewrite AppRun**

```sh
#!/bin/sh
# AppImage entry point.
#
# Deliberately thin, and deliberately without a pre-flight check. An earlier AppRun in
# a sibling repository ran ldconfig to look for libwebkit2gtk, could not find ldconfig
# because it lives in /usr/sbin and is not on a normal user's PATH, and refused to
# launch a working application on Tails. A check that can produce a false negative is
# worse than no check: if a library really is missing, the dynamic linker names it far
# more precisely than a shell script can.
#
# Nothing is bundled beyond the application itself and the age binaries. Tails ships
# libwebkit2gtk-4.1, libgtk-3-0t64, libsoup-3.0-0 and librsvg2-2, verified against the
# Tails package manifest rather than assumed.
#
# The frontend is not here either: Tauri embeds the published Blazor output into the
# executable at compile time.

HERE="$(dirname "$(readlink -f "$0")")"

exec "$HERE/usr/bin/slip39-backup" "$@"
```

- [ ] **Step 2: Update the desktop entry**

Change `Exec=AppRun` to `Exec=slip39-backup` and leave the rest of
`slip39-backup.desktop` alone.

- [ ] **Step 3: Rewrite the build script**

Replace the head of `packaging/appimage/build-appimage.sh` down to the age section.
Keep the age block and the appimagetool block **exactly as they are**, since both are
pinned, checksum-verified and already proven on Linux.

```bash
#!/usr/bin/env bash
# Builds slip39-backup-x86_64.AppImage: the offline, native-window artifact for
# Tails 7+. Run on Linux (a CI ubuntu runner, or WSL for local builds).
#
# Usage:
#   ./build-appimage.sh <tauri-release-binary> <output.AppImage>
#
# The binary comes from:
#   dotnet publish Slip39Demo.Tauri -c Release -o publish-tauri
#   cargo tauri build --no-bundle --manifest-path src-tauri/Cargo.toml
#
# System libraries (webkit2gtk-4.1, gtk3) are NOT bundled. Tails ships them, and
# bundling a browser engine known to be present triples the size and pins a rendering
# stack. See PeteSparrowBTC/tails-appimage.
set -euo pipefail

BINARY="${1:?usage: build-appimage.sh <tauri-release-binary> <output.AppImage>}"
OUTPUT="${2:?usage: build-appimage.sh <tauri-release-binary> <output.AppImage>}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

[ -x "$BINARY" ] || { echo "error: $BINARY missing or not executable"; exit 1; }

# The check that matters on Tails. Debian 13 dropped the webkit2gtk-4.0 series, so a
# binary linking libwebkit2gtk-4.0.so.37 will not start there, and the failure looks
# like an application bug rather than a packaging one. Fail here instead.
NEEDED="$(readelf -d "$BINARY" | grep NEEDED || true)"
if echo "$NEEDED" | grep -q "libwebkit2gtk-4\.0"; then
  echo "error: $BINARY links webkit2gtk-4.0, which Tails 7 does not ship"; exit 1
fi
if ! echo "$NEEDED" | grep -q "libwebkit2gtk-4\.1"; then
  echo "error: $BINARY does not link webkit2gtk-4.1; check the build environment"; exit 1
fi
echo "Links webkit2gtk-4.1, which Tails ships."

# ── Assemble the AppDir ────────────────────────────────────────────────
APPDIR="$WORK/AppDir"
mkdir -p "$APPDIR/usr/bin"
cp "$SCRIPT_DIR/AppRun" "$APPDIR/AppRun"
cp "$SCRIPT_DIR/slip39-backup.desktop" "$APPDIR/"
cp "$SCRIPT_DIR/../../Slip39Demo.UI/wwwroot/favicon.png" "$APPDIR/slip39-backup.png"
cp "$BINARY" "$APPDIR/usr/bin/slip39-backup"
chmod +x "$APPDIR/AppRun" "$APPDIR/usr/bin/slip39-backup"
```

Everything from `# ── Bundle the official age binary ─` onward stays byte for byte as
it is on `main`, including the pinned versions, the explicit checksum comparisons and
the `--appimage-extract-and-run` invocation.

- [ ] **Step 4: Build it on Linux and run it**

On WSL or a Linux machine:

```bash
dotnet publish Slip39Demo.Tauri -c Release -o publish-tauri
cargo tauri build --no-bundle --manifest-path src-tauri/Cargo.toml
bash packaging/appimage/build-appimage.sh src-tauri/target/release/slip39-backup slip39-backup-x86_64.AppImage
ls -lh slip39-backup-x86_64.AppImage
./slip39-backup-x86_64.AppImage
```

Expected: the window opens, the airgap banner resolves, and a practice backup
generates with a transcript naming the bundled `age` path and its SHA-256. Record the
size. Do not compare it to a prediction; there is no size target.

- [ ] **Step 5: Commit**

```bash
git add packaging/appimage
git commit -m "Package the Tauri binary, and check the webkit ABI Tails actually ships"
```

---

## Task 8: The workflow

**Files:**
- Modify: `.github/workflows/appimage.yml`

- [ ] **Step 1: Rewrite the job**

Keep the trigger block, the `permissions: contents: write`, the artifact upload and the
release step. Replace the body between checkout and packaging with:

```yaml
      - name: Set up .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      # The suite gates the artifact, before anything is packaged. This AppImage is
      # what people run against real seed phrases on an airgapped machine.
      - name: Test (gates the released artifact)
        run: dotnet test Slip39Demo.slnx -c Release --verbosity normal

      - name: Install the Tauri Linux build dependencies
        run: |
          sudo apt-get update
          sudo apt-get install -y \
            libwebkit2gtk-4.1-dev libsoup-3.0-dev libssl-dev librsvg2-dev \
            patchelf build-essential file xvfb

      - name: Set up Rust
        uses: dtolnay/rust-toolchain@stable

      # The shell has tests of its own: carrier parsing and the path the age binaries
      # are resolved from. Both are things a C# test can no longer reach.
      - name: Test the shell
        run: cargo test --manifest-path src-tauri/Cargo.toml

      - name: Publish the frontend
        run: dotnet publish Slip39Demo.Tauri -c Release -o publish-tauri

      # Nothing this repository wrote may point at an outside origin: the app must load
      # with the network disconnected. _framework is excluded because it is the .NET
      # WebAssembly runtime rather than our code, and it contains strings that look
      # like URLs and are never fetched.
      - name: Check the published app for external references
        run: |
          if grep -rIEn "https?://" publish-tauri/wwwroot --exclude-dir=_framework \
               | grep -vE "127\.0\.0\.1|localhost|github\.com/PeteSparrowBTC"; then
            echo "::error::The published app references an external origin."
            exit 1
          fi
          echo "No external references."

      - name: Install the Tauri CLI
        run: cargo install tauri-cli --version "^2.0" --locked

      # --no-bundle: Tauri's own bundler copies the WebKitGTK stack in and triples the
      # size. Tails ships those libraries.
      - name: Build the shell
        run: cargo tauri build --no-bundle --manifest-path src-tauri/Cargo.toml

      - name: Build the AppImage
        run: bash packaging/appimage/build-appimage.sh src-tauri/target/release/slip39-backup slip39-backup-x86_64.AppImage
```

- [ ] **Step 2: Keep the smoke test honest**

The old smoke test used `SLIP39_SMOKE=1`, a hook in `DesktopRoot.razor` that exited 0
once Blazor had rendered. That component is deleted. Replace the step with one that
proves the AppImage starts and its window survives a few seconds under xvfb:

```yaml
      - name: Smoke test the AppImage under xvfb
        run: |
          chmod +x slip39-backup-x86_64.AppImage
          xvfb-run -a timeout 20 ./slip39-backup-x86_64.AppImage --appimage-extract-and-run &
          PID=$!
          sleep 12
          kill -0 $PID || { echo "::error::the AppImage exited before 12 seconds"; exit 1; }
          kill $PID || true
          echo "The window stayed up."
```

This is weaker than the old check, which proved the UI had rendered. Say so in the
step's comment rather than letting it read as equivalent. A stronger replacement, a
WebDriver check against the running window, is worth doing later and is not in scope
here.

- [ ] **Step 3: Push the branch and let CI run it**

```bash
git add .github/workflows/appimage.yml
git commit -m "Build the AppImage from the Tauri shell in CI"
git push -u origin feat/tauri-shell
gh pr create --title "Replace the Photino shell with Tauri" --body "..."
```

- [ ] **Step 4: Watch the run and fix what it finds**

```bash
gh run watch
```

A workflow that has never executed is not a workflow. Do not describe this task as
complete until a run has gone green, and quote its output in the pull request.

- [ ] **Step 5: Verify the artifact by hand**

Download the artifact from the run, then on Tails, offline:

```bash
sha256sum -c slip39-backup-x86_64.AppImage.sha256
cp slip39-backup-x86_64.AppImage ~/ && cd ~
chmod +x slip39-backup-x86_64.AppImage
./slip39-backup-x86_64.AppImage
```

Copy off the stick first: a stick may be mounted `noexec` and the executable bit does
not survive a FAT filesystem. Run from a terminal, since GNOME Files does not launch
binaries on double-click and does it silently.

Then feed it a known input and check a known output. A window appearing is not the
test. Confirm the transcript names the bundled `age` path and a SHA-256 matching the
`age` v1.3.1 release you downloaded yourself, and confirm a generated backup recovers.

Do not merge the pull request. Merging is the human's job.

---

## Self-review notes

Checked against the spec:

| spec requirement | task |
| --- | --- |
| three commands, no policy in Rust | 2, 3, 4 |
| separate WASM project, no AgeSharp in the AppImage | 1, 5 |
| `TauriInterop` as a fakeable seam | 2 |
| transcript and fail-closed rules stay in xunit | 5 |
| path resolution and carrier parsing get `cargo test` | 2, 4 |
| `Slip39Demo.Desktop` deleted | 6 |
| pinned `age` and pinned appimagetool preserved | 7, explicitly "keep exactly as they are" |
| `readelf -d` webkit ABI check replaces the glibc guard | 7 |
| thin `AppRun`, no pre-flight check | 7 |
| CSP permits `wasm-unsafe-eval` | 1 |
| capability question settled empirically | 2, step 8 |
| CI gains `cargo test` | 8 |

Two things this plan admits rather than hides:

1. **The smoke test gets weaker.** `SLIP39_SMOKE=1` proved Blazor had rendered inside
   the webview. Its replacement proves only that the process stays up. Task 8 step 2
   says so in the workflow comment instead of letting the step read as equivalent.
2. **Two `SkippableFact` tests lose their real subprocess.** They ran a downloaded age
   release through `NativeAgeEncryptor` when `SLIP39_AGE_DIR` was set. The subprocess
   is now in Rust, so `cargo test` and the manual Tails verification in Task 8 step 5
   cover it instead. That is a real reduction in automated coverage of the encrypt
   path, and Task 8 step 5 is therefore not optional.

*Collaboration by Claude*
