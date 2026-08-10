using Microsoft.JSInterop;

namespace Slip39Demo.Tauri.Services;

// Calls window.__TAURI__.core.invoke, which exists because tauri.conf.json sets
// withGlobalTauri. Blazor loads no bundler, so the module import form of the Tauri
// API is not available here.
public sealed class TauriInterop(IJSRuntime js) : ITauriInterop
{
    public ValueTask<T> InvokeAsync<T>(string command, object? args = null) =>
        js.InvokeAsync<T>("__TAURI__.core.invoke", command, args ?? new { });
}
