using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Slip39Demo.Web.Pages;
using Slip39Demo.Web.Services;
using Xunit;

namespace Slip39Demo.Tests.Web;

// bUnit component tests for the Owner page form. These exercise the failure
// path (no seed -> visible error, no download) and the happy path (valid seed
// -> exactly one download call with the expected filename/mime). The
// IFileDownloader is replaced with a fake that records each call so we never
// touch JS interop in tests.
public class OwnerFormValidationTests : TestContext
{
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
    public void Generate_WithValidSeed_TriggersDownload()
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

        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            downloader.Calls.Should().ContainSingle(c => c.Filename == "output.zip" && c.Mime == "application/zip");
        }, timeout: TimeSpan.FromSeconds(10));
    }
}
