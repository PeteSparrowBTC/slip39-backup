# Restyle from dice-to-seed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Bootstrap 5 and the 273-line dark override sheet in `Slip39Demo.UI` with the hand-written design system from dice-to-seed, deleting roughly 400 KB of vendored CSS and JavaScript from an application that must load with the network disconnected.

**Architecture:** One stylesheet, `Slip39Demo.UI/wwwroot/css/app.css`, defines a palette and a small set of components (`panel`, `banner`, `field`, `mono-block`, `words`, `transcript`). The 11 `.razor` files in `Slip39Demo.UI` are rewritten against those classes. No shell, service, or `Slip39Demo.Core` code is touched. `Slip39Demo.Web` and `Slip39Demo.Desktop` inherit the result through their existing `<link>` to the shared stylesheet.

**Tech Stack:** Blazor (net10.0), Razor class library, plain CSS with custom properties, bUnit + xunit for tests.

This plan implements the styling half of
[docs/specs/2026-08-09-tauri-shell-and-styling-design.md](../specs/2026-08-09-tauri-shell-and-styling-design.md).
It is PR A, and it is independent of the Tauri work in PR B.

## Global Constraints

- **No em dashes and no en dashes** anywhere: prose, comments, commit messages. Use a colon, semicolon, comma, parentheses, or a sentence break. (`CLAUDE.md`)
- **No network references.** No web font, no `@import`, no CDN, no remote image. The app must render identically with the cable out.
- **Colour is never the only encoding.** Every state that a user must not misread (online versus airgapped, real output versus `INSECURE-TEST`) is signalled by wording and by border style as well as by hue.
- **Palette, exact values:**
  `--bg #14161a`, `--panel #1c1f26`, `--panel-edge #2c313b`, `--ink #e6e8ec`, `--ink-dim #99a1b0`, `--accent #ffc53d`, `--ok #3fbf7f`, `--warn #ff9f43`, `--bad #ff5c5c`
- **Fonts:** `--sans: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif` and `--mono: ui-monospace, "Cascadia Mono", "Consolas", "DejaVu Sans Mono", monospace`. System fonts only.
- **Do not touch** `Slip39Demo.Core`, any `Services/` class, `independent-verify.min.js`, or any `@code` block. This PR changes markup and CSS only.
- Commit after every task. Never push to `main`, never merge a PR.
- Branch: `feat/restyle-dice-to-seed`, based on `origin/main`.

---

## File Structure

| file | responsibility | change |
| --- | --- | --- |
| `Slip39Demo.UI/wwwroot/css/app.css` | the entire design system | rewritten, 273 lines to ~300 |
| `Slip39Demo.UI/wwwroot/lib/bootstrap/**` | vendored framework | **deleted** |
| `Slip39Demo.UI/Layout/MainLayout.razor` | page shell | simplified |
| `Slip39Demo.UI/Layout/MainLayout.razor.css` | shell scoping | deleted, folded into `app.css` |
| `Slip39Demo.UI/Layout/NavMenu.razor` + `.css` | sidebar nav for a one-page app | **deleted** |
| `Slip39Demo.UI/Pages/Index.razor` | landing, two route cards | rewritten |
| `Slip39Demo.UI/Pages/Owner.razor` | backup form, lines 1 to 177 only | rewritten |
| `Slip39Demo.UI/Pages/Recoverer.razor` | recovery form | rewritten |
| `Slip39Demo.UI/Shared/ConnectivityBanner.razor` | airgap indicator | rewritten, dual encoding |
| `Slip39Demo.UI/Shared/MnemonicInput.razor` | seed word entry | rewritten |
| `Slip39Demo.UI/Shared/CiphertextInput.razor` | payload.age entry | rewritten |
| `Slip39Demo.UI/Shared/CosignerEditor.razor` | per-cosigner fields | rewritten |
| `Slip39Demo.UI/Shared/GroupConfigEditor.razor` | SLIP-39 group table | rewritten |
| `Slip39Demo.UI/Shared/RecoveredPayloadView.razor` | recovery result | rewritten |
| `Slip39Demo.Web/wwwroot/index.html` | WASM host page | bootstrap `<link>` removed |
| `Slip39Demo.Desktop/wwwroot/index.html` | Photino host page | bootstrap `<link>` removed |
| `Slip39Demo.Tests/Ui/StylesheetContractTests.cs` | guard | **new** |
| `Slip39Demo.Tests/Ui/ConnectivityBannerTests.cs` | dual-encoding proof | **new** |

`NavMenu` is deleted rather than restyled. It renders a sidebar containing exactly one link, to the page the user is already on, and `MainLayout` does not reference it. Restyling dead markup would be work spent on something no user sees.

---

## Task 1: The stylesheet

**Files:**
- Create: `Slip39Demo.Tests/Ui/StylesheetContractTests.cs`
- Modify: `Slip39Demo.UI/wwwroot/css/app.css` (replace entirely)
- Modify: `Slip39Demo.Web/wwwroot/index.html:10-11`
- Modify: `Slip39Demo.Desktop/wwwroot/index.html:9-10`

**Interfaces:**
- Consumes: nothing.
- Produces: the class vocabulary every later task uses. Exact names:
  `app`, `panel`, `panel-header`, `panel-body`, `banner`, `banner-ok`, `banner-warn`, `banner-loud`, `field`, `field-label`, `hint`, `hint-loud`, `input`, `input-mono`, `btn`, `btn-primary`, `btn-danger`, `btn-sm`, `btn-block`, `split`, `split-sticky`, `cols`, `row-between`, `mono-block`, `words`, `transcript`, `t-command`, `t-output`, `t-warning`, `t-note`, `check`, `spinner`, `section-label`, `subtitle`, `page-head`.

  This list is the contract, and Task 1 tests it name by name. Do not add a class to
  it speculatively: `btn-ghost` was listed here in the first draft, no task ever
  consumed it, and defining it would have shipped dead CSS to satisfy a list.

