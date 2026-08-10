using System.Text;
using System.Text.Json;
using Slip39Demo.Tauri.Services;
using Xunit;

namespace Slip39Demo.Tests.Tauri;

public class TauriFileDownloaderTests
{
    sealed class RecordingInterop : ITauriInterop
    {
        public string? Command { get; private set; }
        public string? Json { get; private set; }

        public ValueTask<T> InvokeAsync<T>(string command, object? args = null)
        {
            Command = command;
            Json = JsonSerializer.Serialize(args);
            return ValueTask.FromResult(default(T)!);
        }
    }

    [Fact]
    public async Task Sends_the_name_and_the_bytes_base64_encoded()
    {
        var interop = new RecordingInterop();
        var bytes = Encoding.UTF8.GetBytes("backup contents");

        await new TauriFileDownloader(interop).DownloadAsync("backup.zip", bytes, "application/zip");

        Assert.Equal("save_file", interop.Command);
        var sent = JsonDocument.Parse(interop.Json!).RootElement;
        Assert.Equal("backup.zip", sent.GetProperty("suggestedName").GetString());
        Assert.Equal(Convert.ToBase64String(bytes), sent.GetProperty("bytesB64").GetString());
    }

    // The cancel path. save_file returns Ok(None) when the user dismisses the dialog,
    // which crosses the interop boundary as a null string. DownloadAsync must treat
    // that as a completed call, not throw: the caller asked to save and the user said
    // no, which is not a failure the frontend needs to report.
    //
    // What this does and does not prove, because a review pointed out the first draft of
    // this comment claimed more than the test delivers. DownloadAsync discards the returned
    // path, so there is no null branch to regress and the test cannot fail today whatever
    // the null handling is. It is a guard against a future edit, not a demonstration that
    // the current code got something right: if someone later reads the result, to show the
    // saved path in the UI or to decide the save succeeded, this fails the moment they
    // dereference it without checking. That is worth keeping and is not worth overstating.
    // The cancel path as a whole is not covered by any test that runs a dialog, because no
    // dialog can be opened on the machine this was built on.
    sealed class NullReturningInterop : ITauriInterop
    {
        public ValueTask<T> InvokeAsync<T>(string command, object? args = null) =>
            ValueTask.FromResult(default(T)!);
    }

    [Fact]
    public async Task Tolerates_a_null_result_from_a_cancelled_dialog()
    {
        var exception = await Record.ExceptionAsync(() =>
            new TauriFileDownloader(new NullReturningInterop())
                .DownloadAsync("backup.zip", [1, 2, 3], "application/zip")
                .AsTask());

        Assert.Null(exception);
    }

    // The real payload is a zip, whose bytes are not valid UTF-8 text. Base64 has to
    // carry them exactly: any code on this path that treats the buffer as text (a
    // String::from_utf8_lossy mistake, for instance) would corrupt the backup, and a
    // corrupted backup is not the kind of thing you find out about until you need it.
    [Fact]
    public async Task Base64_round_trips_bytes_that_are_not_valid_utf8()
    {
        var interop = new RecordingInterop();
        byte[] bytes = [0x50, 0x4B, 0x03, 0x04, 0xFF, 0xFE, 0x00, 0x80, 0x81];

        await new TauriFileDownloader(interop).DownloadAsync("backup.zip", bytes, "application/zip");

        var sent = JsonDocument.Parse(interop.Json!).RootElement;
        var roundTripped = Convert.FromBase64String(sent.GetProperty("bytesB64").GetString()!);
        Assert.Equal(bytes, roundTripped);
    }
}
