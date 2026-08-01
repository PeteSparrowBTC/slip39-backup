using Microsoft.JSInterop;
using Slip39Demo.UI.Services;

namespace Slip39Demo.Web.Services;

// Hands bytes to the spsDownload JS helper which builds a Blob and clicks an
// <a download> to trigger the browser's Save dialog. Base64-encoded because
// JSRuntime serialises byte[] as a JSON array (slow + huge for binary).
public sealed class BrowserFileDownloader(IJSRuntime js) : IFileDownloader
{
    public ValueTask DownloadAsync(string filename, byte[] bytes, string mimeType) =>
        js.InvokeVoidAsync("spsDownload", filename, Convert.ToBase64String(bytes), mimeType);
}