- [ ] **Step 1: Write the failing test**

Create `Slip39Demo.Tests/Ui/StylesheetContractTests.cs`:

```csharp
using System.Text.RegularExpressions;

namespace Slip39Demo.Tests.Ui;

// The stylesheet is the whole design system now, so a few things about it are
// worth asserting rather than assuming. This is the same instinct as the
// conformance vectors elsewhere in this suite: a claim nobody checks is a claim
// that drifts.
public class StylesheetContractTests
{
    // Walks up from the test binary to the repository root, so the test does not
    // care whether it runs from bin/Debug, bin/Release, or a CI working directory.
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Slip39Demo.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    static string AppCss() =>
        File.ReadAllText(Path.Combine(RepoRoot(), "Slip39Demo.UI", "wwwroot", "css", "app.css"));

    [Theory]
    [InlineData("--bg", "#14161a")]
    [InlineData("--panel", "#1c1f26")]
    [InlineData("--panel-edge", "#2c313b")]
    [InlineData("--ink", "#e6e8ec")]
    [InlineData("--ink-dim", "#99a1b0")]
    [InlineData("--accent", "#ffc53d")]
    [InlineData("--ok", "#3fbf7f")]
    [InlineData("--warn", "#ff9f43")]
    [InlineData("--bad", "#ff5c5c")]
    public void Palette_defines_the_agreed_value(string name, string value) =>
        Assert.Matches(new Regex($@"{Regex.Escape(name)}:\s*{Regex.Escape(value)}\s*;"), AppCss());

    // An offline tool that reaches for a font or a stylesheet on the network
    // renders differently on the airgapped machine than it did in review, and
    // the reviewer never sees it.
    [Fact]
    public void Stylesheet_references_nothing_on_the_network()
    {
        var offenders = Regex.Matches(AppCss(), @"@import|https?://|//fonts\.")
            .Select(m => m.Value)
            .Distinct()
            .ToArray();

        Assert.True(offenders.Length == 0, $"app.css reaches the network: {string.Join(", ", offenders)}");
    }

    // Both host pages must stop linking the vendored framework. Checked here
    // rather than left to review, because a stray link would silently restore
    // 400 KB and the old cascade.
    [Theory]
    [InlineData("Slip39Demo.Web")]
    [InlineData("Slip39Demo.Desktop")]
    public void Host_page_does_not_link_bootstrap(string project)
    {
        var html = File.ReadAllText(Path.Combine(RepoRoot(), project, "wwwroot", "index.html"));
        Assert.DoesNotContain("bootstrap", html, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 2: Run the test and watch it fail**

Run: `dotnet test Slip39Demo.slnx --filter FullyQualifiedName~StylesheetContractTests`

Expected: the palette theories fail (`app.css` currently defines `--dark-bg` and friends) and both `Host_page_does_not_link_bootstrap` cases fail.

- [ ] **Step 3: Replace the stylesheet**

Replace the entire contents of `Slip39Demo.UI/wwwroot/css/app.css`:

```css
/* The whole design system. Adapted from PeteSparrowBTC/dice-to-seed, which
   established these values, with the components this application needs and that
   one did not: forms, a two-column split, and the encryption transcript.

   Every font here is a system font. No web font, no CDN, no @import: the app has
   to render identically with the network cable out. */

:root {
    --bg: #14161a;
    --panel: #1c1f26;
    --panel-edge: #2c313b;
    --ink: #e6e8ec;
    --ink-dim: #99a1b0;
    --accent: #ffc53d;
    --ok: #3fbf7f;
    --warn: #ff9f43;
    --bad: #ff5c5c;
    --sunken: #0f1115;
    --mono: ui-monospace, "Cascadia Mono", "Consolas", "DejaVu Sans Mono", monospace;
    --sans: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
}

* { box-sizing: border-box; }

body {
    margin: 0;
    background: var(--bg);
    color: var(--ink);
    font-family: var(--sans);
    line-height: 1.5;
}

.app {
    max-width: 68rem;
    margin: 0 auto;
    padding: 1.5rem 1rem 4rem;
}

h1 { font-size: 1.6rem; margin: 0 0 .25rem; }
h2 { font-size: 1.25rem; margin: 0 0 .5rem; }

.subtitle { color: var(--ink-dim); margin: 0 0 1.5rem; }

.section-label {
    color: var(--ink-dim);
    font-size: .85rem;
    text-transform: uppercase;
    letter-spacing: .06em;
    margin: 1.5rem 0 .5rem;
}

.page-head {
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: 1rem;
    margin-bottom: 1rem;
}

/* ---------------------------------------------------------------------------
   Panels: the container that replaces Bootstrap's card.
   --------------------------------------------------------------------------- */
.panel {
    background: var(--panel);
    border: 1px solid var(--panel-edge);
    border-radius: .5rem;
    margin-bottom: 1rem;
}

.panel-header {
    padding: .7rem 1.1rem;
    border-bottom: 1px solid var(--panel-edge);
    font-weight: 600;
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: 1rem;
}

.panel-body { padding: 1rem 1.1rem; }

/* ---------------------------------------------------------------------------
   Banners: the container that replaces Bootstrap's alert.

   Four states, and each is distinguishable without colour. The neutral and ok
   states are plain, warn carries a left rule, and loud is the only one with a
   thick double border. That ordering is deliberate: the state a reader must
   never miss is the one whose shape differs most.
   --------------------------------------------------------------------------- */
