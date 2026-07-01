using System.Text.Json;

namespace PbsBrowser.Api.Pbs;

/// <summary>Parses the JSON output of <c>snapshot list</c> / <c>snapshot files</c>.</summary>
internal static class SnapshotParser
{
    public static IReadOnlyList<SnapshotInfo> Parse(string json)
    {
        var list = new List<SnapshotInfo>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var type = GetString(el, "backup-type");
            var id = GetString(el, "backup-id");
            if (type is null || id is null) continue;

            var epoch = GetInt64(el, "backup-time") ?? 0;
            var time = DateTimeOffset.FromUnixTimeSeconds(epoch);
            var snapshotId = $"{type}/{id}/{time.UtcDateTime:yyyy-MM-ddTHH:mm:ss}Z";

            list.Add(new SnapshotInfo(snapshotId, type, id, time, GetInt64(el, "size"), GetString(el, "comment")));
        }

        list.Sort(static (a, b) => b.Time.CompareTo(a.Time)); // newest first
        return list;
    }

    public static IReadOnlyList<ArchiveInfo> ParseFiles(string json)
    {
        var list = new List<ArchiveInfo>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var name = GetString(el, "filename");
            if (name is null) continue;
            list.Add(new ArchiveInfo(name, GetInt64(el, "size"), GetString(el, "crypt-mode")));
        }
        return list;
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static long? GetInt64(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var v)
            ? v
            : null;
}
