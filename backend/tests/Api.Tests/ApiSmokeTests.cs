using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PbsBrowser.Api.Pbs;
using Xunit;

namespace PbsBrowser.Api.Tests;

public class ApiSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPbsClient>();
                services.AddSingleton<IPbsClient, FakePbsClient>();
            }));
    }

    [Fact]
    public async Task Healthz_returns_ok()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/healthz");
        resp.EnsureSuccessStatusCode();
        Assert.Equal("ok", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Snapshots_returns_fake_list()
    {
        var client = _factory.CreateClient();
        var body = await client.GetStringAsync("/api/snapshots");
        Assert.Contains("host/demo/2026-06-30T00:00:00Z", body);
    }

    [Fact]
    public async Task Catalog_rejects_malformed_snapshot()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/catalog?snapshot=not-a-snapshot");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Catalog_accepts_valid_snapshot()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/catalog?snapshot=host/demo/2026-06-30T00:00:00Z");
        resp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Download_rejects_path_traversal()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync(
            "/api/download?snapshot=host/demo/2026-06-30T00:00:00Z&archive=root.pxar&path=/etc/../../shadow");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Download_streams_restored_file()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync(
            "/api/download?snapshot=host/demo/2026-06-30T00:00:00Z&archive=root.pxar&path=/etc/hosts");
        resp.EnsureSuccessStatusCode();
        Assert.Equal("127.0.0.1 localhost", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Me_echoes_forward_auth_headers()
    {
        var client = _factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        req.Headers.Add("Remote-User", "hcw");
        req.Headers.Add("Remote-Email", "hcw@wsh.no");
        var resp = await client.SendAsync(req);

        var json = await resp.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal("hcw", json!.User);
        Assert.Equal("hcw@wsh.no", json.Email);
    }

    private sealed record MeResponse(string User, string Email);

    private sealed class FakePbsClient : IPbsClient
    {
        public Task<IReadOnlyList<SnapshotInfo>> ListSnapshotsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SnapshotInfo>>(
            [
                new SnapshotInfo("host/demo/2026-06-30T00:00:00Z", "host", "demo",
                    DateTimeOffset.FromUnixTimeSeconds(1782777600), 123, null),
            ]);

        public Task<IReadOnlyList<ArchiveInfo>> ListArchivesAsync(string snapshot, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ArchiveInfo>>([new ArchiveInfo("root.pxar.didx", 999, "encrypt")]);

        public Task<CatalogNode> GetCatalogAsync(string snapshot, CancellationToken ct) =>
            Task.FromResult(new CatalogNode { Name = "/", Path = "/", IsDir = true });

        public Task<RestoreResult?> RestoreFileAsync(string snapshot, string archive, string innerPath, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes("127.0.0.1 localhost");
            return Task.FromResult<RestoreResult?>(new RestoreResult(new MemoryStream(bytes), bytes.Length, "hosts"));
        }
    }
}