.banner {
    border-radius: .5rem;
    padding: .8rem 1rem;
    margin-bottom: 1rem;
    border: 1px solid var(--panel-edge);
    background: var(--panel);
}

.banner-ok { border-color: var(--ok); color: var(--ok); }

.banner-warn {
    border-color: var(--warn);
    border-left: 4px solid var(--warn);
    background: #2a1f0d;
}

.banner-loud {
    border: 3px double var(--bad);
    background: #3a1414;
    color: var(--ink);
    font-weight: 600;
}

.banner strong { font-weight: 700; }

/* ---------------------------------------------------------------------------
   Forms
   --------------------------------------------------------------------------- */
.field { margin-bottom: .9rem; }

.field-label {
    display: block;
    font-weight: 600;
    font-size: .9rem;
    margin-bottom: .3rem;
}

.hint { color: var(--ink-dim); font-size: .85rem; margin: .3rem 0 0; }

/* The one line that has to survive being skim-read. */
.hint-loud {
    color: var(--ink);
    border-left: 3px solid var(--accent);
    padding-left: .75rem;
}

.input {
    font: inherit;
    width: 100%;
    padding: .45rem .6rem;
    border-radius: .35rem;
    border: 1px solid var(--panel-edge);
    background: var(--sunken);
    color: var(--ink);
}

.input:focus {
    outline: none;
    border-color: var(--accent);
}

.input::placeholder { color: var(--ink-dim); }

.input-mono { font-family: var(--mono); }

.check {
    display: flex;
    align-items: flex-start;
    gap: .5rem;
    margin-bottom: .75rem;
    font-size: .9rem;
}

.check input { margin-top: .25rem; accent-color: var(--accent); }

/* ---------------------------------------------------------------------------
   Buttons
   --------------------------------------------------------------------------- */
.btn {
    font: inherit;
    padding: .5rem 1.1rem;
    border-radius: .4rem;
    border: 1px solid var(--panel-edge);
    background: transparent;
    color: var(--ink);
    cursor: pointer;
    text-decoration: none;
    display: inline-block;
}

.btn:hover:not(:disabled) { border-color: var(--accent); color: var(--accent); }
.btn:disabled { color: var(--ink-dim); cursor: not-allowed; }

.btn-primary {
    font-weight: 600;
    border-color: var(--accent);
    background: var(--accent);
    color: #14161a;
}

.btn-primary:hover:not(:disabled) { background: #ffd166; border-color: #ffd166; color: #14161a; }

.btn-primary:disabled {
    background: transparent;
    border-color: var(--panel-edge);
    color: var(--ink-dim);
}

.btn-danger:hover:not(:disabled) { border-color: var(--bad); color: var(--bad); }

.btn-sm { padding: .3rem .7rem; font-size: .85rem; }

.btn-block { width: 100%; text-align: center; }

/* ---------------------------------------------------------------------------
   Layout: replaces the Bootstrap grid.

   .split is the two-column page (form on the left, action on the right) and
   collapses to one column below 62rem. .cols is a generic auto-fitting row for
   the group editor.
   --------------------------------------------------------------------------- */
.split {
    display: grid;
    grid-template-columns: 1fr;
    gap: 1rem;
    align-items: start;
}

@media (min-width: 62rem) {
    .split { grid-template-columns: 7fr 5fr; }
    .split-sticky { position: sticky; top: 1.25rem; }
}

.cols {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(9rem, 1fr));
    gap: .6rem;
    align-items: end;
}

.row-between {
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: 1rem;
}

/* ---------------------------------------------------------------------------
   Output
   --------------------------------------------------------------------------- */
.mono-block {
    font-family: var(--mono);
    font-size: .9rem;
    word-break: break-all;
    background: var(--sunken);
    border: 1px solid var(--panel-edge);
    border-radius: .4rem;
    padding: .7rem .8rem;
    margin: 0;
    white-space: pre-wrap;
    user-select: all;
}

/* Numbered word grid for mnemonics. The number is part of the content rather
   than something the reader counts, because losing your place in a 24 word list
   is the mistake that costs the wallet. */
.words {
    list-style: none;
    counter-reset: word;
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(11rem, 1fr));
    gap: .35rem .75rem;
    padding: 0;
    margin: 0;
}

.words li {
    counter-increment: word;
    font-family: var(--mono);
    font-size: 1.05rem;
    padding: .3rem .5rem;
    background: var(--sunken);
    border: 1px solid var(--panel-edge);
    border-radius: .3rem;
}

.words li::before {
    content: counter(word) ".";
    color: var(--ink-dim);
    display: inline-block;
    min-width: 2.2rem;
}

/* The encryption transcript, rendered as a terminal log. The reader sees the
   command that ran and what it printed back, rather than a claim that something
   happened. Each line kind carries a prefix as well as a colour, so the
   distinction survives a grayscale screenshot. */
.transcript {
    font-family: var(--mono);
    font-size: .85rem;
    background: var(--sunken);
    border: 1px solid var(--panel-edge);
    border-radius: .4rem;
    padding: .8rem .9rem;
    white-space: pre-wrap;
    word-break: break-word;
}

.t-command { color: var(--accent); }
.t-output  { color: var(--ok); }
.t-warning { color: var(--warn); }
.t-note    { color: var(--ink-dim); }

/* ---------------------------------------------------------------------------
   Misc
   --------------------------------------------------------------------------- */
code {
    font-family: var(--mono);
    background: var(--sunken);
    border-radius: .25rem;
    padding: .1rem .3rem;
}

a { color: var(--accent); }

