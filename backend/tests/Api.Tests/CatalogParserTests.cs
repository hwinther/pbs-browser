using System.Linq;
using PbsBrowser.Api.Pbs;
using Xunit;

namespace PbsBrowser.Api.Tests;

public class CatalogParserTests
{
    // Real `proxmox-backup-client catalog dump` shape: <type> "<./archive/path>" [size mtime].
    // type d=dir, f=file; files carry a trailing size + mtime.
    private const string Dump =
        """
        d "./config.pxar.didx"
        f "./config.pxar.didx/adminlist.txt" 57 2026-06-27T16:54:16Z
        d "./config.pxar.didx/worlds_local"
        f "./config.pxar.didx/worlds_local/world.db" 8439727 2026-07-01T04:00:22Z
        f "./config.pxar.didx/worlds_local/world.fwl" 2410 2026-07-01T04:00:22Z
        """;

    [Fact]
    public void Parse_strips_archive_prefix_and_root()
    {
        var root = CatalogParser.Parse(Dump);

        // the "config.pxar.didx" archive component/root is stripped; top level is real fs paths
        Assert.DoesNotContain(root.Children, c => c.Name == "config.pxar.didx");
        // directories first, then files
        Assert.Equal(["worlds_local", "adminlist.txt"], root.Children.Select(c => c.Name).ToArray());
    }

    [Fact]
    public void Parse_captures_file_size_and_marks_type()
    {
        var root = CatalogParser.Parse(Dump);

        var admin = root.Children.Single(c => c.Name == "adminlist.txt");
        Assert.False(admin.IsDir);
        Assert.Equal("/adminlist.txt", admin.Path);
        Assert.Equal(57, admin.Size);
    }

    [Fact]
    public void Parse_builds_nested_dirs_with_sizes()
    {
        var root = CatalogParser.Parse(Dump);

        var worlds = root.Children.Single(c => c.Name == "worlds_local");
        Assert.True(worlds.IsDir);

        var db = worlds.Children.Single(c => c.Name == "world.db");
        Assert.False(db.IsDir);
        Assert.Equal(8439727, db.Size);
        Assert.Equal("/worlds_local/world.db", db.Path);
    }

    // Captured verbatim from proxmox-backup-client 4.2.8 `catalog dump host/mosquitto/…`: exit 0,
    // stdout empty, the listing on stderr, each line padded with trailing spaces.
    private static readonly CommandResult RealDump428 = new(
        0,
        string.Empty,
        "d \"./data.pxar.didx\"    \n" +
        "d \"./data.pxar.didx/lost+found\"    \n" +
        "f \"./data.pxar.didx/mosquitto.db\" 6818 2026-11-06T08:40:57Z    \n");

    [Fact]
    public void ParseCommandOutput_reads_listing_from_stderr_as_client_4_2_8_writes_it()
    {
        var root = CatalogParser.ParseCommandOutput(RealDump428);

        Assert.Equal(["lost+found", "mosquitto.db"], root.Children.Select(c => c.Name).ToArray());
        Assert.True(root.Children[0].IsDir);
        var db = root.Children[1];
        Assert.False(db.IsDir);
        Assert.Equal("/mosquitto.db", db.Path);
        Assert.Equal(6818, db.Size);
    }

    [Fact]
    public void ParseCommandOutput_still_reads_stdout()
    {
        var root = CatalogParser.ParseCommandOutput(new CommandResult(0, Dump, string.Empty));
        Assert.Equal(["worlds_local", "adminlist.txt"], root.Children.Select(c => c.Name).ToArray());
    }

    [Fact]
    public void Parse_ignores_client_log_lines()
    {
        var root = CatalogParser.Parse(
            "Error: something went sideways\n" +
            "starting catalog dump\n" +
            "WARN \"./data.pxar.didx/not-a-file\"\n" +
            "f \"./data.pxar.didx/real\" 1 2026-01-01T00:00:00Z\n");

        Assert.Equal("real", Assert.Single(root.Children).Name);
    }

    [Fact]
    public void Parse_handles_unquoted_fallback_lines()
    {
        var root = CatalogParser.Parse("d ./config.pxar.didx/opt\nf ./config.pxar.didx/opt/app.conf\n");
        var app = root.Children.Single(c => c.Name == "opt").Children.Single(c => c.Name == "app.conf");
        Assert.False(app.IsDir);
    }

    [Fact]
    public void Parse_ignores_blank_and_pathless_lines()
    {
        var root = CatalogParser.Parse("\n   \ngarbage\nf \"./config.pxar.didx/a\" 1 2026-01-01T00:00:00Z\n");
        Assert.Single(root.Children);
        Assert.Equal("a", root.Children[0].Name);
    }
}
