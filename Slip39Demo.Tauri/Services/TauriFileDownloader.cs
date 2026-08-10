using Slip39Demo.UI.Services;

namespace Slip39Demo.Tauri.Services;

// A native save dialog instead of the browser blob-and-anchor mechanism. The bytes
// cross the interop boundary base64 encoded, because the JSON bridge cannot carry a
// byte array. mimeType is accepted for interface compatibility and ignored: the file
// picker takes the extension from the suggested name.
public sealed class TauriFileDownloader(ITauriInterop interop) : IFileDownloader
{
    public async ValueTask<bool> DownloadAsync(string filename, byte[] bytes, string mimeType)
    {
        var path = await interop.InvokeAsync<string?>("save_file", new
        {
            suggestedName = filename,
            bytesB64 = Convert.ToBase64String(bytes),
        });

        // save_file (src-tauri/src/save.rs) returns the path only after it has
        // written and fsynced the bytes there, and returns null when the user
        // cancelled the dialog. Still: the path is a string that crossed an
        // interop boundary, and the whole point of this fix is not to trust a
        // claim of success without a check, so confirm the file is really there
        // rather than relying on the string alone.
        return path is not null && File.Exists(path);
    }
}