.spinner {
    display: inline-block;
    width: .8rem;
    height: .8rem;
    margin-right: .4rem;
    border: 2px solid var(--ink-dim);
    border-top-color: transparent;
    border-radius: 50%;
    animation: spin .7s linear infinite;
}

@keyframes spin { to { transform: rotate(360deg); } }

/* Blazor's own error strip, kept because a silent failure in a tool that guards
   seed phrases is worse than a visible one. */
#blazor-error-ui {
    background: var(--bad);
    color: #fff;
    bottom: 0;
    display: none;
    left: 0;
    padding: .6rem 1.25rem;
    position: fixed;
    width: 100%;
    z-index: 1000;
}

#blazor-error-ui .dismiss { cursor: pointer; position: absolute; right: .75rem; top: .5rem; }

.loading-progress {
    position: absolute;
    display: block;
    width: 8rem;
    height: 8rem;
    inset: 20vh 0 auto 0;
    margin: 0 auto;
}

.loading-progress circle {
    fill: none;
    stroke: var(--panel-edge);
    stroke-width: .6rem;
    transform-origin: 50% 50%;
    transform: rotate(-90deg);
}

.loading-progress circle:last-child {
    stroke: var(--accent);
    stroke-dasharray: calc(3.141 * var(--blazor-load-percentage, 0%) * 0.8), 500%;
    transition: stroke-dasharray .05s ease-in-out;
}

.loading-progress-text {
    position: absolute;
    text-align: center;
    font-weight: bold;
    inset: calc(20vh + 3.25rem) 0 auto .2rem;
    color: var(--ink-dim);
}

.loading-progress-text:after { content: var(--blazor-load-percentage-text, "Loading"); }
```

- [ ] **Step 4: Remove the bootstrap link from both host pages**

In `Slip39Demo.Web/wwwroot/index.html`, delete this line:

```html
    <link rel="stylesheet" href="_content/Slip39Demo.UI/lib/bootstrap/dist/css/bootstrap.min.css" />
```

In `Slip39Demo.Desktop/wwwroot/index.html`, delete this line:

```html
    <link rel="stylesheet" href="_content/Slip39Demo.UI/lib/bootstrap/dist/css/bootstrap.min.css" />
```

Leave every other line in both files alone, including the `independent-verify.min.js` script tag and its comment.

- [ ] **Step 5: Run the test and watch it pass**

Run: `dotnet test Slip39Demo.slnx --filter FullyQualifiedName~StylesheetContractTests`

Expected: PASS, 12 tests.

- [ ] **Step 6: Commit**

```bash
git add Slip39Demo.UI/wwwroot/css/app.css Slip39Demo.Web/wwwroot/index.html Slip39Demo.Desktop/wwwroot/index.html Slip39Demo.Tests/Ui/StylesheetContractTests.cs
git commit -m "Replace the Bootstrap override sheet with the dice-to-seed system"
```

---

## Task 2: The airgap banner, and the rule that colour is not enough

`ConnectivityBanner` is the one component where the styling carries a security
meaning, so it gets a test rather than a look.

**Files:**
- Create: `Slip39Demo.Tests/Ui/ConnectivityBannerTests.cs`
- Modify: `Slip39Demo.UI/Shared/ConnectivityBanner.razor:8-24` (markup only, leave `@code` untouched)

**Interfaces:**
- Consumes: `banner`, `banner-ok`, `banner-loud` from Task 1.
- Produces: nothing later tasks depend on.

- [ ] **Step 1: Write the failing test**

Create `Slip39Demo.Tests/Ui/ConnectivityBannerTests.cs`:

```csharp
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Slip39Demo.UI.Services;
using Slip39Demo.UI.Shared;

namespace Slip39Demo.Tests.Ui;

public class ConnectivityBannerTests : TestContext
{
    sealed class StubProbe(bool online) : IConnectivityProbe
    {
        public Task<bool> IsOnlineAsync() => Task.FromResult(online);
    }

    ConnectivityBanner RenderWith(bool online)
    {
        Services.AddScoped<IConnectivityProbe>(_ => new StubProbe(online));
        return RenderComponent<ConnectivityBanner>().Instance;
    }

    IRenderedComponent<ConnectivityBanner> RenderMarkup(bool online)
    {
        Services.AddScoped<IConnectivityProbe>(_ => new StubProbe(online));
        return RenderComponent<ConnectivityBanner>();
    }

