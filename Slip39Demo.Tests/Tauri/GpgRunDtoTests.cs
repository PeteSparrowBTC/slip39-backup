using System.Text.Json;
using Slip39Demo.Tauri.Services;
using Xunit;

namespace Slip39Demo.Tests.Tauri;

// Pins the agreement between src-tauri/src/gpg.rs (GpgRun derives Serialize with
// serde(rename_all = "camelCase")) and the DTO that reads it. Nothing else in the suite
// deserializes real shell output, so nothing else would notice a renamed field.
public class GpgRunDtoTests
{
    // Copied from what the shell produces on a successful decrypt.
    const string FromTheShell = """
    {
      "exitCode": 0,
      "stdoutB64": "YWdlLWVuY3J5cHRpb24ub3JnL3Yx",
      "stderrText": "gpg: AES256.CFB encrypted data",
      "version": "gpg (GnuPG) 2.2.40",
      "gpgMissing": false
    }
    """;

    [Fact]
    public void Deserializes_every_field_the_shell_sends()
    {
        var run = JsonSerializer.Deserialize<GpgRunDto>(FromTheShell)!;

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("YWdlLWVuY3J5cHRpb24ub3JnL3Yx", run.StdoutB64);
        Assert.Equal("gpg: AES256.CFB encrypted data", run.StderrText);
        Assert.Equal("gpg (GnuPG) 2.2.40", run.Version);
        Assert.False(run.GpgMissing);
    }

    // Why the test above is not decorative. A name that does not match arrives as a
    // default, and a default GpgRunDto reads as exit code 0 with no output: gpg succeeded
    // and returned nothing. TauriPgpVerifier catches that particular shape because empty
    // output cannot equal the age file, but the defaults are worth stating so the next
    // field added to the shell is added on both sides.
    [Fact]
    public void A_name_that_does_not_match_reads_as_a_successful_empty_run()
    {
        var run = JsonSerializer.Deserialize<GpgRunDto>("""{"gpg_missing": true}""")!;

        Assert.False(run.GpgMissing);
        Assert.Equal(0, run.ExitCode);
        Assert.Equal("", run.StdoutB64);
    }

    [Fact]
    public void Reports_a_missing_gpg_the_way_the_shell_spells_it()
    {
        var run = JsonSerializer.Deserialize<GpgRunDto>(
            """{"exitCode": -1, "gpgMissing": true, "stderrText": "gpg could not be run."}""")!;

        Assert.True(run.GpgMissing);
        Assert.Equal(-1, run.ExitCode);
        Assert.Equal("gpg could not be run.", run.StderrText);
    }
}
