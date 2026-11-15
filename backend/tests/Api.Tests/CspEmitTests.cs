using Microsoft.Extensions.Logging;
using PbsBrowser.Api.Security;
using Xunit;

namespace PbsBrowser.Api.Tests;

public sealed class CspEmitTests
{
    [Fact]
    public void Each_report_is_one_warning_with_its_attributes_in_scope()
    {
        var logger = new RecordingLogger();

        CspReports.Emit(logger,
        [
            new CspReport("https://evil.test/x.js", "enforce", "https://pbs.test/", "script-src-elem",
                SourceFile: "https://pbs.test/app.js", LineNumber: 3, ColumnNumber: 9, Sample: "alert(1)"),
            new CspReport("", "report", "https://pbs.test/", "img-src"),
        ], new string('u', 600));

        Assert.Equal(2, logger.Records.Count);
        var (level, message, scope) = logger.Records[0];
        Assert.Equal(LogLevel.Warning, level);
        Assert.Equal("CSP enforce: script-src-elem https://evil.test/x.js on https://pbs.test/", message);
        Assert.Equal("csp.violation", scope["event.name"]);
        Assert.Equal("https://pbs.test/app.js", scope["csp.source_file"]);
        Assert.Equal(3L, scope["csp.line_number"]);
        Assert.Equal(9L, scope["csp.column_number"]);
        Assert.Equal("alert(1)", scope["csp.sample"]);
        Assert.Equal(513, ((string)scope["user_agent.original"]!).Length);

        Assert.Equal(["event.name", "user_agent.original"], logger.Records[1].Scope.Keys.Order());
    }

    [Fact]
    public void No_user_agent_attribute_without_one()
    {
        var logger = new RecordingLogger();
        CspReports.Emit(logger, [new CspReport("inline", "report", "https://pbs.test/", "script-src")], null);
        Assert.Equal(["event.name"], logger.Records.Single().Scope.Keys);
    }

    private sealed class RecordingLogger : ILogger
    {
        private Dictionary<string, object?> _scope = [];

        public List<(LogLevel Level, string Message, Dictionary<string, object?> Scope)> Records { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            _scope = new Dictionary<string, object?>((IDictionary<string, object?>)state);
            return new Reset(this);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Records.Add((logLevel, formatter(state, exception), _scope));

        private sealed class Reset(RecordingLogger logger) : IDisposable
        {
            public void Dispose() => logger._scope = [];
        }
    }
}
