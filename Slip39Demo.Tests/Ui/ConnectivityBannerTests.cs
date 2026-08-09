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
    //
    // The banner-ok assertion here is load-bearing, not decoration: without it,
    // WaitForAssertion is satisfied by the very first synchronous render (the
    // neutral "Checking network status..." markup, which also lacks
    // banner-loud), before CheckAsync has resolved the probe at all. That would
    // let this test pass even if the component never reached the offline state,
    // or if a regression stranded it on the neutral branch forever. Requiring
    // banner-ok in the same assertion block forces the wait past the neutral
    // render before the negative claim about banner-loud is evaluated.
    [Fact]
    public void Offline_state_never_uses_the_loud_shape()
    {
        var cut = RenderMarkup(online: false);

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("banner-ok");
            cut.Markup.Should().NotContain("banner-loud");
        });
    }
}
