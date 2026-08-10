using Slip39Demo.UI.Services;

namespace Slip39Demo.Tauri.Services;

// A native save dialog instead of the browser blob-and-anchor mechanism. The bytes
// cross the interop boundary base64 encoded, because the JSON bridge cannot carry a
// byte array. mimeType is accepted for interface compatibility and ignored: the file
// picker takes the extension from the suggested name.
public sealed class TauriFileDownloader(ITauriInterop interop) : IFileDownloader
{
    public async ValueTask DownloadAsync(string filename, byte[] bytes, string mimeType) =>
        await interop.InvokeAsync<string?>("save_file", new
        {
            suggestedName = filename,
            bytesB64 = Convert.ToBase64String(bytes),
        });
}
