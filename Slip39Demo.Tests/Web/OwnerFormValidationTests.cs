using System.Text.RegularExpressions;
using Bunit;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Slip39Demo.UI.Pages;
using Slip39Demo.UI.Services;
using Xunit;

namespace Slip39Demo.Tests.Web;

// bUnit component tests for the Owner page form. These exercise the failure
// path (no seed -> visible error, no download) and the happy path (valid seed
// -> exactly one download call with the expected filename/mime). The
// IFileDownloader and IIndependentVerifier are replaced with fakes so we never
// touch JS interop in tests; the real verifier runs third-party JS libs in the
// browser and is exercised by the Playwright end-to-end pass instead.
public class OwnerFormValidationTests : TestContext
{
    public OwnerFormValidationTests()
    {
        // Defaults: verification passes, machine is OFFLINE. Individual tests
        // override to exercise the refusal / watermark paths.
        Services.AddSingleton<IIndependentVerifier>(new FakeVerifier(Result.Success()));
        Services.AddSingleton<IConnectivityProbe>(new FakeProbe(online: false));
    }

    // Scripted connectivity probe for the airgap gate.
    sealed class FakeProbe(bool online) : IConnectivityProbe
    {
        public Task<bool> IsOnlineAsync() => Task.FromResult(online);
    }

