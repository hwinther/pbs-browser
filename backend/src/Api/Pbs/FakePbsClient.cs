using System.Text;

namespace PbsBrowser.Api.Pbs;

/// <summary>
/// Sample in-memory <see cref="IPbsClient"/> for local development on machines without the
/// <c>proxmox-backup-client</c> binary (e.g. Windows — the static binary is Linux/amd64 only).
/// Enabled via <c>PBS_FAKE=1</c>, or automatically in Development when the binary is absent. It is
/// never selected in Production. Lets you iterate on the UI/API without a binary or a live PBS.
/// </summary>
public sealed class FakePbsClient : IPbsClient
{
    private const string SampleCatalogDump =
        """
        d---         /
        d---         /etc
        f--- 158     /etc/hosts
        d---         /etc/grafana
        f--- 4096    /etc/grafana/grafana.ini
        d---         /var
        f--- 1048576 /var/lib/grafana/grafana.db
        """;

    public Task<IReadOnlyList<SnapshotInfo>> ListSnapshotsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SnapshotInfo>>(
        [
            new SnapshotInfo("host/grafana/2026-07-01T04:30:00Z", "host", "grafana",
                DateTimeOffset.Parse("2026-07-01T04:30:00Z"), 1_200_000, "daily"),
            new SnapshotInfo("host/node-red/2026-06-30T04:00:00Z", "host", "node-red",
                DateTimeOffset.Parse("2026-06-30T04:00:00Z"), 800_000, null),
            new SnapshotInfo("vm/100/2026-06-29T02:15:00Z", "vm", "100",
                DateTimeOffset.Parse("2026-06-29T02:15:00Z"), 42_000_000_000, null),
        ]);

    public Task<IReadOnlyList<ArchiveInfo>> ListArchivesAsync(string snapshot, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ArchiveInfo>>(
        [
            new ArchiveInfo("root.pxar.didx", 1_150_000, "encrypt"),
            new ArchiveInfo("catalog.pcat1.didx", 20_000, "encrypt"),
            new ArchiveInfo("index.json.blob", 512, "encrypt"),
        ]);

    public Task<CatalogNode> GetCatalogAsync(string snapshot, CancellationToken ct) =>
        Task.FromResult(CatalogParser.Parse(SampleCatalogDump));

    public Task<RestoreResult?> RestoreFileAsync(string snapshot, string archive, string innerPath, CancellationToken ct)
    {
        var body = $"[fake pbs-browser] restored {innerPath}\nfrom {archive} in {snapshot}\n";
        var bytes = Encoding.UTF8.GetBytes(body);
        var fileName = Path.GetFileName(innerPath.TrimEnd('/'));
        return Task.FromResult<RestoreResult?>(new RestoreResult(new MemoryStream(bytes), bytes.Length, fileName));
    }
}
