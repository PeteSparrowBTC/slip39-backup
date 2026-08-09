using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Slip39Demo.UI.Services;
using Slip39Demo.UI.Shared;
using Xunit;

namespace Slip39Demo.Tests.Ui;

// The airgap banner is the one component where styling carries a security
// meaning, so it gets a real test rather than a look. ConnectivityBanner
// probes on OnAfterRenderAsync and then loops on a PeriodicTimer, so the
// first state resolves asynchronously; WaitForAssertion accounts for that.
public class ConnectivityBannerTests : TestContext
{
    sealed class StubProbe(bool online) : IConnectivityProbe
    {
        public Task<bool> IsOnlineAsync() => Task.FromResult(online);
    }

    IRenderedComponent<ConnectivityBanner> RenderMarkup(bool online)
    {
        Services.AddScoped<IConnectivityProbe>(_ => new StubProbe(online));
        return RenderComponent<ConnectivityBanner>();
    }

    // The online state is the one a reader must never skim past, so it must be
    // distinguishable without colour. Two independent encodings are asserted:
    // the word ONLINE in the text, and the banner-loud class, which is the
    // only banner style with a double border.
    [Fact]
    public void Online_state_says_so_in_words_and_in_shape()
    {
        var cut = RenderMarkup(online: true);

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("ONLINE");
            cut.Markup.Should().Contain("banner-loud");
        });
    }

    [Fact]
    public void Offline_state_says_so_in_words()
    {
        var cut = RenderMarkup(online: false);

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().ContainEquivalentOf("offline");
            cut.Markup.Should().Contain("banner-ok");
        });
    }

    // banner-loud must never be reachable in the safe state, because a reader
    // who learns that the loud shape means danger has to be able to rely on it.
    [Fact]
    public void Offline_state_never_uses_the_loud_shape()
    {
        var cut = RenderMarkup(online: false);

        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("banner-loud"));
    }
}
