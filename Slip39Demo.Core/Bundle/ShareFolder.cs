using System.Text;

namespace Slip39Demo.Core.Bundle;

// Composes the file contents of one share folder. Each share folder contains:
//   - README.txt   the recovery instructions (from ReadmeTemplate.Build)
//   - share.slip39 the SLIP-39 mnemonic (a single line of words)
// No payload.age — that travels a completely separate path (per spec §5.1).
// The result is a read-only file map; the actual serialisation (folder on disk,
// zip in memory) is the responsibility of downstream code.
public static class ShareFolder
{
    public static IReadOnlyDictionary<string, byte[]> Build(string readmeText, string mnemonicText) =>
        new Dictionary<string, byte[]>
        {
            ["README.txt"]   = Encoding.UTF8.GetBytes(readmeText),
            ["share.slip39"] = Encoding.UTF8.GetBytes(mnemonicText.TrimEnd() + "\n"),
        };
}
