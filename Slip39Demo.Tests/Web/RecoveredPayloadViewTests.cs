using Bunit;
using FluentAssertions;
using Slip39Demo.Core.Payload;
using Slip39Demo.UI.Shared;
using Xunit;

namespace Slip39Demo.Tests.Web;

// bUnit component tests for the RecoveredPayloadView. Verifies two
// independent rendering contracts: (1) a null payload shows the placeholder
// text, and (2) a valid payload hides seed words behind a reveal button so
// that recovered secrets never appear in markup until the user explicitly
// asks to see them.
public class RecoveredPayloadViewTests : TestContext
{
    [Fact]
    public void Render_HidesSeedWordsByDefault_RevealsOnClick()
    {
        var payload = new PayloadV1_1(
            "1.1", "2026-05-21T00:00:00Z", "Main wallet",
            "abandon abandon abandon abandon about",
            [new Cosigner("main", "bip39", null, "m/84'/0'/0'", null, null)],
            null, "3-of-5", true, null);

        var cut = RenderComponent<RecoveredPayloadView>(p => p.Add(x => x.Payload, payload));

        cut.Markup.Should().NotContain("abandon");
        cut.Markup.Should().Contain("Reveal seed words");

        // Selects the top-level reveal button by id rather than its CSS class:
        // RecoveredPayloadView has a second, identically-labelled "Reveal seed
        // words" button per cosigner, so a class or text selector is ambiguous
        // and a class rename (as happened when this component was restyled)
        // would silently break a class-based selector anyway.
        cut.Find("#reveal-top-seed").Click();

        // The revealed seed renders as a numbered .words list (one <li> per
        // word), not a single text blob, so the contract is checked word by
        // word rather than as one contiguous substring.
        cut.FindAll("ol.words li").Select(li => li.TextContent).Should()
            .Equal("abandon", "abandon", "abandon", "abandon", "about");
    }

    [Fact]
    public void Render_NullPayload_ShowsPlaceholder()
    {
        var cut = RenderComponent<RecoveredPayloadView>(p => p.Add(x => x.Payload, (PayloadV1_1?)null));
        cut.Markup.Should().Contain("no payload");
    }
}
