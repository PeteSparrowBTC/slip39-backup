using System.Text.RegularExpressions;
using FluentAssertions;
using Slip39Demo.Core;
using Xunit;

namespace Slip39Demo.Tests.Ui;

// The version is stamped into every share README and into the verification record inside
// every backup, and it is now shown in the UI. Three consumers of one value is exactly the
// shape that used to drift, so the single source is asserted rather than assumed.
public class AppVersionTests
{
    [Fact]
    public void Current_is_populated()
    {
        AppVersion.Current.Should().NotBeNullOrWhiteSpace();
        AppVersion.Current.Should().NotBe("unknown",
            "the fallback means MSBuild wrote no version attribute at all");
    }

    // The SDK appends "+<commit sha>" to the informational version. It belongs in a build
    // log, not in a footer or a share README, so AppVersion trims it.
    [Fact]
    public void Current_carries_no_source_control_metadata() =>
        AppVersion.Current.Should().NotContain("+");

    // Ties the value the code reports to the value a human edits. Without this, bumping
    // Directory.Build.props and forgetting to rebuild, or vice versa, is invisible.
    //
    // A tagged CI build overrides Version from the tag, so the two are allowed to differ
    // there: the check is skipped when an override is in effect rather than made to fail on
    // a legitimate release build.
    [Fact]
    public void Current_matches_the_version_in_Directory_Build_props()
    {
        var propsPath = Path.Combine(StylesheetContractTests.RepoRootPath(), "Directory.Build.props");
        File.Exists(propsPath).Should().BeTrue($"expected the single source of truth at {propsPath}");

        var match = Regex.Match(File.ReadAllText(propsPath), @"<Version>([^<]+)</Version>");
        match.Success.Should().BeTrue("Directory.Build.props must declare a <Version>");

        var declared = match.Groups[1].Value.Trim();

        if (Environment.GetEnvironmentVariable("GITHUB_REF_NAME") is { Length: > 0 } tagName
            && tagName.StartsWith('v'))
        {
            AppVersion.Current.Should().Be(tagName[1..],
                "a tagged build stamps the version from the tag");
            return;
        }

        AppVersion.Current.Should().Be(declared);
    }
}
