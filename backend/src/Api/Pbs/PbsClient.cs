using System.Diagnostics;

namespace PbsBrowser.Api.Pbs;

/// <summary>
/// Invokes <c>proxmox-backup-client</c> via an argument array — never a shell string — so user input
/// cannot inject. Credentials and the keyfile passphrase are read by the binary from the inherited
/// process environment (PBS_PASSWORD / PBS_ENCRYPTION_PASSWORD), never from application code.
/// </summary>
public sealed class PbsClient(PbsOptions options, ILogger<PbsClient> logger) : IPbsClient
{
    public async Task<IReadOnlyList<SnapshotInfo>> ListSnapshotsAsync(CancellationToken ct)
    {
        var args = new List<string> { "snapshot", "list", "--output-format", "json" };
        AddNamespace(args);
        var (exit, stdout, stderr) = await RunAsync(args, ct);
        if (exit != 0) throw new PbsException(Clip(stderr));
        return SnapshotParser.Parse(stdout);
    }

    public async Task<IReadOnlyList<ArchiveInfo>> ListArchivesAsync(string snapshot, CancellationToken ct)
    {
        var args = new List<string> { "snapshot", "files", snapshot, "--output-format", "json" };
        AddNamespace(args);
        var (exit, stdout, stderr) = await RunAsync(args, ct);
        if (exit != 0) throw new PbsException(Clip(stderr));
        return SnapshotParser.ParseFiles(stdout);
    }

    public async Task<CatalogNode> GetCatalogAsync(string snapshot, CancellationToken ct)
    {
        var args = new List<string> { "catalog", "dump", snapshot };
        AddKeyfile(args);
        AddNamespace(args);
        var (exit, stdout, stderr) = await RunAsync(args, ct);
        if (exit != 0) throw new PbsException(Clip(stderr));
        return CatalogParser.Parse(stdout);
    }

    public async Task<RestoreResult?> RestoreFileAsync(string snapshot, string archive, string innerPath, CancellationToken ct)
    {
        var scratch = Path.Combine(Path.GetTempPath(), "pbsr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            var args = new List<string> { "restore", snapshot, archive, scratch, "--pattern", innerPath };
            AddKeyfile(args);
            AddNamespace(args);
            var (exit, _, stderr) = await RunAsync(args, ct);
            if (exit != 0) throw new PbsException(Clip(stderr));

            var relative = innerPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var restored = Path.Combine(scratch, relative);
            if (!File.Exists(restored))
            {
                TryDelete(scratch);
                return null;
            }

            var length = new FileInfo(restored).Length;
            var fileName = Path.GetFileName(innerPath.TrimEnd('/'));
            var stream = new CleanupFileStream(restored, scratch);
            return new RestoreResult(stream, length, fileName);
        }
        catch
        {
            TryDelete(scratch);
            throw;
        }
    }

    private void AddKeyfile(List<string> args)
    {
        if (options.HasKeyfile)
        {
            args.Add("--keyfile");
            args.Add(options.KeyfilePath);
        }
    }

    private void AddNamespace(List<string> args)
    {
        if (!string.IsNullOrEmpty(options.Namespace))
        {
            args.Add("--ns");
            args.Add(options.Namespace);
        }
    }

    private async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo.FileName = options.ClientPath;
        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;

        // Repository/fingerprint are set explicitly in case they came from a non-env config source;
        // PBS_PASSWORD / PBS_ENCRYPTION_PASSWORD are left to inherit from the pod environment.
        if (!string.IsNullOrEmpty(options.Repository))
            process.StartInfo.Environment["PBS_REPOSITORY"] = options.Repository;
        if (!string.IsNullOrEmpty(options.Fingerprint))
            process.StartInfo.Environment["PBS_FINGERPRINT"] = options.Fingerprint;

        logger.LogDebug("exec {Client} {Args}", options.ClientPath, string.Join(' ', args));

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

    private static string Clip(string s) => s.Length > 2000 ? s[..2000] : s;

    private static void TryDelete(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }
}
