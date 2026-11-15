using Microsoft.Extensions.Logging.Abstractions;
using PbsBrowser.Api.Pbs;
using Xunit;

namespace PbsBrowser.Api.Tests;

/// <summary>
/// Runs <see cref="PbsClient"/> against a shell script standing in for <c>proxmox-backup-client</c>.
/// The script records its arguments and environment, answers like the real client, and fails when a
/// <c>fail</c> file sits next to it. Linux only (CI); the real client is Linux-only as well.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
public sealed class PbsClientProcessTests : IDisposable
{
    private const string Snapshot = "host/demo/2026-06-30T00:00:00Z";

    private const string Script =
        """
        #!/bin/sh
        dir=$(dirname "$0")
        printf '%s\n' "$@" > "$dir/args"
        printf '%s|%s\n' "$PBS_REPOSITORY" "$PBS_FINGERPRINT" > "$dir/env"
        if [ -f "$dir/fail" ]; then
          head -c 3000 /dev/zero | tr '\0' x >&2
          exit 3
        fi
        case "$1:$2" in
          snapshot:list)
            echo '[{"backup-type":"host","backup-id":"demo","backup-time":1782777600,"size":5,"comment":"daily"}]' ;;
          snapshot:files)
            echo '[{"filename":"root.pxar.didx","size":9,"crypt-mode":"encrypt"}]' ;;
          catalog:dump)
            echo 'd "./root.pxar.didx"' >&2
            echo 'f "./root.pxar.didx/etc/hosts" 19 2026-06-30T00:00:00Z' >&2 ;;
          restore:*)
            if [ "$6" != "/missing" ]; then
              mkdir -p "$4$(dirname "$6")"
              printf 'restored %s' "$6" > "$4$6"
            fi ;;
        esac
        """;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pbs-fake-" + Guid.NewGuid().ToString("N"));
    private readonly string _keyfile;

    public PbsClientProcessTests()
    {
        if (!OperatingSystem.IsLinux())
            Assert.Skip("the fake client is a shell script");
        Directory.CreateDirectory(_dir);
        var client = Path.Combine(_dir, "proxmox-backup-client");
        File.WriteAllText(client, Script.ReplaceLineEndings("\n") + "\n");
        File.SetUnixFileMode(client, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        _keyfile = Path.Combine(_dir, "keyfile");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private PbsClient Client(string? ns = "Production/k0s", string? fingerprint = "aa:bb") =>
        new(new PbsOptions
        {
            Repository = "user@pbs!token@pbs.test:8007:store",
            Fingerprint = fingerprint,
            Namespace = ns,
            KeyfilePath = _keyfile,
            ClientPath = Path.Combine(_dir, "proxmox-backup-client"),
        }, NullLogger<PbsClient>.Instance);

    private string[] Args() => File.ReadAllLines(Path.Combine(_dir, "args"));

    private string Env() => File.ReadAllText(Path.Combine(_dir, "env")).Trim();

    [Fact]
    public async Task ListSnapshots_passes_namespace_and_repository_and_parses_the_json()
    {
        var snapshots = await Client().ListSnapshotsAsync(Ct);

        var snapshot = Assert.Single(snapshots);
        Assert.Equal(Snapshot, snapshot.Id);
        Assert.Equal("daily", snapshot.Comment);
        Assert.Equal(["snapshot", "list", "--output-format", "json", "--ns", "Production/k0s"], Args());
        Assert.Equal("user@pbs!token@pbs.test:8007:store|aa:bb", Env());
    }

    [Fact]
    public async Task ListArchives_leaves_out_an_unset_namespace_and_fingerprint()
    {
        var archives = await Client(ns: null, fingerprint: null).ListArchivesAsync(Snapshot, Ct);

        Assert.Equal(new ArchiveInfo("root.pxar.didx", 9, "encrypt"), Assert.Single(archives));
        Assert.Equal(["snapshot", "files", Snapshot, "--output-format", "json"], Args());
        Assert.Equal("user@pbs!token@pbs.test:8007:store|", Env());
    }

    [Fact]
    public async Task GetCatalog_reads_the_listing_from_stderr_and_passes_the_keyfile()
    {
        await File.WriteAllTextAsync(_keyfile, "key", Ct);

        var catalog = await Client().GetCatalogAsync(Snapshot, Ct);

        Assert.Equal("hosts", Assert.Single(Assert.Single(catalog.For("root.pxar").Children).Children).Name);
        Assert.Equal(["catalog", "dump", Snapshot, "--keyfile", _keyfile, "--ns", "Production/k0s"], Args());
    }

    [Fact]
    public async Task DumpCatalogRaw_returns_the_streams_as_they_came()
    {
        var result = await Client().DumpCatalogRawAsync(Snapshot, Ct);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StdOut);
        Assert.Contains("root.pxar.didx/etc/hosts", result.StdErr);
        Assert.DoesNotContain("--keyfile", Args());
    }

    [Fact]
    public async Task A_failing_client_raises_its_stderr_clipped()
    {
        await File.WriteAllTextAsync(Path.Combine(_dir, "fail"), string.Empty, Ct);
        var client = Client();

        var ex = await Assert.ThrowsAsync<PbsException>(() => client.ListSnapshotsAsync(Ct));
        Assert.Equal(new string('x', 2000), ex.Message);
        await Assert.ThrowsAsync<PbsException>(() => client.ListArchivesAsync(Snapshot, Ct));
        await Assert.ThrowsAsync<PbsException>(() => client.GetCatalogAsync(Snapshot, Ct));
    }

    [Fact]
    public async Task Restore_streams_the_file_and_cleans_up_on_dispose()
    {
        var result = await Client().RestoreFileAsync(Snapshot, "root.pxar", "/etc/hosts", Ct);

        Assert.NotNull(result);
        var scratch = Args()[3];
        Assert.Equal("hosts", result.FileName);
        Assert.Equal("restored /etc/hosts".Length, result.Length);
        using (var reader = new StreamReader(result.Stream))
            Assert.Equal("restored /etc/hosts", await reader.ReadToEndAsync(Ct));
        Assert.False(Directory.Exists(scratch));
    }

    [Fact]
    public async Task Restore_of_a_path_not_in_the_archive_is_null_and_leaves_nothing_behind()
    {
        var result = await Client().RestoreFileAsync(Snapshot, "root.pxar", "/missing", Ct);

        Assert.Null(result);
        Assert.False(Directory.Exists(Args()[3]));
    }

    [Fact]
    public async Task A_failed_restore_throws_and_leaves_nothing_behind()
    {
        var before = Directory.GetDirectories(Path.GetTempPath(), "pbsr-*").ToHashSet();
        await File.WriteAllTextAsync(Path.Combine(_dir, "fail"), string.Empty, Ct);

        await Assert.ThrowsAsync<PbsException>(() => Client().RestoreFileAsync(Snapshot, "root.pxar", "/etc/hosts", Ct));

        Assert.False(Directory.Exists(Args()[3]));
        Assert.Subset(before, Directory.GetDirectories(Path.GetTempPath(), "pbsr-*").ToHashSet());
    }
}
