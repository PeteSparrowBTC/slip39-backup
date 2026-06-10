using Bunit;
using FluentAssertions;
using Slip39Demo.Core.Payload;
using Slip39Demo.Web.Shared;
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

        cut.Markup.Should().NotContain("abandon abandon abandon abandon about");
        cut.Markup.Should().Contain("Reveal seed words");

        cut.Find("button.btn-outline-warning").Click();

        cut.Markup.Should().Contain("abandon abandon abandon abandon about");
    }

    [Fact]
    public void Render_NullPayload_ShowsPlaceholder()
    {
        var cut = RenderComponent<RecoveredPayloadView>(p => p.Add(x => x.Payload, (PayloadV1_1?)null));
        cut.Markup.Should().Contain("no payload");
    }
}
