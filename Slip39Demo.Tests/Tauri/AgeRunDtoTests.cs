using System.Text.Json;
using Slip39Demo.Tauri.Services;
using Xunit;

namespace Slip39Demo.Tests.Tauri;

public class AgeRunDtoTests
{
    // Copied from what src-tauri/src/age.rs produces: AgeRun derives Serialize with
    // serde(rename_all = "camelCase").
    const string FromTheShell = """
    {
      "exitCode": 0,
      "stdoutB64": "YWdl",
      "stdoutText": "v1.3.1",
      "stderrText": "",
      "agePath": "/tmp/.mount_x/usr/bin/age/age",
      "ageSha256": "bdc69c09cbdd6cf8b1f333d372a1f58247b3a33146406333e30c0f26e8f51377",
      "pluginPath": "/tmp/.mount_x/usr/bin/age/age-plugin-batchpass",
      "ageMissing": false,
      "pluginMissing": false
    }
    """;

    [Fact]
    public void Deserializes_every_field_the_shell_sends()
    {
        var run = JsonSerializer.Deserialize<AgeRunDto>(FromTheShell)!;

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("YWdl", run.StdoutB64);
        Assert.Equal("v1.3.1", run.StdoutText);
        Assert.Equal("", run.StderrText);
        Assert.Equal("/tmp/.mount_x/usr/bin/age/age", run.AgePath);
        Assert.Equal(
            "bdc69c09cbdd6cf8b1f333d372a1f58247b3a33146406333e30c0f26e8f51377", run.AgeSha256);
        Assert.Equal("/tmp/.mount_x/usr/bin/age/age-plugin-batchpass", run.PluginPath);
        Assert.False(run.AgeMissing);
        Assert.False(run.PluginMissing);
    }

    // The failure this whole file exists to catch: a name that does not match arrives as
    // a default, and a default AgeRunDto reads as a successful run that produced
    // nothing. Proving the defaults are what they are is what makes the test above
    // meaningful rather than decorative.
    [Fact]
    public void A_name_that_does_not_match_would_read_as_a_successful_empty_run()
    {
        var run = JsonSerializer.Deserialize<AgeRunDto>("""{"age_missing": true}""")!;

        Assert.False(run.AgeMissing);
        Assert.Equal(0, run.ExitCode);
        Assert.Equal("", run.StdoutB64);
    }

    [Fact]
    public void Reports_a_missing_bundle_the_way_the_shell_spells_it()
    {
        var run = JsonSerializer.Deserialize<AgeRunDto>(
            """{"exitCode": -1, "ageMissing": true, "pluginMissing": true}""")!;

        Assert.True(run.AgeMissing);
        Assert.True(run.PluginMissing);
        Assert.Equal(-1, run.ExitCode);
    }
}
