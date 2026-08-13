using System.Reflection;
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
// Deliberately does not name TauriAgeEncryptor, which is what satisfies the invariant
// today. "Declared in Slip39Demo.Tauri" is the property; naming the current implementation
// would make the test pass for the wrong reason the moment it is replaced. Non-public types
// count too, because GetTypes() returns them.
public class AppImageEncryptorReachabilityTests
{
    // The transitive closure of this repository's own assemblies, starting from the
    // AppImage frontend. Walked rather than listed: a hardcoded list of the three
    // projects that happen to be linked today would not notice a fourth being added,
    // and adding a project reference is exactly how an in-process encryptor would come
    // back. Only Slip39Demo.* assemblies are followed, because the framework and package
    // graph is large and cannot define an IPayloadEncryptor: the interface is ours.
    static IReadOnlyCollection<Assembly> OurAssembliesLinkedFrom(Assembly root)
    {
        var found = new Dictionary<string, Assembly> { [root.GetName().Name!] = root };
        var pending = new Queue<Assembly>([root]);

        while (pending.Count > 0)
            foreach (var reference in pending.Dequeue().GetReferencedAssemblies()
                         .Where(name => name.Name?.StartsWith("Slip39Demo") == true)
                         .Where(name => !found.ContainsKey(name.Name!)))
            {
                var loaded = Assembly.Load(reference);
                found[reference.Name!] = loaded;
                pending.Enqueue(loaded);
            }

        return found.Values;
    }

    // The same invariant for the outer-lock check, and for the same reason. The layer this
    // verifies was written in-process by BouncyCastle, so the only implementation that can
    // vouch for it is one from outside this repository: the system's GnuPG, reached through
    // the Rust shell. An IOuterLockVerifier declared anywhere else and reachable from the
    // AppImage would be a candidate for a future "fall back to checking it ourselves",
    // which always passes and therefore reads as evidence while proving nothing.
    //
    // BrowserOuterLockVerifier lives in Slip39Demo.Web, which the AppImage does not link,
    // so it cannot be the thing that answers here.
    [Fact]
    public void Every_outer_lock_verifier_reachable_from_the_appimage_frontend_is_declared_in_it()
    {
        var tauriAssembly = typeof(Slip39Demo.Tauri.Services.TauriInterop).Assembly;
        var linked = OurAssembliesLinkedFrom(tauriAssembly);

        linked.Select(assembly => assembly.GetName().Name)
            .Should().Contain("Slip39Demo.UI",
                "the walk must reach the shared UI, or it is not examining anything");

        var offenders = linked
            .SelectMany(assembly => assembly.GetTypes()
                .Where(type => !type.IsAbstract && !type.IsInterface)
                .Where(type => typeof(IOuterLockVerifier).IsAssignableFrom(type))
                .Select(type => (type, assembly)))
            .Where(found => found.assembly != tauriAssembly)
            .Select(found => $"{found.type.FullName} (in {found.assembly.GetName().Name})")
            .ToArray();

        offenders.Should().BeEmpty(
            "every IOuterLockVerifier reachable from the AppImage frontend must be declared "
            + $"in Slip39Demo.Tauri; found declared elsewhere: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Every_payload_encryptor_reachable_from_the_appimage_frontend_is_declared_in_it()
    {
        var tauriAssembly = typeof(Slip39Demo.Tauri.Services.TauriInterop).Assembly;
        var linked = OurAssembliesLinkedFrom(tauriAssembly);

        // Without this the test could pass by finding nothing to examine. If the walk
        // ever returns the frontend alone, every assertion below is vacuously true and
        // an in-process encryptor could sit in Slip39Demo.UI unnoticed, which is the
        // exact arrangement this file was written to end.
        linked.Select(assembly => assembly.GetName().Name)
            .Should().Contain("Slip39Demo.UI",
                "the walk must reach the shared UI, or it is not examining anything");

        var encryptorTypesByAssembly = linked
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
