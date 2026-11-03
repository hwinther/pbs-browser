using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PbsBrowser.Api.Pbs;
using PbsBrowser.Api.Security;
using Xunit;

namespace PbsBrowser.Api.Tests;

public class SecurityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SecurityTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPbsClient>();
                services.AddSingleton<IPbsClient, FakePbsClient>();
            }));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Responses_carry_the_security_headers_and_a_report_only_policy()
    {
        var resp = await _factory.CreateClient().GetAsync("/healthz", Ct);

        Assert.Equal("max-age=31536000", resp.Headers.GetValues("Strict-Transport-Security").Single());
        Assert.Equal("nosniff", resp.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", resp.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", resp.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal(SecurityHeaders.PermissionsPolicy, resp.Headers.GetValues("Permissions-Policy").Single());
        Assert.Equal("same-origin", resp.Headers.GetValues("Cross-Origin-Opener-Policy").Single());
        Assert.Equal(SecurityHeaders.ContentSecurityPolicy,
            resp.Headers.GetValues("Content-Security-Policy-Report-Only").Single());
        Assert.False(resp.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("csp=\"/csp-report\"", resp.Headers.GetValues("Reporting-Endpoints").Single());
    }

    [Fact]
    public async Task Enforce_mode_sends_the_blocking_header()
    {
        var client = _factory.WithWebHostBuilder(b => b.UseSetting("CSP_MODE", "enforce")).CreateClient();
        var resp = await client.GetAsync("/healthz", Ct);

        Assert.True(resp.Headers.Contains("Content-Security-Policy"));
        Assert.False(resp.Headers.Contains("Content-Security-Policy-Report-Only"));
    }

    [Fact]
    public void The_policy_keeps_scripts_to_the_site()
    {
        Assert.Contains("script-src 'self';", SecurityHeaders.ContentSecurityPolicy);
        Assert.DoesNotContain("unsafe-", SecurityHeaders.ContentSecurityPolicy);
        Assert.Contains("frame-ancestors 'none'", SecurityHeaders.ContentSecurityPolicy);
    }

    [Theory]
    [InlineData("{\"csp-report\":{\"document-uri\":\"https://x.test/\",\"effective-directive\":\"img-src\"}}")]
    [InlineData("{not json")]
    public async Task Report_endpoint_answers_204(string body)
    {
        var resp = await _factory.CreateClient().PostAsync("/csp-report",
            new StringContent(body, Encoding.UTF8, "application/csp-report"), Ct);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task Report_endpoint_refuses_get()
    {
        var resp = await _factory.CreateClient().GetAsync("/csp-report", Ct);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, resp.StatusCode);
    }

    [Fact]
    public void Parses_report_uri_and_drops_the_query()
    {
        var reports = CspReports.Parse("""
            {"csp-report": {"blocked-uri": "inline", "disposition": "report", "line-number": 12,
              "document-uri": "https://pbs.test/browse?snapshot=x", "effective-directive": "script-src-elem"}}
            """);

        Assert.Equal(
            [new CspReport("inline", "report", "https://pbs.test/browse", "script-src-elem", LineNumber: 12)],
            reports);
    }

    [Fact]
    public void Parses_report_to_and_skips_other_types()
    {
        var reports = CspReports.Parse("""
            [{"type": "csp-violation", "body": {"blockedURL": "https://evil.test/x.js?t=1", "disposition": "enforce",
               "documentURL": "https://pbs.test/", "effectiveDirective": "script-src-elem"}},
             {"type": "deprecation", "body": {"id": "x"}}]
            """);

        Assert.Equal(
            [new CspReport("https://evil.test/x.js", "enforce", "https://pbs.test/", "script-src-elem")],
            reports);
    }

    [Fact]
    public void Truncates_long_strings_and_caps_the_number_of_reports()
    {
        var one = $$$"""{"type":"csp-violation","body":{"documentURL":"https://pbs.test/","effectiveDirective":"img-src","sample":"{{{new string('x', 2000)}}}"}}""";
        var reports = CspReports.Parse("[" + string.Join(",", Enumerable.Repeat(one, 50)) + "]");

        Assert.Equal(20, reports.Count);
        Assert.Equal(513, reports[0].Sample!.Length);
    }

    [Theory]
    [InlineData("{\"hello\":\"world\"}")]
    [InlineData("{\"csp-report\":{\"document-uri\":\"https://x.test/\"}}")]
    [InlineData("{\"csp-report\":{\"document-uri\":1,\"effective-directive\":\"img-src\"}}")]
    public void Yields_nothing_for_unusable_bodies(string body) => Assert.Empty(CspReports.Parse(body));
}
