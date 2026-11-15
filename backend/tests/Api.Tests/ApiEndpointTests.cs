using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PbsBrowser.Api.Pbs;
using PbsBrowser.Api.Security;
using Xunit;

namespace PbsBrowser.Api.Tests;

/// <summary>The app as it starts with PBS_FAKE=1: the sample <see cref="FakePbsClient"/> the e2e stack uses.</summary>
public class ApiEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Snapshot = "host/grafana/2026-07-01T04:30:00Z";

    private readonly WebApplicationFactory<Program> _fake = factory.WithWebHostBuilder(b => b.UseSetting("PBS_FAKE", "1"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Fake_mode_serves_the_sample_snapshots()
    {
        var snapshots = await _fake.CreateClient().GetFromJsonAsync<SnapshotInfo[]>("/api/snapshots", Ct);

        Assert.Equal([Snapshot, "host/node-red/2026-06-30T04:00:00Z", "vm/100/2026-06-29T02:15:00Z"],
            snapshots!.Select(s => s.Id));
    }

    [Fact]
    public async Task Files_lists_the_archives_of_a_snapshot()
    {
        var archives = await _fake.CreateClient().GetFromJsonAsync<ArchiveInfo[]>(
            $"/api/snapshots/files?snapshot={Snapshot}", Ct);

        Assert.Contains(new ArchiveInfo("config.pxar.didx", 50_588_152, "encrypt"), archives!);
    }

    [Fact]
    public async Task Files_rejects_a_malformed_snapshot()
    {
        var resp = await _fake.CreateClient().GetAsync("/api/snapshots/files?snapshot=../etc", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Catalog_serves_each_archive_of_the_sample_dump()
    {
        var client = _fake.CreateClient();
        var config = await client.GetFromJsonAsync<CatalogNode>($"/api/catalog?snapshot={Snapshot}&archive=config.pxar", Ct);
        var data = await client.GetFromJsonAsync<CatalogNode>($"/api/catalog?snapshot={Snapshot}&archive=data.pxar.didx", Ct);

        Assert.Equal(["worlds_local", "adminlist.txt", "prefs"], config!.Children.Select(c => c.Name));
        Assert.Equal(["plugins", "grafana.db"], data!.Children.Select(c => c.Name));
    }

    [Fact]
    public async Task Download_serves_the_fake_restore_as_an_attachment()
    {
        var resp = await _fake.CreateClient().GetAsync(
            $"/api/download?snapshot={Snapshot}&archive=config.pxar&path=/prefs", Ct);

        resp.EnsureSuccessStatusCode();
        Assert.Equal("prefs", resp.Content.Headers.ContentDisposition!.FileName);
        Assert.StartsWith("[fake pbs-browser] restored /prefs", await resp.Content.ReadAsStringAsync(Ct));
    }

    [Theory]
    [InlineData("snapshot=nope&archive=config.pxar&path=/prefs")]
    [InlineData("snapshot=" + Snapshot + "&archive=config.txt&path=/prefs")]
    public async Task Download_rejects_a_malformed_snapshot_or_archive(string query)
    {
        var resp = await _fake.CreateClient().GetAsync($"/api/download?{query}", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Download_of_a_path_not_in_the_archive_is_404()
    {
        var client = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPbsClient>();
            services.AddSingleton<IPbsClient, NothingRestoredClient>();
        })).CreateClient();

        var resp = await client.GetAsync($"/api/download?snapshot={Snapshot}&archive=config.pxar&path=/gone", Ct);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task The_debug_endpoint_returns_the_raw_dump_when_enabled()
    {
        var client = _fake.WithWebHostBuilder(b => b.UseSetting("PBS_DEBUG", "1")).CreateClient();

        var raw = await client.GetFromJsonAsync<RawDump>($"/api/debug/catalog?snapshot={Snapshot}", Ct);
        var bad = await client.GetAsync("/api/debug/catalog?snapshot=nope", Ct);

        Assert.Equal(0, raw!.ExitCode);
        Assert.Empty(raw.Stdout);
        Assert.Contains("grafana.db", raw.Stderr);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public void The_real_client_is_used_when_fake_mode_is_off()
    {
        var factoryOff = _fake.WithWebHostBuilder(b => b.UseSetting("PBS_FAKE", "0"));
        using var scope = factoryOff.Services.CreateScope();

        Assert.IsType<PbsClient>(scope.ServiceProvider.GetRequiredService<IPbsClient>());
    }

    [Fact]
    public async Task Starts_with_an_otlp_endpoint_configured()
    {
        var client = _fake.WithWebHostBuilder(b => b.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", "http://127.0.0.1:1"))
            .CreateClient();

        var resp = await client.GetAsync("/api/snapshots", Ct);

        resp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task An_oversized_csp_report_is_dropped_with_204()
    {
        var body = new string(' ', CspReports.MaxReportBytes + 10);
        var resp = await _fake.CreateClient().PostAsync("/csp-report",
            new StringContent(body, Encoding.UTF8, "application/csp-report"), Ct);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    private sealed record RawDump(int ExitCode, string Stdout, string Stderr);

    private sealed class NothingRestoredClient : IPbsClient
    {
        private readonly FakePbsClient _inner = new();

        public Task<IReadOnlyList<SnapshotInfo>> ListSnapshotsAsync(CancellationToken ct) => _inner.ListSnapshotsAsync(ct);

        public Task<IReadOnlyList<ArchiveInfo>> ListArchivesAsync(string snapshot, CancellationToken ct) =>
            _inner.ListArchivesAsync(snapshot, ct);

        public Task<SnapshotCatalog> GetCatalogAsync(string snapshot, CancellationToken ct) => _inner.GetCatalogAsync(snapshot, ct);

        public Task<RestoreResult?> RestoreFileAsync(string snapshot, string archive, string innerPath, CancellationToken ct) =>
            Task.FromResult<RestoreResult?>(null);

        public Task<CommandResult> DumpCatalogRawAsync(string snapshot, CancellationToken ct) =>
            _inner.DumpCatalogRawAsync(snapshot, ct);
    }
}
