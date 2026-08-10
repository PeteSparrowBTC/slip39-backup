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
    // that as a normal outcome, not throw, and it must now say so in its return value:
    // false, meaning nothing was saved, so the caller (the Owner page) knows not to
    // claim a backup was created.
    //
    // This used to be a test that could not fail: DownloadAsync discarded the returned
    // path, so there was no null branch to regress whatever the handling was. That is
    // exactly why the return value exists now. This asserts the real value instead of
    // only the absence of an exception.
    sealed class NullReturningInterop : ITauriInterop
    {
        public ValueTask<T> InvokeAsync<T>(string command, object? args = null) =>
            ValueTask.FromResult(default(T)!);
    }

    [Fact]
    public async Task A_null_result_from_a_cancelled_dialog_is_reported_as_not_saved()
    {
        var saved = await new TauriFileDownloader(new NullReturningInterop())
            .DownloadAsync("backup.zip", [1, 2, 3], "application/zip");

        Assert.False(saved);
    }

    // A path came back, so save_file claims the bytes are on disk. This is the half
    // of the contract the interface doc calls out explicitly: read the file to confirm
    // rather than trusting the string. When the path really does point at a file, that
    // check must pass and the result must be true.
    sealed class PathReturningInterop(string? path) : ITauriInterop
    {
        public ValueTask<T> InvokeAsync<T>(string command, object? args = null) =>
            ValueTask.FromResult((T)(object?)path!);
    }

    [Fact]
    public async Task Reports_saved_when_the_returned_path_is_a_real_file()
    {
        var file = Path.Combine(Path.GetTempPath(), $"slip39-tauri-downloader-{Guid.NewGuid()}.zip");
        File.WriteAllBytes(file, [1, 2, 3]);
        try
        {
            var saved = await new TauriFileDownloader(new PathReturningInterop(file))
                .DownloadAsync("backup.zip", [1, 2, 3], "application/zip");

            Assert.True(saved);
        }
        finally
        {
            File.Delete(file);
        }
    }

    // The other half of "read to confirm": a non-null path is not enough on its own.
    // If save_file's contract were ever broken and returned a path to something that
    // was never actually written, this must still report false rather than pass an
    // unchecked claim through to the UI.
    [Fact]
    public async Task Reports_not_saved_when_the_returned_path_does_not_exist()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"slip39-does-not-exist-{Guid.NewGuid()}.zip");

        var saved = await new TauriFileDownloader(new PathReturningInterop(missing))
            .DownloadAsync("backup.zip", [1, 2, 3], "application/zip");

        Assert.False(saved);
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
