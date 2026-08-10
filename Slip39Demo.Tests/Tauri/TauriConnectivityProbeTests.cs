using Slip39Demo.Tauri.Services;
using Xunit;

namespace Slip39Demo.Tests.Tauri;

public class TauriConnectivityProbeTests
{
    // The command name the Rust shell registers. Asserted rather than ignored: a review
    // pointed out that these fakes answered whatever they were asked, so renaming the Rust
    // command to isOnline would have left every test and CI green while the probe threw at
    // runtime, was caught by the deliberately broad catch, and reported online, watermarking
    // every backup made on a genuinely airgapped machine INSECURE-TEST. A wrong watermark is
    // not a data loss, but it teaches the user to distrust the one signal that tells them
    // whether their backup is real.
    const string Command = "is_online";

    sealed class FakeInterop(Func<string, object?> handler) : ITauriInterop
    {
        public ValueTask<T> InvokeAsync<T>(string command, object? args = null)
        {
            Assert.Equal(Command, command);
            return handler(command) is T value
                ? ValueTask.FromResult(value)
                : throw new InvalidOperationException($"unexpected command {command}");
        }
    }

    sealed class ThrowingInterop : ITauriInterop
    {
        public ValueTask<T> InvokeAsync<T>(string command, object? args = null) =>
            throw new InvalidOperationException("the shell is not answering");
    }

    [Fact]
    public async Task Reports_offline_when_the_shell_says_so() =>
        Assert.False(await new TauriConnectivityProbe(new FakeInterop(_ => false)).IsOnlineAsync());

    [Fact]
    public async Task Reports_online_when_the_shell_says_so() =>
        Assert.True(await new TauriConnectivityProbe(new FakeInterop(_ => true)).IsOnlineAsync());

    // The direction that matters. An unanswerable probe must count as online, so the
    // backup is watermarked INSECURE-TEST rather than silently passing as airgapped.
    [Fact]
    public async Task Reports_online_when_the_check_cannot_run() =>
        Assert.True(await new TauriConnectivityProbe(new ThrowingInterop()).IsOnlineAsync());
}
