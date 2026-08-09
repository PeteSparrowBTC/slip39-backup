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
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Slip39Demo.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    static string AppCss() =>
        File.ReadAllText(Path.Combine(RepoRoot(), "Slip39Demo.UI", "wwwroot", "css", "app.css"));

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
        var html = File.ReadAllText(Path.Combine(RepoRoot(), project, "wwwroot", "index.html"));
        html.Should().NotContainEquivalentOf("bootstrap");
    }
}
