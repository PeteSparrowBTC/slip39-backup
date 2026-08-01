using Photino.NET;
using Slip39Demo.UI.Services;

namespace Slip39Demo.Desktop.Services;

// Native GTK save dialog + direct file write — replaces the browser's
// Blob/<a download> path. Cancelling the dialog is a silent no-op, matching
// the browser behaviour where an aborted save just does nothing.
public sealed class NativeFileDownloader(PhotinoWindow window) : IFileDownloader
{
    public async ValueTask DownloadAsync(string filename, byte[] bytes, string mimeType)
    {
        var suggested = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), filename);
        var path = await window.ShowSaveFileAsync("Save file", suggested);
        if (path is not null) await File.WriteAllBytesAsync(path, bytes);
    }
}
