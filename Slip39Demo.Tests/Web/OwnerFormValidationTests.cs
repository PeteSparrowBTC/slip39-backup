using System.Text.RegularExpressions;
using Bunit;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Slip39Demo.UI.Pages;
using Slip39Demo.UI.Services;
using Slip39Demo.Web.Services;
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
    // Selects Owner's own top-level seed field by its placeholder text rather than
    // a CSS class. CosignerEditor's own seed input uses a different (truncated)
    // placeholder, so this stays unambiguous across a restyle: a class-based
    // selector (input.font-monospace, later input.input-mono) breaks the moment
    // both inputs share the same class, which is exactly what happened here and
    // what Task 7 (CosignerEditor's own migration) will do again.
    const string TopLevelSeedSelector =
        "input[placeholder='abandon ability able about above absent absorb abstract absurd abuse access accident']";

    public OwnerFormValidationTests()
    {
        // Defaults: verification passes, machine is OFFLINE. Individual tests
        // override to exercise the refusal / watermark paths.
        Services.AddSingleton<IIndependentVerifier>(new FakeVerifier(Result.Success()));
        Services.AddSingleton<IConnectivityProbe>(new FakeProbe(online: false));
        // The in-process encryptor, not the native one: these tests must not
        // depend on an age binary being present. The subprocess path has its own
        // tests against a real downloaded release.
        Services.AddSingleton<IPayloadEncryptor>(new AgeSharpPayloadEncryptor());
        // The GnuPG outer-lock check, stubbed to pass. The tests below that are about that
        // gate register their own outcome, which wins: the last registration resolves.
        Services.AddSingleton<IOuterLockVerifier>(new FakeOuterLockVerifier());
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

    // Fake IFileDownloader that captures download attempts instead of performing
    // browser/native interop. Test-only class -> mutable list is fine.
    //
    // Takes a script of results, one per call, so a cancelled-then-retried save
    // can be simulated: new NoopDownloader(false, true) fails the first attempt
    // and succeeds the second. With no script (the common case), every call
    // succeeds. Once the script runs out, the last entry keeps repeating, so a
    // third or later click behaves like the second rather than throwing.
    sealed class NoopDownloader : IFileDownloader
    {
        public List<(string Filename, byte[] Bytes, string Mime)> Calls { get; } = new();

        readonly Queue<bool> results;

        public NoopDownloader(params bool[] scriptedResults) =>
            results = new Queue<bool>(scriptedResults.Length > 0 ? scriptedResults : new[] { true });

        public ValueTask<bool> DownloadAsync(string filename, byte[] bytes, string mimeType)
        {
            Calls.Add((filename, bytes, mimeType));
            var result = results.Count > 1 ? results.Dequeue() : results.Peek();
            return ValueTask.FromResult(result);
        }
    }

    // Scripted verifier: returns the configured result and records what it saw.
    // foreignResult defaults to success so existing tests exercise the forward
    // gate in isolation; the reverse gate gets its own test below.
    sealed class FakeVerifier(Result result, Result? foreignResult = null) : IIndependentVerifier
    {
        public List<(int SubsetCount, int PayloadLength)> Calls { get; } = new();
        public int ForeignCalls { get; private set; }

        public Task<Result> VerifyAsync(
            IReadOnlyList<IReadOnlyList<string>> subsets, byte[] payloadAge, string expectedPayloadText)
        {
            Calls.Add((subsets.Count, payloadAge.Length));
            return Task.FromResult(result);
        }

        public Task<Result> VerifyForeignReadableAsync()
        {
            ForeignCalls++;
            return Task.FromResult(foreignResult ?? Result.Success());
        }
    }

    [Fact]
    public void Generate_WhenThisBuildCannotReadAForeignBackup_RefusesDownload()
    {
        // The reverse gate. The forward check passes here (this build writes
        // something the JS stack can read), but the build cannot read what the JS
        // stack writes, which means Recoverer mode would fail on a payload.age or
        // share set that came through any other tool. Refuse rather than hand out
        // a backup only this binary can open.
        var downloader = new NoopDownloader();
        var verifier = new FakeVerifier(
            Result.Success(),
            Result.Failure("this build cannot combine SLIP-39 shares written by another implementation"));
        Services.AddSingleton<IFileDownloader>(downloader);
        Services.AddSingleton<IIndependentVerifier>(verifier);

        var cut = RenderComponent<Owner>();
        cut.FindAll(TopLevelSeedSelector).First()
            .Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".banner-loud").Should()
                .Contain(el => el.TextContent.Contains("REFUSED") && el.TextContent.Contains("cross-implementation"));
        }, timeout: TimeSpan.FromSeconds(10));

        downloader.Calls.Should().BeEmpty();
        verifier.ForeignCalls.Should().Be(1);
    }

    // The outer-lock gate, on a REAL backup. GnuPG is absent (or unreachable), so nothing
    // independent of BouncyCastle has opened the layer BouncyCastle wrote. "Could not
    // check" and "checked and wrong" are the same fact about the backup, so a real backup
    // refuses on both.
    [Fact]
    public void Generate_WhenTheOuterLockCannotBeChecked_RefusesARealBackup()
    {
        var downloader = new NoopDownloader();
        Services.AddSingleton<IFileDownloader>(downloader);
        Services.AddSingleton<IOuterLockVerifier>(new FakeOuterLockVerifier(
            OuterLockOutcome.Unavailable, "A browser cannot run GnuPG"));

        var cut = RenderComponent<Owner>();
        cut.FindAll(TopLevelSeedSelector).First()
            .Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
        AttestOffline(cut); // offline + attested → a real, unwatermarked backup

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".banner-loud").Should().Contain(el =>
                el.TextContent.Contains("REFUSED") && el.TextContent.Contains("outer OpenPGP lock"));
        }, timeout: TimeSpan.FromSeconds(10));

        downloader.Calls.Should().BeEmpty();
    }

    // The same outcome on a watermarked practice backup goes through. It has to: the
    // hosted demo runs in a browser that cannot reach gpg at all, and refusing there would
    // leave nothing to demonstrate. What must NOT happen is that it goes through quietly,
    // so the transcript says the check did not run.
    [Fact]
    public void Generate_WhenTheOuterLockCannotBeChecked_StillProducesAWatermarkedTestBackup()
    {
        var downloader = new NoopDownloader();
        Services.AddSingleton<IFileDownloader>(downloader);
        Services.AddSingleton<IOuterLockVerifier>(new FakeOuterLockVerifier(
            OuterLockOutcome.Unavailable, "A browser cannot run GnuPG"));

        var cut = RenderComponent<Owner>();
        cut.FindAll(TopLevelSeedSelector).First()
            .Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
        AttestOffline(cut, attest: false); // no attestation → INSECURE-TEST

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            downloader.Calls.Should().ContainSingle(c => c.Filename.StartsWith("INSECURE-TEST-"));
        }, timeout: TimeSpan.FromSeconds(10));

        // The transcript is behind a disclosure, so open it and read what it admits to.
        cut.FindAll("button").First(b => b.TextContent.Contains("Show details")).Click();
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("was NOT opened by anything independent");
        }, timeout: TimeSpan.FromSeconds(10));
    }

    // Failed is not Unavailable: it means this build produced an envelope GnuPG cannot
    // open, which is a fact about the code rather than about the machine. That refuses
    // even for a practice backup, because a practice run whose output is malformed is
    // exactly the signal the gate exists to raise.
    [Fact]
    public void Generate_WhenGnuPgRejectsTheEnvelope_RefusesEvenATestBackup()
    {
        var downloader = new NoopDownloader();
        Services.AddSingleton<IFileDownloader>(downloader);
        Services.AddSingleton<IOuterLockVerifier>(new FakeOuterLockVerifier(
            OuterLockOutcome.Failed, "gpg: decryption failed: Bad session key"));

        var cut = RenderComponent<Owner>();
        cut.FindAll(TopLevelSeedSelector).First()
            .Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
        AttestOffline(cut, attest: false); // INSECURE-TEST, and still refused

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".banner-loud").Should().Contain(el =>
                el.TextContent.Contains("REFUSED") && el.TextContent.Contains("Bad session key"));
        }, timeout: TimeSpan.FromSeconds(10));

        downloader.Calls.Should().BeEmpty();
    }

    // What the gate is handed matters as much as what it answers. The armored envelope and
    // the age file that was wrapped in it are two different things, and passing the same
    // bytes twice would make the comparison pass for free.
    [Fact]
    public void Generate_HandsTheOuterLockCheckTheEnvelopeAndTheInnerAgeFile()
    {
        var verifier = new FakeOuterLockVerifier();
        Services.AddSingleton<IFileDownloader>(new NoopDownloader());
        Services.AddSingleton<IOuterLockVerifier>(verifier);

        var cut = RenderComponent<Owner>();
        cut.FindAll(TopLevelSeedSelector).First()
            .Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
        AttestOffline(cut);

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            verifier.Calls.Should().ContainSingle();
            verifier.Calls[0].Armored.Should().StartWith("-----BEGIN PGP MESSAGE-----");
            verifier.Calls[0].ExpectedInnerLength.Should().BeGreaterThan(0);
            verifier.Calls[0].KeyLength.Should().Be(32);
        }, timeout: TimeSpan.FromSeconds(10));
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
        cut.FindAll(TopLevelSeedSelector).First()
            .Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".banner-loud").Should().Contain(el => el.TextContent.Contains("REFUSED"));
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
        cut.FindAll(TopLevelSeedSelector).First()
            .Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            verifier.Calls.Should().ContainSingle();
            verifier.Calls[0].SubsetCount.Should().Be(2);
            verifier.Calls[0].PayloadLength.Should().BeGreaterThan(0);
            // Case-insensitive: this is a row label in the result panel now, so its
            // capitalisation is presentation and should not be what the test pins.
            cut.Markup.Should().ContainEquivalentOf("independently verified");
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

        // TopLevelSeedSelector targets Owner's own top-level seed field specifically
        // (cosigner seed/derivation inputs have a different placeholder).
        cut.FindAll(TopLevelSeedSelector).First()
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
        cut.FindAll(TopLevelSeedSelector).First()
            .Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");

        // Add a second cosigner (identical to the first: no own seed, no passphrase, same path).
        cut.FindAll("button").First(b => b.TextContent.Contains("Add cosigner")).Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".banner-loud").Should().Contain(el => el.TextContent.Contains("same key"));
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
        cut.FindAll(TopLevelSeedSelector).First()
            .Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
        cut.FindAll("button").First(b => b.TextContent.Contains("Add cosigner")).Click();

        // Re-query before each change: every .Change re-renders the live form, so a
        // cached node list would go stale.
        cut.FindAll("input[placeholder='Leave empty if no passphrase']")[0].Change("t1");
        cut.FindAll("input[placeholder='Leave empty if no passphrase']")[1].Change("t2");

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".banner-loud").Should().BeEmpty();
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

        // TopLevelSeedSelector targets Owner's own top-level seed field by
        // placeholder text, so it stays correct regardless of which CSS class
        // either this field or a cosigner's own seed field carries.
        var seedInput = cut.FindAll(TopLevelSeedSelector).First();
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
        cut.FindAll(TopLevelSeedSelector).First()
            .Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
        AttestOffline(cut, attest: false);

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            downloader.Calls.Should().ContainSingle(c => c.Filename.StartsWith("INSECURE-TEST-"));
            // Asserted on the words, not the punctuation: the result panel now renders
            // labelled rows rather than one sentence, so what matters is that the page
            // still says INSECURE-TEST and still says practice only.
            cut.Markup.Should().Contain("INSECURE-TEST backup");
            cut.Markup.Should().Contain("Practice only");
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
        cut.FindAll(TopLevelSeedSelector).First()
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

    // A native save dialog can be cancelled, unlike the browser blob download it
    // replaced. The bug this guards against: the page used to claim "Backup
    // created" regardless of whether anything reached disk. It must not, now that
    // DownloadAsync can say so.
    [Fact]
    public void Generate_WhenSaveIsCancelled_DoesNotClaimBackupCreated()
    {
        var downloader = new NoopDownloader(false);
        Services.AddSingleton<IFileDownloader>(downloader);

        var cut = RenderComponent<Owner>();
        cut.FindAll(TopLevelSeedSelector).First()
            .Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
        AttestOffline(cut);

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            downloader.Calls.Should().ContainSingle();
            cut.Markup.Should().NotContain("Backup created.");
            // "Wallet master fingerprint" only ever renders inside the success panel
            // (it is not the ConnectivityBanner's own, unrelated banner-ok), so its
            // absence is the stronger check that the whole success panel is gone,
            // not only its headline.
            cut.Markup.Should().NotContain("Wallet master fingerprint");
        }, timeout: TimeSpan.FromSeconds(10));
    }

    // The other half of the same bug: a cancelled save must say so in words, not
    // just skip the success banner, and must offer a way to save the same backup
    // again without regenerating it.
    [Fact]
    public void Generate_WhenSaveIsCancelled_ShowsWarningAndSaveAgainButton()
    {
        var downloader = new NoopDownloader(false);
        Services.AddSingleton<IFileDownloader>(downloader);

        var cut = RenderComponent<Owner>();
        cut.FindAll(TopLevelSeedSelector).First()
            .Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
        AttestOffline(cut);

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Nothing was saved");
            cut.Markup.Should().Contain("backup already generated still exists");
            cut.FindAll("button").Should().Contain(b => b.TextContent.Contains("Save again"));
        }, timeout: TimeSpan.FromSeconds(10));
    }

    // The property that proves a mis-clicked Cancel costs nothing: retrying sends
    // the identical bytes under the identical filename, not a freshly generated
    // (and therefore different) backup.
    [Fact]
    public void SaveAgain_AfterACancelledSave_ResendsTheSameBytesAndFilename()
    {
        var downloader = new NoopDownloader(false, true);
        Services.AddSingleton<IFileDownloader>(downloader);

        var cut = RenderComponent<Owner>();
        cut.FindAll(TopLevelSeedSelector).First()
            .Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
        AttestOffline(cut);

        cut.Find("button.btn-primary").Click();
        cut.WaitForAssertion(() => downloader.Calls.Should().ContainSingle(),
            timeout: TimeSpan.FromSeconds(10));

        cut.FindAll("button").First(b => b.TextContent.Contains("Save again")).Click();

        cut.WaitForAssertion(() =>
        {
            downloader.Calls.Should().HaveCount(2);
            downloader.Calls[1].Filename.Should().Be(downloader.Calls[0].Filename);
            downloader.Calls[1].Bytes.Should().Equal(downloader.Calls[0].Bytes);
            cut.Markup.Should().Contain("Backup created.");
        }, timeout: TimeSpan.FromSeconds(10));
    }

    // The unglamorous control case: a save that succeeds on the first try must
    // still look exactly as it did before any of this, cancellation banner
    // included nowhere.
    [Fact]
    public void Generate_WhenSaveSucceeds_ShowsBackupCreatedAndNoCancelWarning()
    {
        var downloader = new NoopDownloader(true);
        Services.AddSingleton<IFileDownloader>(downloader);

        var cut = RenderComponent<Owner>();
        cut.FindAll(TopLevelSeedSelector).First()
            .Change("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
        AttestOffline(cut);

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Backup created.");
            cut.Markup.Should().Contain("Wallet master fingerprint");
            cut.Markup.Should().NotContain("Nothing was saved");
            cut.FindAll("button").Should().NotContain(b => b.TextContent.Contains("Save again"));
        }, timeout: TimeSpan.FromSeconds(10));
    }
}
