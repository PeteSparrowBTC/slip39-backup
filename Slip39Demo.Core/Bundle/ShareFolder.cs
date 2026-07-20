using System.Text;

namespace Slip39Demo.Core.Bundle;

// Composes the file contents of one share folder. Each share folder contains:
//   - README.txt          the recovery instructions (from ReadmeTemplate.Build)
//   - share.slip39        the SLIP-39 mnemonic (a single line of words)
//   - share-qr.png        the same mnemonic as a plain-text QR code, so the
//                         share can be printed and later scanned back with any
//                         QR app instead of retyping 33 words
//   - MANUAL-RECOVERY.txt tool-independent recovery manual (install + usage)
// The manual travels with EVERY share because at recovery time the executor
// holds gathered share zips + payload.age — possibly not our tool or bundle.
// No payload.age — that travels a completely separate path (per spec §5.1).
// The result is a read-only file map; the actual serialisation (folder on disk,
// zip in memory) is the responsibility of downstream code.
public static class ShareFolder
{
    public static IReadOnlyDictionary<string, byte[]> Build(string readmeText, string mnemonicText) =>
        new Dictionary<string, byte[]>
        {
            ["README.txt"]                  = Encoding.UTF8.GetBytes(readmeText),
            ["share.slip39"]                = Encoding.UTF8.GetBytes(mnemonicText.TrimEnd() + "\n"),
            ["share-qr.png"]                = ShareQr.BuildPng(mnemonicText),
            [ManualRecoveryGuide.FileName]  = Encoding.UTF8.GetBytes(ManualRecoveryGuide.Text),
        };
}
