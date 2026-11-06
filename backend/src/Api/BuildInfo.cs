using System.Reflection;

namespace PbsBrowser.Api;

/// <summary>
/// The app version and commit, stamped in at publish: the image build passes the GitVersion
/// <c>APP_VERSION</c> as <c>InformationalVersion</c> and <c>GIT_SHA</c> as <c>SourceRevisionId</c>,
/// which the SDK appends as <c>+&lt;sha&gt;</c>. Locally the SDK fills the commit in from git.
/// </summary>
public sealed record BuildInfo(string Version, string? Commit)
{
    public static BuildInfo FromAssembly(Assembly assembly) =>
        Parse(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>Splits <c>1.2.3-pre.4+abc123</c> into the version and the commit.</summary>
    public static BuildInfo Parse(string? informationalVersion)
    {
        var value = informationalVersion?.Trim() ?? string.Empty;
        var plus = value.IndexOf('+');
        var version = (plus < 0 ? value : value[..plus]).Trim();
        var commit = plus < 0 ? null : value[(plus + 1)..].Trim();
        return new BuildInfo(
            version.Length == 0 ? "unknown" : version,
            string.IsNullOrEmpty(commit) ? null : commit);
    }
}
