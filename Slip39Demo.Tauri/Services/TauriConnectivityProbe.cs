using Slip39Demo.UI.Services;

namespace Slip39Demo.Tauri.Services;

// Airgap probe. The kernel state it reads is described in src-tauri/src/net.rs; this
// class exists to enforce the fail-safe direction on the C# side as well, so an
// interop failure cannot be mistaken for an offline machine.
public sealed class TauriConnectivityProbe(ITauriInterop interop) : IConnectivityProbe
{
    public async Task<bool> IsOnlineAsync()
    {
        try
        {
            return await interop.InvokeAsync<bool>("is_online");
        }
        catch
        {
            // Fail toward danger: unknown means watermark the output.
            return true;
        }
    }
}