    // The online state is the one a reader must never skim past, so it must be
    // distinguishable without colour. Two independent encodings are asserted:
    // the word ONLINE in the text, and the banner-loud class, which is the only
    // banner style with a double border.
    [Fact]
    public void Online_state_says_so_in_words_and_in_shape()
    {
        var cut = RenderMarkup(online: true);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("ONLINE", cut.Markup);
            Assert.Contains("banner-loud", cut.Markup);
        });
    }

    [Fact]
    public void Offline_state_says_so_in_words()
    {
        var cut = RenderMarkup(online: false);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("offline", cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("banner-ok", cut.Markup);
        });
    }

    // banner-loud must never be reachable in the safe state, because a reader
    // who learns that the loud shape means danger has to be able to rely on it.
    //
    // The positive assertion is load-bearing, not decoration. "banner-loud" is
    // absent from the pre-resolution render too, so a lone NotContain would pass
    // before the probe ever resolved, and would keep passing if the component got
    // stuck on "Checking network status" forever. Requiring banner-ok in the same
    // block forces the wait past the neutral state first.
    [Fact]
    public void Offline_state_never_uses_the_loud_shape()
    {
        var cut = RenderMarkup(online: false);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("banner-ok", cut.Markup);
            Assert.DoesNotContain("banner-loud", cut.Markup);
        });
    }
}
```

- [ ] **Step 2: Run the test and watch it fail**

Run: `dotnet test Slip39Demo.slnx --filter FullyQualifiedName~ConnectivityBannerTests`

Expected: FAIL. The component currently emits `alert alert-danger` and `alert alert-success`, so every assertion about `banner-*` fails.

- [ ] **Step 3: Rewrite the markup**

Replace lines 8 to 24 of `Slip39Demo.UI/Shared/ConnectivityBanner.razor` with:

```razor
@if (isOnline is null)
{
    <div class="banner">Checking network status…</div>
}
else if (isOnline == true)
{
    <div class="banner banner-loud">
        <strong>⚠ This machine is ONLINE.</strong> Disconnect from the internet
        (ideally boot Tails and stay offline) before entering any seed.
        Backups generated while online are marked <strong>INSECURE-TEST</strong>
        and must not be used for real funds.
    </div>
}
else
{
    <div class="banner banner-ok">✓ No internet reachable. This machine is offline.</div>
}
```

Leave the `@using`, `@implements`, the comment block at lines 4 to 7, and the whole `@code` block exactly as they are.

Note the copy change on the offline line: the original used a dash to join two clauses, and the house style forbids it.

- [ ] **Step 4: Run the test and watch it pass**

Run: `dotnet test Slip39Demo.slnx --filter FullyQualifiedName~ConnectivityBannerTests`

Expected: PASS, 3 tests.

- [ ] **Step 5: Commit**

```bash
git add Slip39Demo.UI/Shared/ConnectivityBanner.razor Slip39Demo.Tests/Ui/ConnectivityBannerTests.cs
git commit -m "Restyle the airgap banner, and test that its danger state is not colour alone"
```

---

## Task 3: Layout, and deleting the dead sidebar

**Files:**
- Modify: `Slip39Demo.UI/Layout/MainLayout.razor`
- Delete: `Slip39Demo.UI/Layout/MainLayout.razor.css`
- Delete: `Slip39Demo.UI/Layout/NavMenu.razor`
- Delete: `Slip39Demo.UI/Layout/NavMenu.razor.css`

**Interfaces:**
- Consumes: `app` from Task 1.
- Produces: every page now renders inside `<main class="app">`, so pages must not add their own outer container.

- [ ] **Step 1: Confirm the sidebar is unreferenced**

Run: `grep -rn "NavMenu" --include=*.razor --include=*.cs Slip39Demo.UI Slip39Demo.Web Slip39Demo.Desktop Slip39Demo.Tests`

Expected: matches only inside `NavMenu.razor` and `NavMenu.razor.css` themselves. If anything else references it, stop and report rather than deleting.

- [ ] **Step 2: Replace MainLayout**

Replace the entire contents of `Slip39Demo.UI/Layout/MainLayout.razor`:

```razor
@inherits LayoutComponentBase

@* One application, two pages, no sidebar. The template's nav rendered a single
   link to the page the reader was already on, and nothing referenced it. *@
<main class="app">
    @Body
</main>
```

- [ ] **Step 3: Delete the three dead files**

```bash
git rm Slip39Demo.UI/Layout/MainLayout.razor.css Slip39Demo.UI/Layout/NavMenu.razor Slip39Demo.UI/Layout/NavMenu.razor.css
```

- [ ] **Step 4: Build and run the whole suite**

Run: `dotnet test Slip39Demo.slnx -c Release`

Expected: PASS. Nothing in the suite renders `MainLayout` or `NavMenu`, so this is a build check plus a regression check on the existing bUnit tests.

- [ ] **Step 5: Commit**

```bash
git add Slip39Demo.UI/Layout
git commit -m "Reduce the layout to a single main element, and delete the unused sidebar"
```

---

## Task 4: The landing page

**Files:**
- Modify: `Slip39Demo.UI/Pages/Index.razor` (replace lines 5 to 49)

**Interfaces:**
- Consumes: `panel`, `panel-body`, `btn`, `btn-primary`, `subtitle`, `hint`, `split` from Task 1.
- Produces: nothing.

- [ ] **Step 1: Replace the markup**

Keep line 1 (`@page "/"`) and line 3 (`<PageTitle>`) exactly as they are. Replace everything from line 5 to the end of the file with:

```razor
<h1>🔐 SLIP-39 + age wallet backup</h1>
<p class="subtitle">
    Secure your BIP-39 wallet with threshold secret-sharing and age encryption.
    Every operation runs on this machine; nothing is sent anywhere.
</p>

<div class="split">
    <div class="panel">
        <div class="panel-body">
            <h2>📤 Create backup</h2>
            <p class="hint">
                Enter your seed words, choose a threshold, and the tool produces a single
                dated backup zip containing your SLIP-39 share folders and the
                encrypted <code>payload.age</code> blob.
            </p>
            <a href="/owner" class="btn btn-primary">Start backup →</a>
        </div>
    </div>

    <div class="panel">
        <div class="panel-body">
            <h2>📥 Recover wallet</h2>
            <p class="hint">
                Gather threshold-many SLIP-39 mnemonics and the <code>payload.age</code>
                ciphertext, then reconstruct your seed words and passphrase here.
            </p>
            <a href="/recoverer" class="btn">Start recovery →</a>
        </div>
    </div>
</div>

<p class="hint" style="margin-top:2rem">
    Source, specification and the manual recovery procedure:
    <a href="https://github.com/PeteSparrowBTC/slip39-backup">the repository</a>.
