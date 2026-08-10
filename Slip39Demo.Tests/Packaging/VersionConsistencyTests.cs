using System.Text.RegularExpressions;
using Slip39Demo.Tests.Ui;
using Xunit;

namespace Slip39Demo.Tests.Packaging;

// Directory.Build.props is the single source of the version the UI shows and the
// AppImage filename carries. src-tauri/Cargo.toml has to declare its own, so this pins
// the two together rather than trusting whoever bumps one to remember the other.
public class VersionConsistencyTests
{
    static string Read(string relative) =>
        File.ReadAllText(Path.Combine(StylesheetContractTests.RepoRootPath(), relative));

    static string Group(string text, string pattern) =>
        Regex.Match(text, pattern) is { Success: true } m
            ? m.Groups[1].Value
            : throw new InvalidOperationException($"no match for {pattern}");

    [Fact]
    public void The_rust_shell_declares_the_same_version_as_the_dotnet_build()
    {
        var dotnet = Group(Read("Directory.Build.props"), @"<Version>([^<]+)</Version>");
        var rust = Group(Read("src-tauri/Cargo.toml"), @"(?m)^version\s*=\s*""([^""]+)""");

        Assert.Equal(dotnet, rust);
    }

    // Tauri reads the version from Cargo.toml when the config omits it. A value here
    // would be a third declaration, and the one nothing else checks.
    [Fact]
    public void The_tauri_config_declares_no_version_of_its_own() =>
        Assert.DoesNotContain("\"version\"", Read("src-tauri/tauri.conf.json"));
}
