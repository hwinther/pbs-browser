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

/// <summary>A restored file ready to stream. Disposing <see cref="Stream"/> cleans the scratch dir.</summary>
public sealed record RestoreResult(Stream Stream, long Length, string FileName);

/// <summary>Raised when the <c>proxmox-backup-client</c> process exits non-zero.</summary>
public sealed class PbsException(string message) : Exception(message);
