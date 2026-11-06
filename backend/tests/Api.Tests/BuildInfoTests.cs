using Xunit;

namespace PbsBrowser.Api.Tests;

public class BuildInfoTests
{
    [Theory]
    [InlineData("0.5.2+896ef54a1c", "0.5.2", "896ef54a1c")]
    [InlineData("0.5.2-feat-app-version.3+896ef54", "0.5.2-feat-app-version.3", "896ef54")]
    [InlineData("0.5.2", "0.5.2", null)]
    [InlineData("0.5.2+", "0.5.2", null)]
    public void Parse_splits_version_and_commit(string informational, string version, string? commit)
    {
        Assert.Equal(new BuildInfo(version, commit), BuildInfo.Parse(informational));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_falls_back_to_unknown(string? informational)
    {
        Assert.Equal(new BuildInfo("unknown", null), BuildInfo.Parse(informational));
    }
}
