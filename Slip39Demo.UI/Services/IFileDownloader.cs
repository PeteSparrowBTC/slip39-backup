namespace Slip39Demo.UI.Services;

// Abstraction over "trigger a browser file download". The concrete impl
// uses JS interop; tests can substitute a fake that records the call.
public interface IFileDownloader
{
    ValueTask DownloadAsync(string filename, byte[] bytes, string mimeType);
}
