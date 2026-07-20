namespace Slip39Demo.Web.Services;

// Answers "is the internet reachable from this machine right now?" for the
// airgap warning + INSECURE-TEST watermark gate. True = online (danger).
// Implementations must fail SAFE: if the check cannot run, report online so
// the backup gets watermarked rather than silently trusted.
public interface IConnectivityProbe
{
    Task<bool> IsOnlineAsync();
}
