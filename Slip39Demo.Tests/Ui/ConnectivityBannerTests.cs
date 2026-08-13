using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
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

    // bUnit's own NavigationManager reports http://localhost/, a local origin, so every
    // test that is not about provenance keeps exercising the carrier banners unchanged.
    // This stands in for the hosted demo.
    sealed class RemoteNavigation : NavigationManager
    {
        public RemoteNavigation() =>
            Initialize("https://petesparrowbtc.github.io/slip39-backup/",
                       "https://petesparrowbtc.github.io/slip39-backup/owner");
    }

    IRenderedComponent<ConnectivityBanner> RenderMarkup(bool online, bool servedRemotely = false)
    {
        Services.AddScoped<IConnectivityProbe>(_ => new StubProbe(online));
        if (servedRemotely)
            Services.AddSingleton<NavigationManager>(new RemoteNavigation());
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

    // The defect this whole mechanism exists for. On the hosted demo a visitor could pull
    // the network cable, watch this component turn green and say "This machine is offline",
    // and reasonably conclude it was now safe to type a real seed, while running
    // WebAssembly a server sent them on their everyday computer. The reassuring state must
    // not be reachable there at all: offline is necessary and not sufficient.
    [Fact]
    public void A_page_served_over_the_network_never_shows_the_reassuring_state()
    {
        var cut = RenderMarkup(online: false, servedRemotely: true);

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("banner-loud");
            cut.Markup.Should().NotContain("banner-ok");
            cut.Markup.Should().NotContain("This machine is offline");
        });
    }

    // Naming the origin rather than saying "somewhere": a reader can only judge the claim
    // if they can see what served them, and it distinguishes the hosted demo from a local
    // server they started themselves.
    [Fact]
    public void A_page_served_over_the_network_names_who_served_it()
    {
        var cut = RenderMarkup(online: false, servedRemotely: true);

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("https://petesparrowbtc.github.io");
            cut.Markup.Should().Contain("INSECURE-TEST");
        });
    }

    // Both facts are true at once and both belong on screen, but provenance leads: no
    // amount of disconnecting fixes code you cannot check, while the reverse is not true.
    [Fact]
    public void A_remote_page_that_is_also_online_says_both()
    {
        var cut = RenderMarkup(online: true, servedRemotely: true);

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("served to you by");
            cut.Markup.Should().Contain("ONLINE");
        });
    }
}
