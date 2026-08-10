using FluentAssertions;
using Slip39Demo.UI.Services;
using Xunit;

namespace Slip39Demo.Tests.Tauri;

// Pins a structural property of the AppImage frontend: every IPayloadEncryptor reachable
// from an assembly Slip39Demo.Tauri links is declared in Slip39Demo.Tauri itself. That is
// what makes the claim in Slip39Demo.Tauri.csproj ("this project never references
// AgeSharpPayloadEncryptor, so the artifact that touches real seed phrases cannot fall
// back to encrypting in-process") structural rather than a matter of what Program.cs
// happens to register today.
//
// Checked by reflection over the linked assemblies rather than by reading Program.cs's
// registrations, because the risk this guards against is a future edit that adds a
// second IPayloadEncryptor and wires it up; a test that read the registration list would
// have to be updated by the very edit it exists to catch, which defeats the point.
//
// Deliberately does not name TauriAgeEncryptor, which does not exist until a later task.
// "Declared in Slip39Demo.Tauri" is the invariant, and it holds for the current
// NotWiredYet placeholder in Program.cs (internal, and still counted: GetTypes()
// includes non-public types) exactly as it will for whatever replaces it.
public class AppImageEncryptorReachabilityTests
{
    [Fact]
    public void Every_payload_encryptor_reachable_from_the_appimage_frontend_is_declared_in_it()
    {
        var tauriAssembly = typeof(Slip39Demo.Tauri.Services.TauriInterop).Assembly;
        var uiAssembly = typeof(IPayloadEncryptor).Assembly;
        var coreAssembly = typeof(Slip39Demo.Core.Age.AgePassphrase).Assembly;

        var encryptorTypesByAssembly = new[] { tauriAssembly, uiAssembly, coreAssembly }
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes()
                .Where(type => !type.IsAbstract && !type.IsInterface)
                .Where(type => typeof(IPayloadEncryptor).IsAssignableFrom(type))
                .Select(type => (type, assembly)));

        var offenders = encryptorTypesByAssembly
            .Where(found => found.assembly != tauriAssembly)
            .Select(found => $"{found.type.FullName} (in {found.assembly.GetName().Name})")
            .ToArray();

        offenders.Should().BeEmpty(
            "every IPayloadEncryptor reachable from the AppImage frontend must be declared "
            + $"in Slip39Demo.Tauri; found declared elsewhere: {string.Join(", ", offenders)}");
    }
}
