using FluentAssertions;
using Slip39Demo.Core.Bundle;
using Xunit;

namespace Slip39Demo.Tests.Bundle;

public class ReadmeTemplateTests
{
    [Fact]
    public void Build_ContainsRecovererFacingContent()
    {
        var readme = ReadmeTemplate.Build(
            groupName: "only",
            shareIndex: 2,
            shareCountInGroup: 5,
            createdDate: "2026-05-21",
            toolVersion: "2.0.0");

        readme.Should().Contain("SLIP-39 SHARE BACKUP");
        readme.Should().Contain("share 2 of 5");
        readme.Should().Contain("Created: 2026-05-21");
        readme.Should().Contain("Tool: Seed-Phrase-Storage-SLIP39 v2.0.0");
        readme.Should().Contain("RECOVERY PROCEDURE");
        readme.Should().Contain("ALTERNATIVE TOOLS");
        readme.Should().Contain("https://github.com/satoshilabs/slips/blob/master/slip-0039.md");
        readme.Should().Contain("https://age-encryption.org/v1");
    }

    [Fact]
    public void Build_TellsHolderTheyNeedDoNothing()
    {
        var readme = ReadmeTemplate.Build("only", 1, 5, "2026-05-21", "2.0.0");

        readme.Should().Contain("only HOLDING");
        readme.Should().Contain("store it safely");
    }

    [Fact]
    public void Build_DoesNotLeakThresholdStructure_NorCallShareUseless()
    {
        // A share must not reveal the scheme to its holder: no threshold / group
        // breakdown (redundant — SLIP-39 encodes the required count in the mnemonic),
        // and it must not tell the holder the share is "useless" alone (invites
        // carelessness). Guards the deliberate omissions.
        var readme = ReadmeTemplate.Build("family", 3, 6, "2026-05-21", "2.0.0");

        readme.Should().NotContain("Group threshold");
        readme.Should().NotContain("groups recover");
        readme.Should().NotContain("3-of-5");
        readme.Should().NotContainEquivalentOf("reveals nothing");
        readme.Should().NotContainEquivalentOf("cryptographically useless");
    }
}
