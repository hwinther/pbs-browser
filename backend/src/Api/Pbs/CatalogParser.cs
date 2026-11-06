namespace PbsBrowser.Api.Pbs;

/// <summary>
/// Parses <c>proxmox-backup-client catalog dump &lt;snapshot&gt;</c> output into one tree per archive.
///
/// Each line is <c>&lt;type&gt; "&lt;path&gt;" [size mtime]</c>. A snapshot's catalog covers all of
/// its pxar archives, each as a top-level <c>./&lt;archive&gt;.didx</c> directory (client 4.2.8):
/// <code>
/// d "./data.pxar.didx"
/// f "./data.pxar.didx/mosquitto.db" 6812 2026-11-06T20:34:28Z
/// d "./bin.pxar.didx"
/// f "./bin.pxar.didx/pbs-job" 1904800 2026-11-06T08:34:08Z
/// </code>
/// The archive component picks the tree, keyed by the name <c>restore</c> takes (<c>data.pxar</c>,
/// without the index suffix), and the rest is the path inside it (<c>/mosquitto.db</c>). Lines without
/// an archive component are ignored. Type <c>d</c> is a directory, any other entry type a file; files
/// carry a trailing size which we capture.
///
/// proxmox-backup-client 4.x writes the dump to <b>stderr</b> (via its logger) and leaves stdout
/// empty, so <see cref="ParseCommandOutput"/> reads both streams. Only lines whose type is a known
/// catalog entry type are taken, so the client's own log lines on stderr never become tree nodes.
/// </summary>
public static class CatalogParser
{
    // pxar catalog entry types: directory, file, symlink, hardlink, block/char device, fifo, socket.
    private static readonly HashSet<string> EntryTypes = ["d", "f", "l", "h", "b", "c", "p", "s"];

    /// <summary>Parses a <c>catalog dump</c> run, wherever the client put the listing.</summary>
    public static SnapshotCatalog ParseCommandOutput(CommandResult result) =>
        Parse(result.StdOut + "\n" + result.StdErr);

    public static SnapshotCatalog Parse(string dumpText)
    {
        var archives = new Dictionary<string, CatalogNode>(StringComparer.Ordinal);

        foreach (var rawLine in dumpText.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            if (!TryParse(line, out var type, out var rawPath, out var size)) continue;
            if (!TrySplitArchive(rawPath, out var archive, out var inner)) continue;

            if (!archives.TryGetValue(archive, out var root))
                archives[archive] = root = SnapshotCatalog.EmptyRoot();

            // The archive's own line ("d ./data.pxar.didx") only registers the archive.
            if (inner.Length > 0)
                Insert(root, inner, isDir: type.StartsWith('d'), size);
        }

        foreach (var root in archives.Values)
            SortRecursive(root);
        return new SnapshotCatalog(archives);
    }

    private static bool TryParse(string line, out string type, out string path, out long? size)
    {
        type = string.Empty;
        path = string.Empty;
        size = null;

        var q1 = line.IndexOf('"');
        var q2 = line.LastIndexOf('"');
        if (q1 >= 0 && q2 > q1)
        {
            type = line[..q1].Trim();
            path = line.Substring(q1 + 1, q2 - q1 - 1);
            // Tail after the closing quote is `<size> <mtime>` for files.
            foreach (var token in line[(q2 + 1)..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                if (long.TryParse(token, out var s))
                {
                    size = s;
                    break;
                }
            }
            return EntryTypes.Contains(type);
        }

        // Fallback: "<type> <path>" without quotes.
        var sp = line.IndexOf(' ');
        if (sp <= 0) return false;
        type = line[..sp];
        path = line[(sp + 1)..].Trim();
        return EntryTypes.Contains(type);
    }

    // "./config.pxar.didx/worlds_local/world.db" -> ("config.pxar", "/worlds_local/world.db")
    // "./config.pxar.didx"                      -> ("config.pxar", "")
    private static bool TrySplitArchive(string path, out string archive, out string inner)
    {
        archive = string.Empty;
        inner = string.Empty;

        path = path.Trim().Trim('"');
        if (path.StartsWith("./", StringComparison.Ordinal)) path = path[2..];
        path = path.Trim('/');
        if (path.Length == 0) return false;

        var slash = path.IndexOf('/');
        var first = slash < 0 ? path : path[..slash];
        if (!IsArchiveComponent(first)) return false;

        archive = SnapshotCatalog.ArchiveKey(first);
        inner = slash < 0 ? string.Empty : "/" + path[(slash + 1)..];
        return true;
    }

    private static bool IsArchiveComponent(string s) =>
        s.EndsWith(".didx", StringComparison.Ordinal) ||
        s.EndsWith(".fidx", StringComparison.Ordinal) ||
        s.EndsWith(".pxar", StringComparison.Ordinal) ||
        s.EndsWith(".mpxar", StringComparison.Ordinal) ||
        s.EndsWith(".ppxar", StringComparison.Ordinal);

    private static void Insert(CatalogNode root, string absolutePath, bool isDir, long? size)
    {
        var parts = absolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = root;
        var built = string.Empty;

        for (var i = 0; i < parts.Length; i++)
        {
            var name = parts[i];
            built += "/" + name;
            var last = i == parts.Length - 1;

            var child = current.Children.FirstOrDefault(c => c.Name == name);
            if (child is null)
            {
                child = new CatalogNode
                {
                    Name = name,
                    Path = built,
                    IsDir = !last || isDir,
                    Size = last ? size : null,
                };
                current.Children.Add(child);
            }
            else if (last)
            {
                child.IsDir = isDir;
                child.Size = size;
            }

            current = child;
        }
    }

    private static void SortRecursive(CatalogNode node)
    {
        node.Children.Sort(static (a, b) =>
        {
            if (a.IsDir != b.IsDir) return a.IsDir ? -1 : 1; // directories first
            return string.CompareOrdinal(a.Name, b.Name);
        });
        foreach (var child in node.Children)
            SortRecursive(child);
    }
}
