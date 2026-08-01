using Slip39Demo.UI.Services;

namespace Slip39Demo.Desktop.Services;

// Airgap probe from kernel state: any non-loopback interface whose
// /sys/class/net/<if>/operstate is not "down" counts as potentially online.
// Stronger than the web demo's navigator.onLine (kernel truth, not webview
// state) and deliberately pessimistic: "unknown" counts as online, and any
// error reports ONLINE so generation falls into the INSECURE-TEST watermark
// path instead of silently passing as airgapped.
public sealed class LinuxConnectivityProbe : IConnectivityProbe
{
    public Task<bool> IsOnlineAsync()
    {
        try
        {
            var online = Directory.EnumerateDirectories("/sys/class/net")
                .Where(dir => Path.GetFileName(dir) != "lo")
                .Select(dir => Path.Combine(dir, "operstate"))
                .Where(File.Exists)
                .Select(f => File.ReadAllText(f).Trim())
                .Any(state => state != "down");
            return Task.FromResult(online);
        }
        catch
        {
            return Task.FromResult(true);
        }
    }
}
