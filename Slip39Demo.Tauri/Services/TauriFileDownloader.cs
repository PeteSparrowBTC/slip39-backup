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

        // save_file (src-tauri/src/save.rs) returns the path only after it has written the
        // bytes, fsynced them to the device and renamed the file into place, and returns
        // null when the user cancelled the dialog. So a non-null path IS the confirmation,
        // and this method's only job is to pass that distinction on.
        //
        // Do NOT add a File.Exists check here, however tempting "verify rather than trust"
        // sounds. This assembly runs as WebAssembly inside the webview, where System.IO
        // sees the runtime's virtual filesystem and not the machine's: File.Exists on a real
        // host path like /home/amnesia/backup.zip returns false no matter what was written.
        // A check like that passes in the test project, which runs on a normal .NET host with
        // a real filesystem, and then reports every successful save as a failure in the only
        // build that matters. It was written and removed before it shipped; the checking
        // belongs on the Rust side, where the file actually is, and that is where it is.
        return path is not null;
    }
}
