using Bunit;
using FluentAssertions;
using Slip39Demo.UI.Shared;
using Xunit;

namespace Slip39Demo.Tests.Ui;

public class IconTests : TestContext
{
    [Theory]
    [InlineData("backup")]
    [InlineData("recover")]
    [InlineData("lock")]
    public void Known_icon_renders_drawable_geometry(string name)
    {
        var cut = RenderComponent<Icon>(p => p.Add(c => c.Name, name));

        cut.Markup.Should().Contain("<svg");
        // currentColor is the whole point: the icon follows the colour of the text it
        // sits in, including the accent, with no per-icon colour to keep in sync.
        cut.Markup.Should().Contain("currentColor");
        cut.FindAll("path, rect").Should().NotBeEmpty($"icon '{name}' must draw something");
    }

    // An empty <svg> is invisible, so a typo in a name would silently remove an icon
    // and nothing would ever report it. Fail loudly instead.
    [Fact]
    public void Unknown_icon_name_throws_rather_than_rendering_nothing()
    {
        var render = () => RenderComponent<Icon>(p => p.Add(c => c.Name, "no-such-icon"));

        render.Should().Throw<ArgumentException>().WithMessage("*no-such-icon*");
    }

    // The guard. Astral-plane characters (U+10000 and above) are exactly the
    // pictographic emoji, and in UTF-16 they always appear as a surrogate pair. The
    // glyphs deliberately kept in this UI (⚠ ✓ → ←) are all in the basic plane, so
    // this catches a returning emoji without touching them.
    [Fact]
    public void No_razor_file_contains_an_astral_plane_character()
    {
        var offenders = Directory
            .EnumerateFiles(
                Path.Combine(StylesheetContractTests.RepoRootPath(), "Slip39Demo.UI"),
                "*.razor",
                SearchOption.AllDirectories)
            .SelectMany(f => File.ReadLines(f)
                .Select((line, i) => (file: Path.GetFileName(f), no: i + 1, line))
                .Where(x => x.line.Any(char.IsSurrogate))
                .Select(x => $"{x.file}:{x.no}"))
            .ToArray();

        offenders.Should().BeEmpty(
            "emoji are a font dependency and must not return: " + string.Join(", ", offenders));
    }
}