</p>
```

Two content changes are deliberate. The old copy claimed "100% client-side" next to a
link to the open internet; the replacement states the property without the marketing
number. The "Phase 1 backend tests pass. Phase 2 wires this UI" line is removed, since
it describes a development status that stopped being true.

- [ ] **Step 2: Build**

Run: `dotnet build Slip39Demo.slnx -c Release`

Expected: PASS.

- [ ] **Step 3: Look at it**

Run: `dotnet run --project Slip39Demo.Web` in the background, open the printed URL, and confirm two panels sit side by side above 62rem and stack below it.

- [ ] **Step 4: Commit**

```bash
git add Slip39Demo.UI/Pages/Index.razor
git commit -m "Restyle the landing page"
```

---

## Task 5: The backup page

**Files:**
- Modify: `Slip39Demo.UI/Pages/Owner.razor` lines 17 to 177 only

**Interfaces:**
- Consumes: `panel`, `panel-header`, `panel-body`, `split`, `split-sticky`, `field`, `field-label`, `input`, `input-mono`, `banner`, `banner-ok`, `banner-warn`, `banner-loud`, `check`, `btn`, `btn-primary`, `btn-sm`, `btn-block`, `transcript`, `t-command`, `t-output`, `t-warning`, `t-note`, `page-head`, `section-label`, `hint`, `spinner` from Task 1.
- Produces: nothing.

**Expect to touch `Slip39Demo.Tests/Web/OwnerFormValidationTests.cs` as well.** It selects
elements by the Bootstrap classes this task renames, so it will fail until its selectors
follow. Select by something the restyle cannot move: the input's unique `placeholder`, as
that file already does elsewhere. Do not select by CSS class. A class-based selector here
silently resolved to a cosigner's input instead of the page's own once the classes diverged,
and it would break again the moment Task 7 migrates `CosignerEditor`.

**Do not touch lines 179 to 495.** That is the `@code` block, and this PR changes no behaviour.

- [ ] **Step 1: Replace the page shell and metadata panel**

Replace lines 17 to 45 with:

```razor
<div class="page-head">
    <h1>📤 Create backup</h1>
    <a href="/" class="btn btn-sm">← Home</a>
</div>

<ConnectivityBanner OnlineChanged="v => machineOnline = v" />

<div class="split">
    <div>
        <div class="panel">
            <div class="panel-header">Wallet metadata</div>
            <div class="panel-body">
                <div class="field">
                    <label class="field-label">Label</label>
                    <input class="input" @bind="model.Label" placeholder="e.g. Main wallet 2026" />
                </div>
                <div class="field">
                    <label class="field-label">
                        Top-level seed words (shared-seed mode; leave empty for per-cosigner seeds)
                    </label>
                    <input class="input input-mono" @bind="model.TopLevelSeedWords"
                           placeholder="abandon ability able about above absent absorb abstract absurd abuse access accident" />
                </div>
                <div class="field">
                    <label class="field-label">Descriptor (multisig only, optional)</label>
                    <input class="input input-mono" @bind="model.Descriptor"
                           placeholder="wsh(sortedmulti(2, ...))" />
                </div>
            </div>
        </div>
```

- [ ] **Step 2: Replace the cosigner section**

Replace lines 47 to 74 with, keeping the `@key` comment intact because it records a real bug:

```razor
        <p class="section-label">Cosigners</p>
        @for (var i = 0; i < model.Cosigners.Count; i++)
        {
            var idx = i;
            // @key ties each editor to its cosigner object. Without it, Blazor
            // matches editors by position; once the form re-renders on every edit
            // (live fingerprints/warnings), that mis-associates state and typed
            // passphrases/seeds fail to commit for the 2nd+ cosigner.
            <CosignerEditor @key="model.Cosigners[idx]" Vm="model.Cosigners[idx]" Index="idx"
                            TopLevelSeed="@model.TopLevelSeedWords"
                            Removable="model.Cosigners.Count > 1"
                            OnChanged="StateHasChanged"
                            OnRemove="() => RemoveCosigner(idx)" />
        }
        <button class="btn btn-sm" @onclick="AddCosigner">+ Add cosigner</button>

        @* Live sanity checks on the cosigner set. Two cosigners deriving from the
           same secret cannot add multisig security, so surface that before the user
           commits a broken backup. Non-blocking: warns, does not prevent Generate. *@
        @foreach (var w in CosignerWarnings())
        {
            <div class="banner @(w.Strong ? "banner-loud" : "banner-warn")">@w.Message</div>
        }

        <GroupConfigEditor Groups="model.Groups"
                           GroupThreshold="model.GroupThreshold"
                           GroupThresholdChanged="v => model.GroupThreshold = v" />
    </div>
```

- [ ] **Step 3: Replace the generate panel**

Replace lines 76 to 130 with:

```razor
    <div class="split-sticky">
        <div class="panel">
            <div class="panel-header">Generate</div>
            <div class="panel-body">
                <p class="hint">
                    Clicking <strong>Generate</strong> creates a random 32-byte key, SLIP-39-splits
                    it, encrypts the wallet payload with age, and downloads everything as one
                    dated zip (for example <code>slip39-wallet-backup-main-wallet-2026-07-19.zip</code>).
                </p>

                @* Airgap attestation: the tool cannot PROVE the environment is safe
                   (a web page cannot detect Tails), so a real backup requires the
                   offline probe to pass AND this deliberate confirmation. Anything
                   less is watermarked INSECURE-TEST. *@
                <label class="check">
                    <input type="checkbox" id="airgap-attest" @bind="attested" />
                    <span>
                        I confirm this machine is offline / airgapped (ideally Tails)
                        and will stay offline until this browser session ends.
                    </span>
                </label>

                @if (IsTestOnly)
                {
                    <div class="banner banner-warn">
                        ⚠ This backup will be marked <strong>INSECURE-TEST</strong>
                        (@(machineOnline != false ? "internet reachable" : "environment not attested")).
                        Usable for practice only, never for real funds.
                    </div>
                }

                @if (errorMessage is not null)
                {
                    <div class="banner banner-loud">@errorMessage</div>
                }
                @if (successMessage is not null)
                {
                    <div class="banner banner-ok">@successMessage</div>
                }

                <button class="btn btn-primary btn-block" @onclick="GenerateAsync"
                        disabled="@(isGenerating || HasBlockingCosignerDuplicate())">
                    @if (isGenerating)
                    {
                        <span class="spinner"></span>
                        <text>Generating…</text>
                    }
                    else
                    {
                        <text>Generate</text>
                    }
                </button>
            </div>
        </div>
    </div>
