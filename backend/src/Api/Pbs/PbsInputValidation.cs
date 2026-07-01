using System.Text.RegularExpressions;

namespace PbsBrowser.Api.Pbs;

/// <summary>
/// Allow-list validation for every user-supplied value that reaches the client binary. This is the
/// security boundary: combined with arg-array exec (no shell) it prevents both shell injection and
/// path/pattern abuse. Keep this covered by tests.
/// </summary>
public static partial class PbsInputValidation
{
    // <type>/<id>/<rfc3339-utc>, e.g. host/grafana/2026-06-30T01:02:03Z
    [GeneratedRegex(@"^(host|vm|ct)/[A-Za-z0-9_.\-]{1,128}/\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$")]
    private static partial Regex SnapshotRegex();

    // Archive file name inside a snapshot, e.g. db.pxar, root.pxar.didx, drive-scsi0.img
    [GeneratedRegex(@"^[A-Za-z0-9_.\-]{1,128}\.(pxar|mpxar|ppxar|img|conf|log|blob|fidx|didx)$")]
    private static partial Regex ArchiveRegex();

    public static bool IsValidSnapshot(string? value) =>
        value is not null
        && !value.Contains("..", StringComparison.Ordinal) // defence-in-depth: no traversal in the id
        && SnapshotRegex().IsMatch(value);

    public static bool IsValidArchive(string? value) =>
        value is not null && ArchiveRegex().IsMatch(value);

    /// <summary>
    /// Absolute path inside the archive. Rejected: relative paths, traversal (<c>..</c>), NUL/control
    /// chars, non-ASCII, and glob/escape metacharacters — the path is passed as a literal
    /// <c>--pattern</c>, so a stray <c>*</c> or <c>[</c> would change which files get restored.
    /// (Filenames containing those characters cannot be downloaded in this version — accepted limit.)
    /// </summary>
    public static bool IsValidInnerPath(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        if (value[0] != '/') return false;
        if (value.Length > 4096) return false;
        if (value.Contains("..", StringComparison.Ordinal)) return false;

        foreach (var ch in value)
        {
            if (ch < 0x20 || ch > 0x7E) return false;                 // printable ASCII only
            if (ch is '*' or '?' or '[' or ']' or '\\') return false; // glob / escape metacharacters
        }
        return true;
    }
}