    // Waits for the banner's first probe to land, then (optionally) ticks the
    // airgap attestation — the preconditions for a clean, unwatermarked backup.
    static void AttestOffline(IRenderedComponent<Owner> cut, bool attest = true)
    {
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("No internet reachable"),
            timeout: TimeSpan.FromSeconds(10));
        if (attest)
            cut.Find("#airgap-attest").Change(true);
    }

    // Fake IFileDownloader that captures download attempts instead of
    // performing browser interop. Test-only class -> mutable list is fine.
    sealed class NoopDownloader : IFileDownloader
    {
        public List<(string Filename, byte[] Bytes, string Mime)> Calls { get; } = new();

        public ValueTask DownloadAsync(string filename, byte[] bytes, string mimeType)
        {
            Calls.Add((filename, bytes, mimeType));
            return ValueTask.CompletedTask;
        }
    }

    // Scripted verifier: returns the configured result and records what it saw.
    sealed class FakeVerifier(Result result) : IIndependentVerifier
    {
        public List<(int SubsetCount, int PayloadLength)> Calls { get; } = new();

        public Task<Result> VerifyAsync(
            IReadOnlyList<IReadOnlyList<string>> subsets, byte[] payloadAge, string expectedPayloadText)
        {
            Calls.Add((subsets.Count, payloadAge.Length));
            return Task.FromResult(result);
        }
    }

    [Fact]
    public void Generate_WhenIndependentVerificationFails_RefusesDownload()
    {
        // The whole point of the gate: a backup that third-party implementations
        // cannot round-trip must never reach the user. Visible REFUSED error, zero
        // download calls.
        var downloader = new NoopDownloader();
        Services.AddSingleton<IFileDownloader>(downloader);
        Services.AddSingleton<IIndependentVerifier>(
            new FakeVerifier(Result.Failure("subset 1 recovered a DIFFERENT key")));

        var cut = RenderComponent<Owner>();
        cut.FindAll("input.font-monospace").First()
            .Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".alert-danger").Should().Contain(el => el.TextContent.Contains("REFUSED"));
        }, timeout: TimeSpan.FromSeconds(10));

        downloader.Calls.Should().BeEmpty();
    }

    [Fact]
    public void Generate_PassesCoveringSubsetsToVerifier()
    {
        // Default 3-of-5 config → ceil(5/3) = 2 covering subsets; verifier must see
        // them plus a non-empty payload.
        var downloader = new NoopDownloader();
        var verifier = new FakeVerifier(Result.Success());
        Services.AddSingleton<IFileDownloader>(downloader);
        Services.AddSingleton<IIndependentVerifier>(verifier);

        var cut = RenderComponent<Owner>();
        cut.FindAll("input.font-monospace").First()
            .Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            verifier.Calls.Should().ContainSingle();
            verifier.Calls[0].SubsetCount.Should().Be(2);
            verifier.Calls[0].PayloadLength.Should().BeGreaterThan(0);
            cut.Markup.Should().Contain("independently verified");
        }, timeout: TimeSpan.FromSeconds(10));

        downloader.Calls.Should().ContainSingle();
    }

    [Fact]
    public void TopLevelSeed_ShowsCosignerFingerprint_ForCanonicalSeed()
    {
        // Exercises the real Owner -> CosignerEditor wiring. The cosigner leaves its
        // own seed blank, so its auto-computed fingerprint must come from the shared
        // top-level seed. Regression guard: TopLevelSeed was once passed as a literal
        // string attribute (missing @), so the child fingerprinted the text
        // "model.TopLevelSeedWords" instead of the seed. The canonical all-abandon/about
        // seed with no passphrase has master fingerprint 73c5da0a.
        var downloader = new NoopDownloader();
        Services.AddSingleton<IFileDownloader>(downloader);

        var cut = RenderComponent<Owner>();

        // First monospace input is the top-level seed (cosigner seed/derivation inputs
        // come later in the DOM).
        cut.FindAll("input.font-monospace").First()
            .Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("input[readonly]").Should()
                .Contain(el => el.GetAttribute("value") == "73c5da0a");
        }, timeout: TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void DuplicateCosigners_SameKey_BlockGeneration()
    {
        // Two cosigners both falling back to the top-level seed, with the default
        // (empty) passphrase and same derivation path, are the identical key — a
        // degenerate multisig. Generation must be blocked: a visible error and no
        // download.
        var downloader = new NoopDownloader();
        Services.AddSingleton<IFileDownloader>(downloader);

        var cut = RenderComponent<Owner>();
        cut.FindAll("input.font-monospace").First()
            .Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");

        // Add a second cosigner (identical to the first: no own seed, no passphrase, same path).
        cut.FindAll("button").First(b => b.TextContent.Contains("Add cosigner")).Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".alert-danger").Should().Contain(el => el.TextContent.Contains("same key"));
            // The Generate button is disabled while a duplicate exists.
            cut.Find("button.btn-primary").HasAttribute("disabled").Should().BeTrue();
        }, timeout: TimeSpan.FromSeconds(10));

        downloader.Calls.Should().BeEmpty();
    }

    [Fact]
    public void Cosigners_DifferentPassphrases_NotBlocked_DistinctFingerprints()
    {
        // Two cosigners on the same top-level seed but DIFFERENT passphrases are
        // distinct keys — not blocked. Also proves each cosigner's passphrase binds
        // independently: the two auto-computed fingerprints must differ.
        var downloader = new NoopDownloader();
        Services.AddSingleton<IFileDownloader>(downloader);

        var cut = RenderComponent<Owner>();
        cut.FindAll("input.font-monospace").First()
            .Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
        cut.FindAll("button").First(b => b.TextContent.Contains("Add cosigner")).Click();

        // Re-query before each change: every .Change re-renders the live form, so a
        // cached node list would go stale.
        cut.FindAll("input[placeholder='Leave empty if no passphrase']")[0].Change("t1");
        cut.FindAll("input[placeholder='Leave empty if no passphrase']")[1].Change("t2");

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".alert-danger").Should().BeEmpty();
            cut.Find("button.btn-primary").HasAttribute("disabled").Should().BeFalse();
            var fps = cut.FindAll("input[readonly]").Select(el => el.GetAttribute("value")).ToList();
            fps.Should().HaveCount(2);
            fps[0].Should().NotBe(fps[1]); // distinct passphrases -> distinct keys
        }, timeout: TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Generate_WithNoSeed_DisplaysError()
    {
        var downloader = new NoopDownloader();
        Services.AddSingleton<IFileDownloader>(downloader);

        var cut = RenderComponent<Owner>();

        // Default form: 1 cosigner with no seed words, no top-level seed.
        var generateButton = cut.Find("button.btn-primary");
        generateButton.Click();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("At least one seed must be provided");
        });
        downloader.Calls.Should().BeEmpty();
    }

    [Fact]
    public void Generate_OfflineAndAttested_TriggersCleanDownload()
    {
        var downloader = new NoopDownloader();
        Services.AddSingleton<IFileDownloader>(downloader);

        var cut = RenderComponent<Owner>();

        // The top-level seed words input is the first input with the
        // font-monospace class on the page (the other monospace inputs
        // are derivation_path/seed_words inside cosigner editors, which
        // come later in the DOM).
        var seedInput = cut.FindAll("input.font-monospace").First();
        seedInput.Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");

        AttestOffline(cut); // offline probe landed + attestation ticked → clean backup

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            // Filename is date-stamped and label-slugged, and NOT watermarked. The
            // default form label is "Main wallet", so it becomes
            // "slip39-wallet-backup-main-wallet-<yyyy-MM-dd>.zip".
            downloader.Calls.Should().ContainSingle(c =>
                Regex.IsMatch(c.Filename, @"^slip39-wallet-backup-main-wallet-\d{4}-\d{2}-\d{2}\.zip$")
                && c.Mime == "application/zip");
        }, timeout: TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Generate_WithoutAttestation_IsWatermarkedInsecureTest()
    {
        // Offline probe alone is NOT enough — the user must also attest the
        // airgap. Unattested generation must carry the INSECURE-TEST watermark.
        var downloader = new NoopDownloader();
        Services.AddSingleton<IFileDownloader>(downloader);

        var cut = RenderComponent<Owner>();
        cut.FindAll("input.font-monospace").First()
            .Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
        AttestOffline(cut, attest: false);

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            downloader.Calls.Should().ContainSingle(c => c.Filename.StartsWith("INSECURE-TEST-"));
            cut.Markup.Should().Contain("INSECURE-TEST backup (practice only)");
        }, timeout: TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Generate_WhenOnline_IsWatermarkedInsecureTest_EvenIfAttested()
    {
        // The probe outranks the checkbox: internet reachable → watermark,
        // regardless of what the user attests.
        var downloader = new NoopDownloader();
        Services.AddSingleton<IFileDownloader>(downloader);
        Services.AddSingleton<IConnectivityProbe>(new FakeProbe(online: true));

        var cut = RenderComponent<Owner>();
        cut.FindAll("input.font-monospace").First()
            .Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("ONLINE"),
            timeout: TimeSpan.FromSeconds(10));
        cut.Find("#airgap-attest").Change(true);

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            downloader.Calls.Should().ContainSingle(c => c.Filename.StartsWith("INSECURE-TEST-"));
        }, timeout: TimeSpan.FromSeconds(10));
    }
}