</div>
```

- [ ] **Step 4: Replace the transcript panel**

Replace lines 132 to 177 with:

```razor
@if (transcript is not null)
{
    <div class="panel" style="margin-top:1rem">
        <div class="panel-header">
            <span>How your payload was encrypted</span>
            <button class="btn btn-sm" @onclick="() => showTranscript = !showTranscript">
                @(showTranscript ? "Hide details" : "Show details")
            </button>
        </div>
        <div class="panel-body">
            <p class="hint">@transcript.Summary</p>
            @if (showTranscript)
            {
                @* A terminal-style log: the reader sees the command that ran and
                   what it printed back, not a claim that something happened. Each
                   kind carries a prefix as well as a colour, so the distinction
                   survives a grayscale screenshot. *@
                <div class="transcript">
                    @foreach (var line in transcript.Lines)
                    {
                        @switch (line.Kind)
                        {
                            case TranscriptLineKind.Command:
                                <div class="t-command">$ @line.Text</div>
                                break;
                            case TranscriptLineKind.Output:
                                <div class="t-output">&gt; @line.Text</div>
                                break;
                            case TranscriptLineKind.Warning:
                                <div class="t-warning">! @line.Text</div>
                                break;
                            default:
                                <div class="t-note"># @line.Text</div>
                                break;
                        }
                    }
                </div>
            }
        </div>
    </div>
}
```

The `>` and `#` prefixes are new. `Command` already had `$` and `Warning` already had
`!`, while `Output` and `Note` were distinguished by colour alone, which the global
constraint forbids.

- [ ] **Step 5: Run the suite**

Run: `dotnet test Slip39Demo.slnx -c Release`

Expected: PASS. `OwnerFormValidationTests` renders this page, so a broken Razor
expression shows up here rather than in the browser.

- [ ] **Step 6: Commit**

```bash
git add Slip39Demo.UI/Pages/Owner.razor
git commit -m "Restyle the backup page, and give transcript lines a prefix as well as a colour"
```

---

## Task 6: The recovery page

**Files:**
- Modify: `Slip39Demo.UI/Pages/Recoverer.razor` (markup only)

**Interfaces:**
- Consumes: the same vocabulary as Task 5.
- Produces: nothing.

- [ ] **Step 1: Apply the mapping**

Read the file, then apply this mapping to every element in the markup. The `@code`
block is not touched.

| Bootstrap | replacement |
| --- | --- |
| `container py-4` on the outer div | delete the div; `MainLayout` provides `.app` |
| `d-flex justify-content-between align-items-center mb-3` | `page-head` |
| `card` | `panel` |
| `card-header` | `panel-header` |
| `card-body p-3` / `card-body p-4` | `panel-body` |
| `row g-3` | `split` |
| `col-lg-6` | plain `<div>` inside `.split` |
| `alert alert-danger` | `banner banner-loud` |
| `btn btn-success w-100` | `btn btn-primary btn-block` |
| `btn btn-sm btn-outline-secondary` | `btn btn-sm` |
| `spinner-border spinner-border-sm me-2` | `spinner` |
| `small` / `text-muted` on a `<p>` | `hint` |
| `text-white` on a heading | delete; headings inherit `--ink` |
| `text-center` | delete unless the element is genuinely centred |
| `mb-0`, `mb-3`, `me-2`, `p-2` | delete; spacing comes from the components |

- [ ] **Step 2: Run the suite**

Run: `dotnet test Slip39Demo.slnx -c Release`

Expected: PASS. `RecoveryInputBindingTests` and `RecoveredPayloadViewTests` render this
page and its child.

- [ ] **Step 3: Commit**

```bash
git add Slip39Demo.UI/Pages/Recoverer.razor
git commit -m "Restyle the recovery page"
```

---

## Task 7: The shared components

**Files:**
- Modify: `Slip39Demo.UI/Shared/MnemonicInput.razor`
- Modify: `Slip39Demo.UI/Shared/CiphertextInput.razor`
- Modify: `Slip39Demo.UI/Shared/CosignerEditor.razor`
- Modify: `Slip39Demo.UI/Shared/GroupConfigEditor.razor`
- Modify: `Slip39Demo.UI/Shared/RecoveredPayloadView.razor`

**Interfaces:**
- Consumes: the vocabulary from Task 1.
- Produces: nothing.

- [ ] **Step 1: Apply the Task 6 mapping to all five files**

Plus these component-specific rules:

