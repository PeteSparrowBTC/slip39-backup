# Native-Window AppImage for Tails (Photino.Blazor / WebKitGTK)

**Date:** 2026-08-01
**Status:** Approved
**Supersedes:** the browser-delivery portion of `2026-05-21-slip39-age-redesign-design.md` (Kestrel host + Tor Browser flow)

## Problem

The current offline AppImage starts a loopback Kestrel server (`Slip39Demo.Host`)
and `xdg-open`s the URL. On Tails — the sole supported target — `xdg-open`
resolves to Tor Browser, which routes even `127.0.0.1` through the Tor SOCKS
proxy. Offline (the intended state), the proxy refuses the connection; the user
sees "the proxy server is refusing connections" and must either flip
`network.proxy.allow_hijacking_localhost` in `about:config` (amnesic — gone
every reboot, and the old "No Proxy for" dialog our docs describe no longer
exists) or carry a second third-party browser (LibreWolf) on the stick. That is
unacceptable friction for a tool whose pitch is "run one file".

## Decision

Replace the server+browser delivery with a **native desktop window**:
Photino.Blazor hosting the existing Razor components in a WebKitGTK window.
No browser, no localhost port, no Tor interaction, no proxy.

### Verified facts this rests on

- Tails 7.10 official package manifest (`tails-amd64-7.10.packages`) ships all
  three libraries Photino.Native links against (its makefile pkg-configs
  `gtk+-3.0 webkit2gtk-4.1 libnotify`):
  `libwebkit2gtk-4.1-0` 2.52.3, `libgtk-3-0t64` 3.24.49, `libnotify4` 0.8.6.
- Photino.Blazor 4.0.13 targets net8.0/net9.0 — consumable from net10.0.
- In Photino, Blazor runs in-process on .NET; the webview only renders and
  bridges JS interop. The `independent-verify.min.js` gate (slip39-js + typage)
  and WebCrypto keep working — WebKitGTK provides both. No browser-WASM anywhere.

### Constraints

- **Tails-only.** If Tails doesn't ship a dependency, we don't use it. We do
  NOT bundle WebKitGTK/GTK/libnotify in the AppImage. The AppImage will not run
  on distros lacking `webkit2gtk-4.1`; that is accepted and documented.
- **Minimum Tails 7.0** (Debian 13, glibc 2.41). Photino.Native.so requires
  glibc ≥ 2.38 — verified empirically: on Tails 6 (Debian 12, glibc 2.36) the
  spike fails with `GLIBC_2.38 not found`. Tails 6 is EOL; supporting it would
  be an anti-feature for a security tool. CI must assert the glibc floor of
  the shipped .so (`objdump -T | grep GLIBC_` max ≤ 2.41) so a Photino bump
  can't silently outgrow what current Tails ships.
- The hosted online demo (Blazor WASM on GitHub Pages, INSECURE-TEST
  watermarked) must keep working unchanged.

### Alternatives considered

- **Keep browser delivery, fix docs** — leaves the amnesic `about:config` hack
  or a second browser binary as the permanent UX on the only supported OS. Rejected.
- **Avalonia native rewrite** — self-contained like Electrum's bundled-Qt
  AppImage, but discards the entire Blazor UI and the browser-grade print
  dialog for recovery kits. Buys nothing on Tails, where WebKitGTK is already
  present. Rejected.
- (Reference point: Tails itself ships Electrum as a Debian package against
  system Qt6 — "use what the OS ships" is the same principle we apply here.)

## Phase 0 — Spike (throwaway)

`tools/spike-photino/`: minimal Photino.Blazor app packaged with the existing
`build-appimage.sh`. One page, three checks:

1. `window.print()` — the recovery-kit print path (print-to-PDF must work).
2. `crypto.subtle.digest` via JS interop — what typage/age needs.
3. Photino native save-file dialog + `File.WriteAllBytesAsync` — writes a test file.

Run manually on the Tails stick. All three pass → proceed to Phase 1. If print
fails → design a native PDF-generation fallback before migrating. The spike is
deleted once the verdict is recorded here.

**Verdict (2026-08-01, Tails 7.10 on the physical stick): ALL THREE PASS.**
Print dialog with print-to-PDF, WebCrypto digest match, native save dialog all
confirmed working. (First attempt on a Tails 6 stick failed with
`GLIBC_2.38 not found` — that produced the Tails 7.0 floor constraint above.)
Phase 1 is a GO.

