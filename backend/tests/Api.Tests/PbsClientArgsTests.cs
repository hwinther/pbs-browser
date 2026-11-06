using PbsBrowser.Api.Pbs;
using Xunit;

namespace PbsBrowser.Api.Tests;

public class PbsClientArgsTests
{
    private static readonly List<string> Args =
        PbsClient.RestoreArgs("host/demo/2026-06-30T00:00:00Z", "config.pxar", "/tmp/pbsr-x", "/a b (c).txt");

    [Fact]
    public void RestoreArgs_pass_user_values_as_separate_arguments()
    {
        Assert.Equal(
            ["restore", "host/demo/2026-06-30T00:00:00Z", "config.pxar", "/tmp/pbsr-x", "--pattern", "/a b (c).txt"],
            Args.Take(6));
    }

    // As a non-root user the client fails on "failed to set ownership: Operation not permitted"
    // unless ownership (and the other metadata) is skipped; only the file's bytes are streamed.
    [Theory]
    [InlineData("--ignore-ownership")]
    [InlineData("--ignore-permissions")]
    [InlineData("--ignore-acls")]
    [InlineData("--ignore-xattrs")]
    [InlineData("--allow-existing-dirs")]
    public void RestoreArgs_skip_metadata_a_non_root_user_cannot_restore(string flag)
    {
        var i = Args.IndexOf(flag);
        Assert.True(i > 0, $"{flag} missing");
        Assert.Equal("true", Args[i + 1]);
    }
}
