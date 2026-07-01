using Microsoft.Extensions.Caching.Memory;
using PbsBrowser.Api.Pbs;

var builder = WebApplication.CreateBuilder(args);

var pbsOptions = PbsOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(pbsOptions);
builder.Services.AddMemoryCache();

// Use the real client, or a sample in-memory fake for local dev without the binary. The fake is
// selected when PBS_FAKE is set explicitly, or automatically in Development when the binary is
// absent (e.g. developing on Windows — proxmox-backup-client-static is Linux/amd64 only).
var useFake = builder.Configuration["PBS_FAKE"] switch
{
    "1" or "true" or "True" => true,
    "0" or "false" or "False" => false,
    _ => builder.Environment.IsDevelopment() && !File.Exists(pbsOptions.ClientPath),
};
if (useFake)
    builder.Services.AddSingleton<IPbsClient, FakePbsClient>();
else
    builder.Services.AddSingleton<IPbsClient, PbsClient>();

var app = builder.Build();

if (useFake)
    app.Logger.LogWarning(
        "PBS client is in FAKE mode (sample data, no real PBS). Set PBS_FAKE=0 to force the real binary.");

// SPA static assets (built into wwwroot at image build).
app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

// Echoes the forward-auth identity headers Traefik/Authelia inject. Trusted only because the
// NetworkPolicy admits ingress solely from Traefik.
api.MapGet("/me", (HttpContext ctx) => Results.Ok(new
{
    user = ctx.Request.Headers["Remote-User"].FirstOrDefault() ?? string.Empty,
    email = ctx.Request.Headers["Remote-Email"].FirstOrDefault() ?? string.Empty,
}));

api.MapGet("/snapshots", async (IPbsClient pbs, CancellationToken ct) =>
    Results.Ok(await pbs.ListSnapshotsAsync(ct)));

api.MapGet("/snapshots/files", async (string snapshot, IPbsClient pbs, CancellationToken ct) =>
{
    if (!PbsInputValidation.IsValidSnapshot(snapshot))
        return Results.BadRequest(new { error = "invalid snapshot" });
    return Results.Ok(await pbs.ListArchivesAsync(snapshot, ct));
});

api.MapGet("/catalog", async (string snapshot, IPbsClient pbs, IMemoryCache cache, CancellationToken ct) =>
{
    if (!PbsInputValidation.IsValidSnapshot(snapshot))
        return Results.BadRequest(new { error = "invalid snapshot" });

    // The catalog is metadata-only and cheap to decrypt, but still a process spawn — cache briefly.
    var tree = await cache.GetOrCreateAsync($"catalog::{snapshot}", entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
        return pbs.GetCatalogAsync(snapshot, ct);
    });
    return Results.Ok(tree);
});

api.MapGet("/download", async (string snapshot, string archive, string path,
    HttpContext ctx, IPbsClient pbs, ILoggerFactory loggerFactory, CancellationToken ct) =>
{
    if (!PbsInputValidation.IsValidSnapshot(snapshot))
        return Results.BadRequest(new { error = "invalid snapshot" });
    if (!PbsInputValidation.IsValidArchive(archive))
        return Results.BadRequest(new { error = "invalid archive" });
    if (!PbsInputValidation.IsValidInnerPath(path))
        return Results.BadRequest(new { error = "invalid path" });

    var result = await pbs.RestoreFileAsync(snapshot, archive, path, ct);
    if (result is null)
        return Results.NotFound();

    var user = ctx.Request.Headers["Remote-User"].FirstOrDefault() ?? "anonymous";
    loggerFactory.CreateLogger("Audit").LogInformation(
        "restore user={User} snapshot={Snapshot} archive={Archive} path={Path} bytes={Bytes}",
        user, snapshot, archive, path, result.Length);

    return Results.File(result.Stream, "application/octet-stream", result.FileName);
});

// Diagnostic: raw `catalog dump` exit/stdout/stderr, to inspect what the client actually returns
// (e.g. when a snapshot's tree comes back empty). Enabled only with PBS_DEBUG set.
if (app.Configuration["PBS_DEBUG"] is "1" or "true" or "True")
{
    api.MapGet("/debug/catalog", async (string snapshot, IPbsClient pbs, CancellationToken ct) =>
    {
        if (!PbsInputValidation.IsValidSnapshot(snapshot))
            return Results.BadRequest(new { error = "invalid snapshot" });
        var r = await pbs.DumpCatalogRawAsync(snapshot, ct);
        return Results.Ok(new { exitCode = r.ExitCode, stdout = r.StdOut, stderr = r.StdErr });
    });
}

app.MapGet("/healthz", () => Results.Text("ok"));

// SPA fallback — any non-API route serves the app shell.
app.MapFallbackToFile("index.html");

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
