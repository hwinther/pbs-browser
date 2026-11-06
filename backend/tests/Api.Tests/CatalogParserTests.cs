using PbsBrowser.Api.Pbs;
using Xunit;

namespace PbsBrowser.Api.Tests;

public class CatalogParserTests
{
    // `proxmox-backup-client catalog dump` shape: <type> "<./archive/path>" [size mtime].
    // type d=dir, f=file; files carry a trailing size + mtime.
    private const string Dump =
        """
        d "./config.pxar.didx"
        f "./config.pxar.didx/adminlist.txt" 57 2026-06-27T16:54:16Z
        d "./config.pxar.didx/worlds_local"
        f "./config.pxar.didx/worlds_local/world.db" 8439727 2026-07-01T04:00:22Z
        f "./config.pxar.didx/worlds_local/world.fwl" 2410 2026-07-01T04:00:22Z
        """;

    // Captured verbatim from proxmox-backup-client 4.2.8 `catalog dump host/mosquitto/…`: exit 0,
    // stdout empty, the listing on stderr, each line padded with trailing spaces.
    private static readonly CommandResult RealDump428 = new(
        0,
        string.Empty,
        "d \"./data.pxar.didx\"    \n" +
        "d \"./data.pxar.didx/lost+found\"    \n" +
        "f \"./data.pxar.didx/mosquitto.db\" 6818 2026-11-06T08:40:57Z    \n");

    // Captured verbatim from proxmox-backup-client 4.2.8 `catalog dump host/pbs-browser-fixture/…`,
    // a two-archive snapshot (data.pxar:/data, bin.pxar:/usr/bin): each archive is its own top-level
    // ./<archive>.didx directory, in backup order rather than sorted.
    private static readonly CommandResult RealTwoArchiveDump428 = new(
        0,
        string.Empty,
        "d \"./data.pxar.didx\"    \n" +
        "d \"./data.pxar.didx/lost+found\"    \n" +
        "f \"./data.pxar.didx/mosquitto.db\" 6812 2026-11-06T20:34:28Z    \n" +
        "d \"./bin.pxar.didx\"    \n" +
        "f \"./bin.pxar.didx/pbs-job\" 1904800 2026-11-06T08:34:08Z    \n" +
        "f \"./bin.pxar.didx/proxmox-backup-client\" 21446320 2026-11-06T08:34:10Z    \n");

    private static string[] Names(CatalogNode node) => node.Children.Select(c => c.Name).ToArray();

    [Fact]
    public void Parse_strips_archive_prefix_and_root()
    {
        var root = CatalogParser.Parse(Dump).For("config.pxar");

        // the "config.pxar.didx" archive component/root is stripped; top level is real fs paths
        Assert.DoesNotContain(root.Children, c => c.Name == "config.pxar.didx");
        // directories first, then files
        Assert.Equal(["worlds_local", "adminlist.txt"], Names(root));
    }

    [Fact]
    public void Parse_captures_file_size_and_marks_type()
    {
        var root = CatalogParser.Parse(Dump).For("config.pxar");

        var admin = root.Children.Single(c => c.Name == "adminlist.txt");
        Assert.False(admin.IsDir);
        Assert.Equal("/adminlist.txt", admin.Path);
        Assert.Equal(57, admin.Size);
    }

    [Fact]
    public void Parse_builds_nested_dirs_with_sizes()
    {
        var root = CatalogParser.Parse(Dump).For("config.pxar");

        var worlds = root.Children.Single(c => c.Name == "worlds_local");
        Assert.True(worlds.IsDir);

        var db = worlds.Children.Single(c => c.Name == "world.db");
        Assert.False(db.IsDir);
        Assert.Equal(8439727, db.Size);
        Assert.Equal("/worlds_local/world.db", db.Path);
    }

    [Fact]
    public void ParseCommandOutput_reads_listing_from_stderr_as_client_4_2_8_writes_it()
    {
        var root = CatalogParser.ParseCommandOutput(RealDump428).For("data.pxar");

        Assert.Equal(["lost+found", "mosquitto.db"], Names(root));
        Assert.True(root.Children[0].IsDir);
        var db = root.Children[1];
        Assert.False(db.IsDir);
        Assert.Equal("/mosquitto.db", db.Path);
        Assert.Equal(6818, db.Size);
    }

    [Fact]
    public void ParseCommandOutput_still_reads_stdout()
    {
        var root = CatalogParser.ParseCommandOutput(new CommandResult(0, Dump, string.Empty)).For("config.pxar");
        Assert.Equal(["worlds_local", "adminlist.txt"], Names(root));
    }

    [Fact]
    public void Parse_builds_a_separate_tree_per_archive()
    {
        var catalog = CatalogParser.ParseCommandOutput(RealTwoArchiveDump428);

        Assert.Equal(["bin.pxar", "data.pxar"], catalog.Archives.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(["lost+found", "mosquitto.db"], Names(catalog.For("data.pxar")));

        var bin = catalog.For("bin.pxar");
        Assert.Equal(["pbs-job", "proxmox-backup-client"], Names(bin));
        Assert.Equal("/proxmox-backup-client", bin.Children[1].Path);
        Assert.Equal(21446320, bin.Children[1].Size);
    }

    [Theory]
    [InlineData("bin.pxar")]
    [InlineData("bin.pxar.didx")]
    public void For_accepts_the_archive_with_or_without_index_suffix(string archive)
    {
        var catalog = CatalogParser.ParseCommandOutput(RealTwoArchiveDump428);
        Assert.Equal(["pbs-job", "proxmox-backup-client"], Names(catalog.For(archive)));
    }

    [Fact]
    public void For_an_archive_not_in_the_catalog_is_empty()
    {
        var root = CatalogParser.ParseCommandOutput(RealTwoArchiveDump428).For("drive-scsi0.img");
        Assert.True(root.IsDir);
        Assert.Empty(root.Children);
    }

    [Fact]
    public void Parse_registers_an_archive_with_no_entries()
    {
        var catalog = CatalogParser.Parse("d \"./empty.pxar.didx\"\n");
        Assert.True(catalog.Archives.ContainsKey("empty.pxar"));
        Assert.Empty(catalog.For("empty.pxar").Children);
    }

    [Fact]
    public void Parse_ignores_client_log_lines()
    {
        var root = CatalogParser.Parse(
            "Error: something went sideways\n" +
            "starting catalog dump\n" +
            "WARN \"./data.pxar.didx/not-a-file\"\n" +
            "f \"./data.pxar.didx/real\" 1 2026-01-01T00:00:00Z\n").For("data.pxar");

        Assert.Equal("real", Assert.Single(root.Children).Name);
    }

    [Fact]
    public void Parse_ignores_lines_without_an_archive_component()
    {
        var catalog = CatalogParser.Parse("f \"./stray\" 1 2026-01-01T00:00:00Z\nd \"./etc/opt\"\n");
        Assert.Empty(catalog.Archives);
    }

    [Fact]
    public void Parse_handles_unquoted_fallback_lines()
    {
        var root = CatalogParser.Parse("d ./config.pxar.didx/opt\nf ./config.pxar.didx/opt/app.conf\n").For("config.pxar");
        var app = root.Children.Single(c => c.Name == "opt").Children.Single(c => c.Name == "app.conf");
        Assert.False(app.IsDir);
    }

    [Fact]
    public void Parse_ignores_blank_and_pathless_lines()
    {
        var root = CatalogParser.Parse("\n   \ngarbage\nf \"./config.pxar.didx/a\" 1 2026-01-01T00:00:00Z\n").For("config.pxar");
        Assert.Equal("a", Assert.Single(root.Children).Name);
    }
}
