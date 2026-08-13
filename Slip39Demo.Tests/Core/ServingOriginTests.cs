using FluentAssertions;
using Slip39Demo.Core;
using Xunit;

namespace Slip39Demo.Tests.Core;

// The gate between a curious visitor on the hosted demo and a real seed typed into a web
// page. Getting it wrong in the permissive direction is the worst single mistake this UI
// can make, so the case analysis is asserted rather than trusted to a one-line expression.
//
// Ported with the logic from dice-to-seed, which had these tests first.
public class ServingOriginTests
{
    // The AppImage is the case that must not be warned at. Whichever scheme the WebView
    // uses on a given platform, nothing crossed a network: it is an in-process handler
    // reading bytes off the disk. A false negative here would put a provenance warning on
    // the offline tool and refuse every real backup it makes.
    [Theory]
    [InlineData("tauri://localhost/")]
    [InlineData("http://tauri.localhost/")]
    [InlineData("https://tauri.localhost/")]
    [InlineData("file:///tmp/.mount_x/usr/lib/index.html")]
    [InlineData("asset://localhost/")]
    public void The_appimage_shell_counts_as_local(string uri) =>
        ServingOrigin.IsLocal(uri).Should().BeTrue($"{uri} is how the AppImage's WebView loads");

    [Theory]
    [InlineData("http://localhost/")]
    [InlineData("http://LOCALHOST:5000/")]
    [InlineData("http://127.0.0.1:8080/")]
    [InlineData("http://127.0.0.2/")]
    [InlineData("http://[::1]:5001/")]
    [InlineData("http://anything.localhost/")]
    public void An_ordinary_local_server_counts_as_local(string uri) =>
        ServingOrigin.IsLocal(uri).Should().BeTrue();

    [Theory]
    [InlineData("https://petesparrowbtc.github.io/slip39-backup/")]
    [InlineData("http://192.168.1.10/")]
    [InlineData("https://example.com/")]
    public void Anything_served_over_a_network_does_not(string uri) =>
        ServingOrigin.IsLocal(uri).Should().BeFalse();

    // The reason this is a function and not a substring test. Both of these are ordinary
    // internet hosts that a "contains" or "starts with" check waves through, which is
    // exactly the silence an attacker serving a modified copy would want.
    [Theory]
    [InlineData("https://127.0.0.1.example.com/")]
    [InlineData("https://localhost.evil.com/")]
    [InlineData("https://notlocalhost/")]
    [InlineData("https://mylocalhost.net/")]
    public void A_host_that_merely_contains_a_local_name_does_not(string uri) =>
        ServingOrigin.IsLocal(uri).Should().BeFalse(
            $"{uri} is an ordinary internet host that a substring test would pass");

    // A safety gate fails towards the warning. An unparseable base URI is a situation
    // nobody designed, and the wrong response to it is a green tick.
    [Theory]
    [InlineData("")]
    [InlineData("not a uri")]
    [InlineData("/slip39-backup/")]
    public void An_unparseable_origin_is_treated_as_remote(string uri) =>
        ServingOrigin.IsLocal(uri).Should().BeFalse("an unknown origin must not read as safe");

    // Describe puts the scheme in deliberately: without it a reader cannot tell an
    // ordinary local server from the AppImage's in-process handler.
    [Theory]
    [InlineData("https://petesparrowbtc.github.io/slip39-backup/", "https://petesparrowbtc.github.io")]
    [InlineData("http://localhost:5000/", "http://localhost:5000")]
    [InlineData("http://127.0.0.1/", "http://127.0.0.1")]
    public void Describe_names_the_scheme_host_and_any_port(string uri, string expected) =>
        ServingOrigin.Describe(uri).Should().Be(expected);
}
