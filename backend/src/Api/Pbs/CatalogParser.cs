namespace PbsBrowser.Api.Pbs;

/// <summary>
/// Parses the text output of <c>proxmox-backup-client catalog dump &lt;snapshot&gt;</c> into a tree.
///
/// The dump is a line-per-entry listing; each line carries a type/mode prefix followed by an absolute
/// path (entries are always rooted at <c>/</c>). We locate the path as the substring starting at the
/// first <c>/</c>, treat a leading <c>d</c> in the prefix (or a trailing <c>/</c>) as a directory, and
/// take the first all-digit token in the prefix as the size when present.
///
/// The exact column layout is not a stable API, so only this tokenizer should need adjusting per
/// client version — the tree-assembly logic below is covered by unit tests.
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

            var slash = line.IndexOf('/');
            if (slash < 0) continue;

            var prefix = line[..slash].Trim();
            var path = line[slash..].Trim();
            if (path is "/" or "") continue; // root already exists

            var isDir = prefix.StartsWith('d') || path.EndsWith('/');
            var size = ExtractSize(prefix);
            Insert(root, path.TrimEnd('/'), isDir, size);
        }

        SortRecursive(root);
        return root;
    }

    private static long? ExtractSize(string prefix)
    {
        foreach (var token in prefix.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (long.TryParse(token, out var value))
                return value;
        }
        return null;
    }

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
