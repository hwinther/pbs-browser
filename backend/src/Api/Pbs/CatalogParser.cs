namespace PbsBrowser.Api.Pbs;

/// <summary>
/// Parses <c>proxmox-backup-client catalog dump &lt;snapshot&gt;</c> output into a tree.
///
/// Each line is <c>&lt;type&gt; "&lt;path&gt;" [size mtime]</c>, e.g.
/// <code>
/// d "./config.pxar.didx/worlds_local"
/// f "./config.pxar.didx/worlds_local/world.db" 8439727 2026-07-01T04:00:22Z
/// </code>
/// The quoted path is prefixed with <c>./</c> and the archive name (e.g. <c>config.pxar.didx</c>);
/// both are stripped so the tree shows real filesystem paths (<c>/worlds_local/world.db</c>). Type
/// <c>d</c> is a directory, anything else (<c>f</c>, <c>l</c>, …) is a file. Files carry a trailing
/// size which we capture.
/// </summary>
public static class CatalogParser
{
    public static CatalogNode Parse(string dumpText)
    {
        var root = new CatalogNode { Name = "/", Path = "/", IsDir = true };

        foreach (var rawLine in dumpText.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            if (!TryParse(line, out var type, out var rawPath, out var size)) continue;

            var inner = ToInnerPath(rawPath);
            if (inner.Length == 0) continue;

            Insert(root, inner, isDir: type.StartsWith('d'), size);
        }

        SortRecursive(root);
        return root;
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
            return type.Length > 0;
        }

        // Fallback: "<type> <path>" without quotes.
        var sp = line.IndexOf(' ');
        if (sp <= 0) return false;
        type = line[..sp];
        path = line[(sp + 1)..].Trim();
        return true;
    }

    // "./config.pxar.didx/worlds_local/world.db" -> "/worlds_local/world.db"
    private static string ToInnerPath(string path)
    {
        path = path.Trim().Trim('"');
        if (path.StartsWith("./", StringComparison.Ordinal)) path = path[2..];
        path = path.TrimStart('/');
        if (path.Length == 0) return string.Empty;

        var slash = path.IndexOf('/');
        if (slash < 0)
            return IsArchiveComponent(path) ? string.Empty : "/" + path;

        if (IsArchiveComponent(path[..slash]))
            path = path[(slash + 1)..];

        path = path.TrimEnd('/');
        return path.Length == 0 ? string.Empty : "/" + path;
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