## Phase 1 — Restructure

| Project | Fate | Contents |
|---|---|---|
| `Slip39Demo.UI` | **new** Razor Class Library | Pages (Index, Owner, Recoverer), Layout, Shared, service interfaces (`IFileDownloader`, `IIndependentVerifier`, `IConnectivityProbe`), `JsIndependentVerifier` (host-agnostic), static assets incl. `independent-verify.min.js`, `connectivity.js`, CSS |
| `Slip39Demo.Web` | shrinks to WASM shell | `Program.cs`, `index.html`, `BrowserFileDownloader`, `JsConnectivityProbe`. GitHub Pages demo unchanged |
| `Slip39Demo.Desktop` | **new** Photino.Blazor shell | window creation, DI registration of desktop implementations |
| `Slip39Demo.Host` | **deleted** | server mode gone entirely |

Desktop service implementations:

- `NativeFileDownloader : IFileDownloader` — Photino save-file dialog, then
  `File.WriteAllBytesAsync`. Replaces the Blob/`<a download>` mechanism.
- `LinuxConnectivityProbe : IConnectivityProbe` — reads
  `/sys/class/net/*/carrier`, ignoring `lo`; any live carrier (`1`) → online →
  INSECURE-TEST watermark path. Carrier, not operstate: idle NIC drivers
  commonly report operstate `unknown`, which false-positived a genuinely
  airgapped Tails machine as online (found in acceptance testing). Fail-safe
  direction as the JS probe: if `/sys` can't be enumerated, report **online**.
  Stronger than `navigator.onLine`: kernel state, not webview state.

## Phase 2 — Packaging & CI

- `AppRun` → exec the self-contained `Slip39Demo.Desktop` binary. No port, no
  `xdg-open`. `SPS_PORT` is gone.
- `build-appimage.sh` bundles Desktop publish output (linux-x64,
  self-contained) + the UI library's static web assets. System webview libs
  are NOT bundled.
- `appimage.yml`: publish Desktop → build AppImage → smoke test on
  ubuntu-latest with `libwebkit2gtk-4.1-0` apt-installed (mirrors Tails's
  library set), under `xvfb-run`. `SPS_SMOKE=1` makes the app exit 0 once the
  Blazor root renders (JS-interop ready callback), exit 1 after 30 s.
  `v*` tag → release publishing unchanged.

## Phase 3 — Docs

- `TAILS_INSTRUCTIONS.md` rewritten: verify sha256 → `chmod +x` → run →
  window opens. LibreWolf, Tor Browser proxy config, and python http.server
  sections deleted.
- README offline section updated to match; `README_OFFLINE.md` browser
  troubleshooting removed.

## Testing & acceptance

- `Slip39Demo.Core` / `Slip39Demo.Tests` untouched — crypto logic does not move.
- WASM demo still builds and deploys → proves the UI extraction broke nothing
  for the browser target.
- CI smoke test proves the Desktop shell renders in a WebKitGTK environment.
- Manual acceptance on the Tails stick (final gate):
  1. AppImage runs from USB, window opens offline, no Tor prompts.
  2. Full owner flow: generate → verified-generation gate passes → shares shown.
  3. Recovery kit prints (print-to-PDF) from the WebKitGTK print dialog.
  4. Share QR codes render.
  5. Save-file dialog writes to the stick.
  6. Recoverer flow round-trips.
  7. With network connected (Tor bootstrapped), generation is watermarked
     INSECURE-TEST — the `LinuxConnectivityProbe` gate fires.

## Risks

- **WebKitGTK print dialog behavior on Tails** — the one genuinely unverified
  behavior; that is exactly what the spike exists to answer.
- **Photino project size** — smaller OSS project than Avalonia; mitigated by
  the thin surface we use (window + dialog + JS bridge) and by the UI living
  in a host-agnostic RCL, so a future host swap is contained to
  `Slip39Demo.Desktop`.
- **Tails major upgrades** (Debian base bumps webkit soname) — caught by the
  CI smoke test matrix when Tails moves; Debian stable churn is low.

*Collaboration by Claude*