- `form-control form-control-sm` becomes `input`, and `font-monospace` becomes `input-mono` alongside it.
- `form-label` becomes `field-label`, and the surrounding `mb-2` / `mb-3` div becomes `field`.
- `form-text` becomes `hint`.
- `btn btn-sm btn-outline-danger` becomes `btn btn-sm btn-danger`.
- In `GroupConfigEditor`, `row g-2` with `col-2` / `col-3` / `col-4` becomes a single `<div class="cols">` with plain children. The auto-fit grid replaces the fixed column widths.
- In `RecoveredPayloadView`, `row` with `col-sm-3` / `col-sm-9` label-and-value pairs becomes a `<dl>` styled by `.cols`, and `user-select-all` becomes `mono-block`, which already sets `user-select: all`.
- In `RecoveredPayloadView`, `bg-success border-success` marks the successful verification result. Replace with `banner banner-ok`, and make sure the success text says so in words, since the global constraint forbids colour as the only encoding.
- Where a recovered mnemonic is displayed, use the `words` list rather than a paragraph:

```razor
<ol class="words">
    @foreach (var word in words)
    {
        <li>@word</li>
    }
</ol>
```

- [ ] **Step 2: Run the suite**

Run: `dotnet test Slip39Demo.slnx -c Release`

Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add Slip39Demo.UI/Shared
git commit -m "Restyle the shared input and result components"
```

---

## Task 8: Delete the vendored framework

This is last, so that every earlier task could still be checked in a browser with the
old stylesheet present if something looked wrong.

**Files:**
- Delete: `Slip39Demo.UI/wwwroot/lib/bootstrap/**`
- Modify: `Slip39Demo.Tests/Ui/StylesheetContractTests.cs` (add one test)

- [ ] **Step 1: Add the failing test**

Append to `StylesheetContractTests`:

```csharp
    // The point of the exercise. An offline tool should ship what a reviewer can
    // read before putting it on a USB stick, and 400 KB of framework that nothing
    // references is not that.
    [Fact]
    public void No_bootstrap_asset_remains_in_the_shared_ui()
    {
        var lib = Path.Combine(RepoRoot(), "Slip39Demo.UI", "wwwroot", "lib", "bootstrap");
        Assert.False(Directory.Exists(lib), $"{lib} still exists");
    }

    // Catches a Bootstrap class left behind in markup, which would silently do
    // nothing now that the framework is gone.
    [Fact]
    public void No_razor_file_uses_a_bootstrap_class()
    {
        var pattern = new Regex(
            @"\b(card|card-header|card-body|card-title|card-text|alert|alert-[a-z]+|" +
            @"btn-outline-[a-z]+|btn-secondary|btn-success|btn-info|form-control|form-label|" +
            @"form-check|form-check-input|form-check-label|form-text|row|col-[a-z0-9-]+|" +
            @"spinner-border|text-muted|text-white|text-info|text-secondary|bg-dark|bg-light|" +
            @"bg-success|border-success|user-select-all|font-monospace|shadow-sm|sticky-top)\b");

        var offenders = Directory
            .EnumerateFiles(Path.Combine(RepoRoot(), "Slip39Demo.UI"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => File.ReadLines(f)
                .Select((line, i) => (file: Path.GetFileName(f), no: i + 1, line))
                .Where(x => x.line.Contains("class=") && pattern.IsMatch(x.line))
                .Select(x => $"{x.file}:{x.no}"))
            .ToArray();

        Assert.True(offenders.Length == 0,
            $"Bootstrap classes remain: {string.Join(", ", offenders)}");
    }
```

- [ ] **Step 2: Run the test and watch it fail**

Run: `dotnet test Slip39Demo.slnx --filter FullyQualifiedName~StylesheetContractTests`

Expected: `No_bootstrap_asset_remains_in_the_shared_ui` fails. If
`No_razor_file_uses_a_bootstrap_class` also fails, its message names the file and line
of every leftover, and each one is a miss from Tasks 4 to 7. Fix them before continuing.

- [ ] **Step 3: Delete the framework**

```bash
git rm -r Slip39Demo.UI/wwwroot/lib/bootstrap
```

- [ ] **Step 4: Run the whole suite**

Run: `dotnet test Slip39Demo.slnx -c Release`

Expected: PASS.

- [ ] **Step 5: Measure what the published app lost**

```bash
dotnet publish Slip39Demo.Web -c Release -o /tmp/pub-after
du -sh /tmp/pub-after/wwwroot
```

Record the number in the commit message. Do not predict it beforehand.

- [ ] **Step 6: Check it renders with the network off**

Run `dotnet run --project Slip39Demo.Web`, load the page, then in the browser devtools
network panel reload with the "offline" throttle applied. Confirm the page still
renders and no request fails. This is the property the whole exercise is about.

- [ ] **Step 7: Commit**

```bash
git add Slip39Demo.UI Slip39Demo.Tests/Ui/StylesheetContractTests.cs
git commit -m "Delete the vendored Bootstrap bundle"
```

- [ ] **Step 8: Open the pull request**

```bash
git push -u origin feat/restyle-dice-to-seed
gh pr create --title "Restyle from dice-to-seed, and delete the vendored Bootstrap bundle" --body "..."
```

Do not merge it. Merging is the human's job.

---

## Self-review notes

Checked against the spec's styling section:

- palette, exact values: Task 1
- system fonts only, no network reference: Task 1, asserted by test
- Bootstrap deleted: Task 8, asserted by test
- `panel` / `banner` / `mono-block` / word grid idioms: Tasks 1, 5, 6, 7
- colour never the only encoding: Task 2 for the airgap banner (tested), Task 5 for the transcript prefixes, Task 7 for the verification result
- `Slip39Demo.Web` wiring untouched: no task modifies `Program.cs`
- no test asserts on a CSS class today, so nothing existing breaks: confirmed by grep before this plan was written, and Tasks 3, 5, 6, 7 each re-run the suite

The one thing this plan cannot verify by test is whether the result looks good. Tasks 4
and 8 include a browser check for that, and it needs a human.

*Collaboration by Claude*
