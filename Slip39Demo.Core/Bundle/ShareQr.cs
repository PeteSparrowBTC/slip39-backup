using QRCoder;

namespace Slip39Demo.Core.Bundle;

// Renders a share mnemonic as a QR code PNG. The QR payload is the PLAIN
// mnemonic text — deliberately no wrapper format: SLIP-39 has no standard QR
// encoding, so plain-text-in-QR is the only choice every scanner app decodes
// straight back into the words (research: PaperAge/SeedQR interop lesson).
//
// ECC level Q (~25% damage recovery) balances print durability against QR
// density for a ~200-character mnemonic. QRCoder's PngByteQRCode is pure
// managed code — no System.Drawing — so it runs under Blazor WASM.
public static class ShareQr
{
    // Pixels per QR module; 10 gives a comfortably scannable ~450px image for a
    // 33-word mnemonic when printed at typical sheet sizes.
    const int PixelsPerModule = 10;

    public static byte[] BuildPng(string mnemonicText)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(mnemonicText.Trim(), QRCodeGenerator.ECCLevel.Q);
        using var png = new PngByteQRCode(data);
        return png.GetGraphic(PixelsPerModule);
    }
}
