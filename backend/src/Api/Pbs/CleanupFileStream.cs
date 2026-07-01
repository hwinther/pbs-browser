namespace PbsBrowser.Api.Pbs;

/// <summary>
/// A read-only <see cref="FileStream"/> that deletes a scratch directory once the stream is disposed
/// — i.e. after the HTTP response has finished streaming the restored file.
/// </summary>
internal sealed class CleanupFileStream : FileStream
{
    private readonly string _scratchDir;

    public CleanupFileStream(string path, string scratchDir)
        : base(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true)
    {
        _scratchDir = scratchDir;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        TryCleanup();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        TryCleanup();
    }

    private void TryCleanup()
    {
        try
        {
            if (Directory.Exists(_scratchDir))
                Directory.Delete(_scratchDir, recursive: true);
        }
        catch
        {
            // best-effort; a leftover temp dir is not fatal
        }
    }
}
