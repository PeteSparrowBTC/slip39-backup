using FluentAssertions;
using Slip39Demo.Core.Bundle;
using Slip39Demo.Core.Slip39;
using Xunit;

namespace Slip39Demo.Tests.Bundle;

public class ReadmeTemplateTests
{
    [Fact]
    public void Build_SingleGroup_ContainsExpectedHeaders()
    {
        var cfg = new GroupConfig(1, [new ShareGroup("only", 3, 5)], true);

        var readme = ReadmeTemplate.Build(
            cfg: cfg,
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
    public void Build_MultiGroup_ListsEveryGroup()
    {
        var cfg = new GroupConfig(
            GroupThreshold: 2,
            Groups: [
                new ShareGroup("personal-1", 1, 1),
                new ShareGroup("friends", 3, 5),
                new ShareGroup("family", 2, 6)],
            Extendable: true);

        var readme = ReadmeTemplate.Build(cfg, "family", 3, 6, "2026-05-21", "2.0.0");

        readme.Should().Contain("personal-1: 1-of-1");
        readme.Should().Contain("friends: 3-of-5");
        readme.Should().Contain("family: 2-of-6");
        readme.Should().Contain("Group threshold: any 2 of 3 groups recover");
    }
}
