using Microsoft.JSInterop;
using Slip39Demo.UI.Services;

namespace Slip39Demo.Web.Services;

// Hands bytes to the spsDownload JS helper which builds a Blob and clicks an
// <a download> to trigger the browser's Save dialog. Base64-encoded because
// JSRuntime serialises byte[] as a JSON array (slow + huge for binary).
public sealed class BrowserFileDownloader(IJSRuntime js) : IFileDownloader
{
    public async ValueTask<bool> DownloadAsync(string filename, byte[] bytes, string mimeType)
    {
        await js.InvokeVoidAsync("spsDownload", filename, Convert.ToBase64String(bytes), mimeType);

        // Handing the blob to the browser is the last thing this build can see.
        // There is no callback for "the user kept the file" versus "the user
        // cancelled the browser's own save prompt" or navigated away, so true
        // here reports only that the handoff happened, not that a file exists.
        // That gap is one more reason this build is DEMONSTRATION AND TESTING
        // ONLY; the Tauri shell can and does check the real outcome.
        return true;
    }
}
