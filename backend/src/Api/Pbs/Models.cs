namespace PbsBrowser.Api.Pbs;

/// <summary>A backup snapshot. <see cref="Id"/> is the <c>type/id/time</c> specifier the client accepts.</summary>
public sealed record SnapshotInfo(
    string Id,
    string BackupType,
    string BackupId,
    DateTimeOffset Time,
    long? Size,
    string? Comment);

/// <summary>A file/archive inside a snapshot (e.g. <c>root.pxar.didx</c>, <c>index.json.blob</c>).</summary>
public sealed record ArchiveInfo(string Filename, long? Size, string? CryptMode);

/// <summary>A node in the catalog file tree.</summary>
public sealed class CatalogNode
{
    public required string Name { get; set; }
    public required string Path { get; set; }
    public bool IsDir { get; set; }
    public long? Size { get; set; }
    public List<CatalogNode> Children { get; set; } = [];
}

/// <summary>
/// A snapshot's catalog: one file tree per pxar archive, keyed by the archive name <c>restore</c>
/// takes (<c>config.pxar</c>, without the <c>.didx</c>/<c>.fidx</c> index suffix).
/// </summary>
public sealed record SnapshotCatalog(IReadOnlyDictionary<string, CatalogNode> Archives)
{
    /// <summary>The tree of one archive; empty when the catalog has no entries for it.</summary>
    public CatalogNode For(string archive) =>
        Archives.TryGetValue(ArchiveKey(archive), out var root) ? root : EmptyRoot();

    /// <summary><c>config.pxar.didx</c> and <c>config.pxar</c> both name <c>config.pxar</c>.</summary>
    public static string ArchiveKey(string archive) =>
        archive.EndsWith(".didx", StringComparison.Ordinal) || archive.EndsWith(".fidx", StringComparison.Ordinal)
            ? archive[..^5]
            : archive;

    public static CatalogNode EmptyRoot() => new() { Name = "/", Path = "/", IsDir = true };
}

/// <summary>A restored file ready to stream. Disposing <see cref="Stream"/> cleans the scratch dir.</summary>
public sealed record RestoreResult(Stream Stream, long Length, string FileName);

/// <summary>Raw result of a client invocation — used by the diagnostic endpoint.</summary>
public sealed record CommandResult(int ExitCode, string StdOut, string StdErr);

/// <summary>Raised when the <c>proxmox-backup-client</c> process exits non-zero.</summary>
public sealed class PbsException(string message) : Exception(message);
