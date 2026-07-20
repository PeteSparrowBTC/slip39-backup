using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Slip39Demo.Core.Bundle;
using Xunit;
using ZXing.ImageSharp;

namespace Slip39Demo.Tests.Bundle;

// A QR that renders but does not SCAN is false durability — so these tests
// decode the generated PNG with an independent reader (ZXing) and require the
// exact mnemonic text back. Rendering is QRCoder; reading is ZXing; the two
// share no code.
public class ShareQrTests
{
    // Realistic 33-word SLIP-39-shaped share line (content doesn't need to be a
    // valid share for QR round-tripping — length and charset are what matter).
    const string Mnemonic =
        "canyon lilac academic acne agency metric repeat helpful alive dining bracelet volume "
        + "scroll lizard talent owner purchase jerky galaxy glasses depart dramatic black stilt "
        + "swimming smith race golden retailer flash ruler involve sled";

    [Fact]
    public void BuildPng_ProducesScannableQr_RoundTrippingTheMnemonic()
    {
        var png = ShareQr.BuildPng(Mnemonic);

        // PNG magic bytes — it really is a PNG.
        png[..8].Should().BeEquivalentTo(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        // Decode with an independent reader and demand the exact text back.
        using var image = Image.Load<Rgba32>(png);
        var reader = new BarcodeReader<Rgba32>();
        var result = reader.Decode(image);

        result.Should().NotBeNull("the QR must be scannable, not just renderable");
        result!.Text.Should().Be(Mnemonic);
    }

    [Fact]
    public void ShareFolder_IncludesQrPng()
    {
        var files = ShareFolder.Build("readme", Mnemonic);

        files.Should().ContainKey("share-qr.png");
        files["share-qr.png"].Length.Should().BeGreaterThan(200);
    }
}
