using Microsoft.JSInterop;

namespace Slip39Demo.Web.Services;

// Bridges to window.SPSConn.isOnline (wwwroot/connectivity.js). Fail-safe: any
// interop error is reported as ONLINE so generation falls into the
// INSECURE-TEST watermark path instead of silently passing as airgapped.
public sealed class JsConnectivityProbe(IJSRuntime js) : IConnectivityProbe
{
    public async Task<bool> IsOnlineAsync()
    {
        try
        {
            return await js.InvokeAsync<bool>("SPSConn.isOnline");
        }
        catch
        {
            return true;
        }
    }
}
