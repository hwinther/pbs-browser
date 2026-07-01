using System.Linq;
using PbsBrowser.Api.Pbs;
using Xunit;

namespace PbsBrowser.Api.Tests;

public class SnapshotParserTests
{
    [Fact]
    public void Parse_reads_and_sorts_newest_first()
    {
        const string json =
            """
            [
              { "backup-type": "vm",   "backup-id": "100",     "backup-time": 1700000000 },
              { "backup-type": "host", "backup-id": "grafana", "backup-time": 1759190400, "size": 12345, "comment": "daily" }
            ]
            """;

        var list = SnapshotParser.Parse(json);

        Assert.Equal(2, list.Count);
        Assert.Equal("host", list[0].BackupType);   // 1759190400 is newer -> first
        Assert.Equal("grafana", list[0].BackupId);
        Assert.Equal(12345, list[0].Size);
        Assert.Equal("daily", list[0].Comment);
    }

    [Fact]
    public void Parse_generates_ids_that_pass_validation()
    {
        const string json = """[ { "backup-type": "host", "backup-id": "grafana", "backup-time": 1759190400 } ]""";

        var snap = SnapshotParser.Parse(json).Single();

        Assert.StartsWith("host/grafana/", snap.Id);
        // the generated specifier must round-trip through the same allow-list the API enforces
        Assert.True(PbsInputValidation.IsValidSnapshot(snap.Id));
    }

    [Fact]
    public void ParseFiles_reads_archive_list()
    {
        const string json =
            """
            [
              { "filename": "root.pxar.didx", "size": 999, "crypt-mode": "encrypt" },
              { "filename": "index.json.blob" }
            ]
            """;

        var files = SnapshotParser.ParseFiles(json);

        Assert.Equal(2, files.Count);
        Assert.Equal("root.pxar.didx", files[0].Filename);
        Assert.Equal("encrypt", files[0].CryptMode);
        Assert.Equal(999, files[0].Size);
        Assert.Null(files[1].Size);
    }

    [Fact]
    public void Parse_handles_empty_array()
    {
        Assert.Empty(SnapshotParser.Parse("[]"));
        Assert.Empty(SnapshotParser.ParseFiles("[]"));
    }
}
