namespace PbsBrowser.Api.Pbs;

/// <summary>
/// Configuration for talking to PBS. Sensitive values (PBS_PASSWORD, PBS_ENCRYPTION_PASSWORD) are
/// NOT held here — they are read from the process environment by the client binary directly, so they
/// never pass through application code or logs.
/// </summary>
public sealed class PbsOptions
{
    /// <summary>e.g. <c>user@pbs!token@host:8007:datastore</c>. Also available to the binary via env.</summary>
    public string Repository { get; init; } = string.Empty;

    /// <summary>Server cert fingerprint, for self-signed PBS. Optional.</summary>
    public string? Fingerprint { get; init; }

    /// <summary>PBS datastore namespace, e.g. <c>Production/k0s</c>. Passed as <c>--ns</c>. Optional.</summary>
    public string? Namespace { get; init; }

    /// <summary>Path to the client-side encryption keyfile (mounted from a Secret).</summary>
    public string KeyfilePath { get; init; } = "/etc/pbs/keyfile";

    /// <summary>Path to the <c>proxmox-backup-client</c> binary inside the image.</summary>
    public string ClientPath { get; init; } = "/usr/bin/proxmox-backup-client";

    /// <summary>True when the encryption keyfile is present and should be passed to decrypt commands.</summary>
    public bool HasKeyfile => File.Exists(KeyfilePath);

    public static PbsOptions FromConfiguration(IConfiguration config) => new()
    {
        Repository = config["PBS_REPOSITORY"] ?? string.Empty,
        Fingerprint = NullIfBlank(config["PBS_FINGERPRINT"]),
        Namespace = NullIfBlank(config["PBS_NAMESPACE"]),
        KeyfilePath = NullIfBlank(config["PBS_KEYFILE"]) ?? "/etc/pbs/keyfile",
        ClientPath = NullIfBlank(config["PBS_CLIENT_PATH"]) ?? "/usr/bin/proxmox-backup-client",
    };

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
