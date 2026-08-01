using Slip39Demo.UI.Services;

namespace Slip39Demo.Desktop.Services;

// Airgap probe from kernel state: online = any non-loopback interface with a
// live carrier (/sys/class/net/<if>/carrier == "1" — cable with signal, or
// associated Wi-Fi). Carrier is used instead of operstate because idle NIC
// drivers commonly report operstate "unknown", which false-positives an
// airgapped Tails machine as online.
//
// Fail-safe direction: if /sys can't be enumerated at all, report ONLINE so
// generation falls into the INSECURE-TEST watermark path instead of silently
// passing as airgapped. A single unreadable carrier file, however, means that
// interface is admin-down (the kernel refuses the read) — no link possible.
public sealed class LinuxConnectivityProbe : IConnectivityProbe
{
    public Task<bool> IsOnlineAsync()
    {
        try
        {
            var online = Directory.EnumerateDirectories("/sys/class/net")
                .Where(dir => Path.GetFileName(dir) != "lo")
                .Any(HasLiveCarrier);
            return Task.FromResult(online);
        }
        catch
        {
            return Task.FromResult(true);
        }
    }

    static bool HasLiveCarrier(string dir)
    {
        try
        {
            return File.ReadAllText(Path.Combine(dir, "carrier")).Trim() == "1";
        }
        catch
        {
            return false;
        }
    }
}
