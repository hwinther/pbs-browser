using System.Linq;
using PbsBrowser.Api.Pbs;
using Xunit;

namespace PbsBrowser.Api.Tests;

public class CatalogParserTests
{
    private const string Dump =
        """
        d---        /
        d---        /etc
        f--- 158    /etc/hosts
        d---        /var
        f--- 4096   /var/lib/grafana/grafana.db
        """;

    [Fact]
    public void Parse_builds_nested_tree()
    {
        var root = CatalogParser.Parse(Dump);

        Assert.Equal("/", root.Path);
        Assert.True(root.IsDir);

        // directories sort before files; etc + var are the top-level dirs
        var names = root.Children.Select(c => c.Name).ToArray();
        Assert.Equal(["etc", "var"], names);
    }

    [Fact]
    public void Parse_marks_files_and_sizes()
    {
        var root = CatalogParser.Parse(Dump);

        var hosts = root.Children.Single(c => c.Name == "etc").Children.Single(c => c.Name == "hosts");
        Assert.False(hosts.IsDir);
        Assert.Equal(158, hosts.Size);
        Assert.Equal("/etc/hosts", hosts.Path);
    }

    [Fact]
    public void Parse_creates_intermediate_directories()
    {
        var root = CatalogParser.Parse(Dump);

        var grafanaDb = root
            .Children.Single(c => c.Name == "var")
            .Children.Single(c => c.Name == "lib")
            .Children.Single(c => c.Name == "grafana")
            .Children.Single(c => c.Name == "grafana.db");

        Assert.False(grafanaDb.IsDir);
        Assert.Equal(4096, grafanaDb.Size);
        // intermediate "lib" was never an explicit line, so it must have been synthesised as a dir
        var lib = root.Children.Single(c => c.Name == "var").Children.Single(c => c.Name == "lib");
        Assert.True(lib.IsDir);
    }

    [Fact]
    public void Parse_detects_trailing_slash_directories()
    {
        var root = CatalogParser.Parse("0  /opt/data/\n");
        var data = root.Children.Single(c => c.Name == "opt").Children.Single(c => c.Name == "data");
        Assert.True(data.IsDir);
    }

    [Fact]
    public void Parse_ignores_blank_and_pathless_lines()
    {
        var root = CatalogParser.Parse("\n   \ngarbage-without-slash\nf--- 1 /a\n");
        Assert.Single(root.Children);
        Assert.Equal("a", root.Children[0].Name);
    }
}
