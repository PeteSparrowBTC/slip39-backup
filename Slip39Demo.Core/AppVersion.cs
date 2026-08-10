using System.Reflection;

namespace Slip39Demo.Core;

// The one place the application's version is read from.
//
// WHY THIS EXISTS
// The version was previously a literal "2.0.0" written twice in Owner.razor, once for the
// share README and once for the verification record, with nothing keeping them equal and
// nothing showing either to the user. Those two documents sit side by side inside every
// backup, and a reader comparing them has no way to tell which claim is stale.
//
// The value comes from MSBuild, so Directory.Build.props is the single source and a tagged
// CI build can stamp it from the tag. That is what makes the demo page and the AppImage
// published from one tag report one version.
public static class AppVersion
{
    // Computed once. The assembly's attributes cannot change while the process runs, and
    // this is read on every page render through the layout footer.
    public static string Current { get; } = Read();

    static string Read()
    {
        // The informational version rather than AssemblyVersion: AssemblyVersion is the
        // four-part numeric form and drops any prerelease suffix, so "2.1.0-rc.1" would
        // silently display as "2.1.0" and misreport a release candidate as a release.
        var informational = typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
            return typeof(AppVersion).Assembly.GetName().Version?.ToString(3) ?? "unknown";

        // The SDK appends "+<commit sha>" as source-control metadata. It is useful in a
        // build log and noise in a footer, so it is trimmed rather than shown.
        var metadata = informational.IndexOf('+');
        return metadata < 0 ? informational : informational[..metadata];
    }
}
