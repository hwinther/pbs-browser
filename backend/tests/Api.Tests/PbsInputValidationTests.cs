using PbsBrowser.Api.Pbs;
using Xunit;

namespace PbsBrowser.Api.Tests;

public class PbsInputValidationTests
{
    [Theory]
    [InlineData("host/grafana/2026-06-30T01:02:03Z")]
    [InlineData("vm/100/2024-01-01T00:00:00Z")]
    [InlineData("ct/node-red.prod/2025-12-31T23:59:59Z")]
    public void IsValidSnapshot_accepts_well_formed(string value) =>
        Assert.True(PbsInputValidation.IsValidSnapshot(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("host/grafana")]                              // missing time
    [InlineData("foo/grafana/2026-06-30T01:02:03Z")]         // bad type
    [InlineData("host/../2026-06-30T01:02:03Z")]             // traversal in id
    [InlineData("host/x/2026-06-30T01:02:03Z; rm -rf /")]    // trailing junk
    [InlineData("host/x/2026-06-30 01:02:03")]               // wrong time format
    public void IsValidSnapshot_rejects_bad(string? value) =>
        Assert.False(PbsInputValidation.IsValidSnapshot(value));

    [Theory]
    [InlineData("root.pxar")]
    [InlineData("db.pxar.didx")]
    [InlineData("drive-scsi0.img")]
    [InlineData("index.json.blob")]
    public void IsValidArchive_accepts_known_suffixes(string value) =>
        Assert.True(PbsInputValidation.IsValidArchive(value));

    [Theory]
    [InlineData(null)]
    [InlineData("root.tar")]            // unknown suffix
    [InlineData("../root.pxar")]        // traversal
    [InlineData("root.pxar; id")]       // injection attempt
    [InlineData("root pxar")]           // space
    public void IsValidArchive_rejects_bad(string? value) =>
        Assert.False(PbsInputValidation.IsValidArchive(value));

    [Theory]
    [InlineData("/etc/hosts")]
    [InlineData("/var/lib/grafana/grafana.db")]
    [InlineData("/a/b/c/d.txt")]
    public void IsValidInnerPath_accepts_absolute_clean(string value) =>
        Assert.True(PbsInputValidation.IsValidInnerPath(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("etc/hosts")]              // not absolute
    [InlineData("/etc/../../secret")]      // traversal
    [InlineData("/etc/ho*sts")]            // glob star
    [InlineData("/etc/ho?sts")]            // glob question
    [InlineData("/etc/[abc]")]             // glob class
    [InlineData("/etc/back\\slash")]       // backslash
    [InlineData("/etc/null\0byte")]        // NUL
    public void IsValidInnerPath_rejects_dangerous(string? value) =>
        Assert.False(PbsInputValidation.IsValidInnerPath(value));
}
