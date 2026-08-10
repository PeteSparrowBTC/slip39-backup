using System.Text;
using Slip39Demo.Tauri.Services;
using Slip39Demo.UI.Services;
using Xunit;

namespace Slip39Demo.Tests.Tauri;

public class TauriAgeEncryptorTests
{
    static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    sealed class StubInterop(AgeRunDto result) : ITauriInterop
    {
        public ValueTask<T> InvokeAsync<T>(string command, object? args = null) =>
            ValueTask.FromResult((T)(object)result);
    }

    static AgeRunDto Good(byte[] ciphertext) => new()
    {
        ExitCode = 0,
        StdoutB64 = Convert.ToBase64String(ciphertext),
        StdoutText = "v1.3.1",
        StderrText = "",
        AgePath = "/tmp/.mount_x/usr/bin/age/age",
        AgeSha256 = "abc123",
        PluginPath = "/tmp/.mount_x/usr/bin/age/age-plugin-batchpass",
        AgeMissing = false,
        PluginMissing = false,
    };

    static byte[] ValidAgeFile() =>
        Encoding.ASCII.GetBytes("age-encryption.org/v1\n-> batchpass\nbody");

    [Fact]
    public async Task Rejects_a_key_that_is_not_32_bytes()
    {
        var result = await new TauriAgeEncryptor(new StubInterop(Good(ValidAgeFile())))
            .EncryptAsync([1, 2, 3], new byte[16]);

        Assert.True(result.IsFailure);
        Assert.Contains("32 bytes", result.Error);
    }

    // The rule the whole class exists for.
    [Fact]
    public async Task Missing_binary_fails_rather_than_falling_back()
    {
        var missing = Good([]) with { AgeMissing = true };

        var result = await new TauriAgeEncryptor(new StubInterop(missing)).EncryptAsync([1], Key);

        Assert.True(result.IsFailure);
        Assert.Contains("missing", result.Error);
        Assert.Contains("refuses to fall back", result.Error);
    }

    // Reported on its own, because the plugin is the half that gets forgotten and the
    // message has to name the right file.
    [Fact]
    public async Task Missing_plugin_names_the_plugin()
    {
        var missing = Good([]) with { PluginMissing = true };

        var result = await new TauriAgeEncryptor(new StubInterop(missing)).EncryptAsync([1], Key);

        Assert.True(result.IsFailure);
        Assert.Contains("age-plugin-batchpass", result.Error);
    }

    [Fact]
    public async Task Nonzero_exit_fails()
    {
        var failed = Good([]) with { ExitCode = 1, StderrText = "age: bad passphrase" };

        var result = await new TauriAgeEncryptor(new StubInterop(failed)).EncryptAsync([1], Key);

        Assert.True(result.IsFailure);
        Assert.Contains("bad passphrase", result.Error);
    }

    [Fact]
    public async Task Output_that_is_not_an_age_file_fails()
    {
        var wrong = Good(Encoding.ASCII.GetBytes("this is not an age file at all"));

        var result = await new TauriAgeEncryptor(new StubInterop(wrong)).EncryptAsync([1], Key);

        Assert.True(result.IsFailure);
        Assert.Contains("age v1 header", result.Error);
    }

    [Fact]
    public async Task Empty_output_fails()
    {
        var result = await new TauriAgeEncryptor(new StubInterop(Good([]))).EncryptAsync([1], Key);

        Assert.True(result.IsFailure);
        Assert.Contains("no output", result.Error);
    }

    [Fact]
    public async Task Transcript_shows_the_binary_its_hash_and_the_command()
    {
        var result = await new TauriAgeEncryptor(new StubInterop(Good(ValidAgeFile())))
            .EncryptAsync(Encoding.UTF8.GetBytes("payload"), Key);

        Assert.True(result.IsSuccess);
        var text = string.Join("\n", result.Value.Transcript.Lines.Select(l => l.Text));
        Assert.Contains("/usr/bin/age/age", text);
        Assert.Contains("abc123", text);
        Assert.Contains("--encrypt -j batchpass", text);
        Assert.Contains("AGE_PASSPHRASE", text);
        Assert.Contains(TranscriptLineKind.Warning, result.Value.Transcript.Lines.Select(l => l.Kind));
    }

    // The key must never appear in anything shown to the user, because the transcript is
    // exactly what somebody pastes into a bug report when a backup will not recover.
    //
    // Every part of the transcript, and every spelling. The version this replaces checked
    // only Lines, and only lowercase hex, which left two ways for a leak to pass: the
    // Summary line, and a Convert.ToHexString that was never lowercased. Both are cheap to
    // cover and neither would have been noticed by anything else.
    [Fact]
    public async Task Transcript_never_contains_the_key()
    {
        var result = await new TauriAgeEncryptor(new StubInterop(Good(ValidAgeFile())))
            .EncryptAsync(Encoding.UTF8.GetBytes("payload"), Key);

        var everythingShown = string.Join(
            "\n",
            result.Value.Transcript.Lines.Select(l => l.Text).Append(result.Value.Transcript.Summary));
        var hex = Convert.ToHexString(Key);

        Assert.DoesNotContain(hex.ToLowerInvariant(), everythingShown);
        Assert.DoesNotContain(hex.ToUpperInvariant(), everythingShown);
        Assert.DoesNotContain(Convert.ToBase64String(Key), everythingShown);
    }

    // Proves the test above is looking in the right place. Without it, a change that
    // stopped collecting the Summary, or collected the wrong field, would leave
    // Transcript_never_contains_the_key passing for the wrong reason: it asserts an
    // absence, and an absence is trivially true of a string nobody built.
    [Fact]
    public async Task The_leak_check_reads_the_whole_transcript()
    {
        var result = await new TauriAgeEncryptor(new StubInterop(Good(ValidAgeFile())))
            .EncryptAsync(Encoding.UTF8.GetBytes("payload"), Key);

        var everythingShown = string.Join(
            "\n",
            result.Value.Transcript.Lines.Select(l => l.Text).Append(result.Value.Transcript.Summary));

        Assert.Contains("age-encryption.org/v1", everythingShown);
        Assert.Contains("official age program", everythingShown);
    }
}
