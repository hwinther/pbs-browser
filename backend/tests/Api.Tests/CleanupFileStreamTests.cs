using PbsBrowser.Api.Pbs;
using Xunit;

namespace PbsBrowser.Api.Tests;

public sealed class CleanupFileStreamTests
{
    private static string Scratch()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pbsr-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "file"), "bytes");
        return dir;
    }

    [Fact]
    public void Dispose_deletes_the_scratch_dir()
    {
        var dir = Scratch();
        var stream = new CleanupFileStream(Path.Combine(dir, "file"), dir);
        Assert.Equal(5, stream.Length);

        stream.Dispose();

        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public async Task DisposeAsync_deletes_the_scratch_dir()
    {
        var dir = Scratch();
        var stream = new CleanupFileStream(Path.Combine(dir, "file"), dir);

        await stream.DisposeAsync();

        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public void A_scratch_dir_already_gone_is_not_an_error()
    {
        var dir = Scratch();
        var file = Path.Combine(dir, "file");
        var elsewhere = Path.Combine(Path.GetTempPath(), "pbsr-gone-" + Guid.NewGuid().ToString("N"));

        new CleanupFileStream(file, elsewhere).Dispose();

        Assert.True(File.Exists(file));
        Directory.Delete(dir, recursive: true);
    }
}
