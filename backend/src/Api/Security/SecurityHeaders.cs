namespace PbsBrowser.Api.Security;

/// <summary>
/// Security headers and the Content-Security-Policy on every response.
/// </summary>
/// <remarks>
/// The SPA has no inline scripts or styles (Vite emits files only), so the policy needs no nonce and
/// is the same for every response. CSP_MODE=report-only (the default) sends it as
/// Content-Security-Policy-Report-Only: browsers report violations to <see cref="ReportPath"/>
/// (<see cref="CspReports"/>) and block nothing. CSP_MODE=enforce blocks.
/// </remarks>
internal static class SecurityHeaders
{
    public const string ReportPath = "/csp-report";

    internal const string PermissionsPolicy = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";

    internal static readonly string ContentSecurityPolicy = string.Join("; ",
        "default-src 'self'",
        "script-src 'self'",
        "style-src 'self'",
        "img-src 'self' data:",
        "connect-src 'self'",
        "object-src 'none'",
        "base-uri 'none'",
        "form-action 'self'",
        "frame-ancestors 'none'",
        $"report-uri {ReportPath}",
        "report-to csp");

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, IConfiguration configuration)
    {
        var cspHeader = string.Equals(configuration["CSP_MODE"]?.Trim(), "enforce", StringComparison.Ordinal)
            ? "Content-Security-Policy"
            : "Content-Security-Policy-Report-Only";

        return app.Use((context, next) =>
        {
            var headers = context.Response.Headers;
            // Not includeSubDomains: that would force HTTPS on every *.wsh.no name.
            headers["Strict-Transport-Security"] = "max-age=31536000";
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = PermissionsPolicy;
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers[cspHeader] = ContentSecurityPolicy;
            headers["Reporting-Endpoints"] = $"csp=\"{ReportPath}\"";
            return next(context);
        });
    }
}
