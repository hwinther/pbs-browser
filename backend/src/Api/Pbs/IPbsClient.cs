namespace PbsBrowser.Api.Pbs;

/// <summary>Thin wrapper over the <c>proxmox-backup-client</c> binary.</summary>
public interface IPbsClient
{
    Task<IReadOnlyList<SnapshotInfo>> ListSnapshotsAsync(CancellationToken ct);

    Task<IReadOnlyList<ArchiveInfo>> ListArchivesAsync(string snapshot, CancellationToken ct);

    Task<CatalogNode> GetCatalogAsync(string snapshot, CancellationToken ct);

    /// <summary>Restores a single file to a scratch dir and returns a stream over it, or null if the
    /// path was not present in the archive. The returned stream cleans the scratch dir on dispose.</summary>
    Task<RestoreResult?> RestoreFileAsync(string snapshot, string archive, string innerPath, CancellationToken ct);
}
