using Microsoft.Extensions.Configuration;
using PbsBrowser.Api.Pbs;
using Xunit;

namespace PbsBrowser.Api.Tests;

public sealed class PbsOptionsTests
{
    private static PbsOptions From(Dictionary<string, string?> values) =>
        PbsOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

    [Fact]
    public void Defaults_without_configuration()
    {
        var options = From([]);

        Assert.Equal(string.Empty, options.Repository);
        Assert.Null(options.Fingerprint);
        Assert.Null(options.Namespace);
        Assert.Equal("/etc/pbs/keyfile", options.KeyfilePath);
        Assert.Equal("/usr/bin/proxmox-backup-client", options.ClientPath);
    }

    [Fact]
    public void Reads_the_pbs_settings_and_treats_blank_as_unset()
    {
        var options = From(new()
        {
            ["PBS_REPOSITORY"] = "user@pbs!t@pbs.test:8007:store",
            ["PBS_FINGERPRINT"] = "aa:bb",
            ["PBS_NAMESPACE"] = "  ",
            ["PBS_KEYFILE"] = "/keys/k",
            ["PBS_CLIENT_PATH"] = "",
        });

        Assert.Equal("user@pbs!t@pbs.test:8007:store", options.Repository);
        Assert.Equal("aa:bb", options.Fingerprint);
        Assert.Null(options.Namespace);
        Assert.Equal("/keys/k", options.KeyfilePath);
        Assert.Equal("/usr/bin/proxmox-backup-client", options.ClientPath);
    }

    [Fact]
    public void HasKeyfile_follows_the_file()
    {
        var path = Path.GetTempFileName();
        try
        {
            Assert.True(new PbsOptions { KeyfilePath = path }.HasKeyfile);
        }
        finally
        {
            File.Delete(path);
        }
        Assert.False(new PbsOptions { KeyfilePath = path }.HasKeyfile);
    }
}
