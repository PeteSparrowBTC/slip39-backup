namespace Slip39Demo.Tauri.Services;

// The single seam between the WebAssembly app and the Rust shell. It exists so the
// three services can be tested against a fake instead of a live WebView.
public interface ITauriInterop
{
    ValueTask<T> InvokeAsync<T>(string command, object? args = null);
}
