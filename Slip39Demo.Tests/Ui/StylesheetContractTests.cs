using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Slip39Demo.Tests.Ui;

// The stylesheet is the whole design system now, so a few things about it are
// worth asserting rather than assuming. This is the same instinct as the
// conformance vectors elsewhere in this suite: a claim nobody checks is a claim
// that drifts.
public class StylesheetContractTests
{
    // Walks up from the test binary to the repository root, so the test does not
    // care whether it runs from bin/Debug, bin/Release, or a CI working directory.
    // Internal (not private) so IconTests can reach it without duplicating the
    // walk-up logic.
    internal static string RepoRootPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Slip39Demo.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    static string AppCss() =>
        File.ReadAllText(Path.Combine(RepoRootPath(), "Slip39Demo.UI", "wwwroot", "css", "app.css"));

    // The exact class vocabulary task-1-brief.md promises to every later task
    // ("Produces", Interfaces section). A name listed there that app.css does not
    // define is dead on arrival for whichever task consumes it: this is exactly how
    // btn-ghost got into the first draft of the contract and out of review, because
    // nothing checked the list against the stylesheet.
    static readonly string[] ContractClasses =
    [
        "app", "panel", "panel-header", "panel-body", "banner", "banner-ok", "banner-warn",
        "banner-loud", "field", "field-label", "hint", "hint-loud", "input", "input-mono",
        "btn", "btn-primary", "btn-danger", "btn-sm", "btn-block", "split", "split-sticky",
        "cols", "row-between", "mono-block", "words", "transcript", "t-command", "t-output",
        "t-warning", "t-note", "check", "spinner", "section-label", "subtitle", "page-head",
        "icon", "with-icon", "choice-pair",
    ];

    // Matches the class as a whole selector token, not a substring: `\.panel\b`
    // would also satisfy on `.panel-header` because `-` is a non-word character, so
    // the lookahead excludes any [\w-] continuation instead of relying on \b.
    [Fact]
    public void Every_contract_class_is_defined_in_the_stylesheet()
    {
        var css = AppCss();
        var missing = ContractClasses
            .Where(name => !Regex.IsMatch(css, $@"\.{Regex.Escape(name)}(?![\w-])"))
            .ToArray();

        missing.Should().BeEmpty(
            $"app.css must define every class the plan's contract promises; missing: {string.Join(", ", missing)}");
    }

    // A regression guard for something a test suite cannot otherwise see. App.razor
    // focuses the h1 after every navigation, so without a rule suppressing the ring
    // there is a box around the heading on every page load. The Task 1 rewrite of this
    // stylesheet dropped the rule that origin/main had, and only a screenshot caught it.
    //
    // The :not(:focus-visible) part is asserted deliberately: a bare
    // "h1:focus { outline: none }" would pass a looser test while removing the focus
    // indicator from keyboard users as well.
    [Fact]
    public void Programmatic_h1_focus_does_not_draw_a_ring_but_keyboard_focus_still_does()
    {
        Assert.Matches(
            new Regex(@"h1:focus:not\(:focus-visible\)\s*\{[^}]*outline:\s*none"),
            AppCss());
    }

    // The case the rule above deliberately leaves alone: a hard page load or a real
    // keyboard tab still lands a focus-visible ring on the h1, so it is styled to look
    // like part of the design (the same accent colour used everywhere else) rather than
    // suppressed. Asserted on var(--accent) specifically, not just "outline: something",
    // so a later edit that reaches for an arbitrary colour instead of the palette variable
    // fails the build the way the missing suppression rule once slipped past review.
    [Fact]
    public void Keyboard_visible_h1_focus_is_styled_with_the_accent_colour()
    {
        Assert.Matches(
            new Regex(@"h1:focus-visible\s*\{[^}]*outline:[^;]*var\(--accent\)"),
            AppCss());
    }

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
    public void Palette_defines_the_agreed_value(string name, string value)
    {
        var pattern = new Regex($@"{Regex.Escape(name)}:\s*{Regex.Escape(value)}\s*;");
        pattern.IsMatch(AppCss()).Should().BeTrue($"app.css should define {name}: {value};");
    }

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

        offenders.Should().BeEmpty("app.css must not reach the network");
    }

    // Both host pages must stop linking the vendored framework. Checked here
    // rather than left to review, because a stray link would silently restore
    // 400 KB and the old cascade.
    [Theory]
    [InlineData("Slip39Demo.Web")]
    [InlineData("Slip39Demo.Desktop")]
    public void Host_page_does_not_link_bootstrap(string project)
    {
        var html = File.ReadAllText(Path.Combine(RepoRootPath(), project, "wwwroot", "index.html"));
        html.Should().NotContainEquivalentOf("bootstrap");
    }

    // The point of the exercise. An offline tool should ship what a reviewer can
    // read before putting it on a USB stick, and 400 KB of framework that nothing
    // references is not that.
    [Fact]
    public void No_bootstrap_asset_remains_in_the_shared_ui()
    {
        var lib = Path.Combine(RepoRootPath(), "Slip39Demo.UI", "wwwroot", "lib", "bootstrap");
        Assert.False(Directory.Exists(lib), $"{lib} still exists");
    }

    // Catches a Bootstrap class left behind in markup, which would silently do
    // nothing now that the framework is gone.
    //
    // The boundaries are (?<![\w-]) and (?![\w-]), not \b, and that is load-bearing.
    // \b treats a hyphen as a boundary, so \brow\b matches the "row" inside the
    // legitimate new class "row-between" and fails the build on correct code. The
    // same trap cost a round in Task 1 with .panel and .panel-header.
    [Fact]
    public void No_razor_file_uses_a_bootstrap_class()
    {
        var pattern = new Regex(
            @"(?<![\w-])(card|card-header|card-body|card-title|card-text|alert|alert-[a-z]+|" +
            @"btn-outline-[a-z]+|btn-secondary|btn-success|btn-info|form-control|form-label|" +
            @"form-check|form-check-input|form-check-label|form-text|row|col-[a-z0-9-]+|" +
            @"spinner-border|text-muted|text-white|text-info|text-secondary|bg-dark|bg-light|" +
            @"bg-success|border-success|user-select-all|font-monospace|shadow-sm|sticky-top)(?![\w-])");

        var offenders = Directory
            .EnumerateFiles(Path.Combine(RepoRootPath(), "Slip39Demo.UI"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => File.ReadLines(f)
                .Select((line, i) => (file: Path.GetFileName(f), no: i + 1, line))
                .Where(x => x.line.Contains("class=") && pattern.IsMatch(x.line))
                .Select(x => $"{x.file}:{x.no}"))
            .ToArray();

        Assert.True(offenders.Length == 0,
            $"Bootstrap classes remain: {string.Join(", ", offenders)}");
    }
}
