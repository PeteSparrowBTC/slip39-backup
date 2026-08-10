namespace Slip39Demo.UI.Services;

// Abstraction over "hand these bytes to the platform's save mechanism", whether
// that is a browser blob download or a native save dialog. Tests substitute a
// fake that records the call.
public interface IFileDownloader
{
    // Attempts to save bytes under filename and reports whether a file actually
    // resulted.
    //
    // True means the bytes reached a file: in the Tauri shell this is checked by
    // reading the path the save dialog returned, and in the browser build it
    // means the blob was handed off (see BrowserFileDownloader for the limit of
    // what that build can actually observe).
    //
    // False means no file was written. This is a normal outcome, not an error:
    // the caller offered to save and the user declined, most often by cancelling
    // or closing a native dialog. Callers must not treat false as success and
    // must not discard the bytes on false, since the same save may be retried.
    ValueTask<bool> DownloadAsync(string filename, byte[] bytes, string mimeType);
}
