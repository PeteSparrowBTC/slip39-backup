using Slip39Demo.Tauri.Services;
using Xunit;

namespace Slip39Demo.Tests.Tauri;

public class TauriConnectivityProbeTests
{
    sealed class FakeInterop(Func<string, object?> handler) : ITauriInterop
    {
        public ValueTask<T> InvokeAsync<T>(string command, object? args = null) =>
            handler(command) is T value
                ? ValueTask.FromResult(value)
                : throw new InvalidOperationException($"unexpected command {command}");
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
