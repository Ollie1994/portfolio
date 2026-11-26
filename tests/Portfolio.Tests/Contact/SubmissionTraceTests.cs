using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Portfolio.Api.Services;
using Portfolio.Shared.Contact;

namespace Portfolio.Tests.Contact;

/// <summary>
/// A submission is delivered as one trace, and the only way it is read is the query in
/// README.md ("Reading submissions"). So these tests take the prefix and the property
/// names from that query and read the logged message the way it does. If the two ever
/// disagree, the query keeps running and returns empty columns: nothing else would fail.
/// </summary>
public class SubmissionTraceTests
{
    private static readonly ContactRequest Submission = new()
    {
        Name = "Åsa Öberg",
        Email = "asa@example.se",
        Message = "Hej! \"Quotes\", a {brace}, <angle brackets> and a line break\nfor good measure 🚂"
    };

    [Fact]
    public void AcceptedSubmission_IsOneTraceTheReadmeQueryReads()
    {
        var query = ReadmeQuery.Load();
        var logger = new CapturingLogger();

        var result = new ContactService(logger).Submit(Submission);

        Assert.Equal(ContactOutcome.Accepted, result.Outcome);
        var trace = Assert.Single(logger.Messages, m => m.StartsWith(query.Prefix, StringComparison.Ordinal));

        // What the query does: parse_json(substring(message, indexof(message, "{"))).
        using var payload = JsonDocument.Parse(trace[trace.IndexOf('{', StringComparison.Ordinal)..]);

        // Both ways: the query reads every property the payload has, and nothing else.
        // Property access on dynamic in KQL is case-sensitive, as these names are compared.
        var logged = payload.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal);
        Assert.Equal(query.Properties.Order(StringComparer.Ordinal), logged);

        // And each holds the submitted text exactly, non-ASCII and escapes included.
        Assert.Equal(Submission.Name, payload.RootElement.GetProperty(nameof(ContactRequest.Name)).GetString());
        Assert.Equal(Submission.Email, payload.RootElement.GetProperty(nameof(ContactRequest.Email)).GetString());
        Assert.Equal(Submission.Message, payload.RootElement.GetProperty(nameof(ContactRequest.Message)).GetString());
    }

    [Fact]
    public void InvalidSubmission_LogsNoneOfWhatWasSent()
    {
        // Field names only: the values are personal data, and a rejected message is not
        // delivered, so nothing of it belongs in the logs.
        var logger = new CapturingLogger();
        var invalid = Submission with { Message = "short" };

        var result = new ContactService(logger).Submit(invalid);

        Assert.Equal(ContactOutcome.Invalid, result.Outcome);
        AssertNothingSentWasLogged(logger, invalid);
    }

    [Fact]
    public void HoneypotSubmission_LogsNoneOfWhatWasSent()
    {
        var logger = new CapturingLogger();
        var bot = Submission with { Website = "http://spam.example" };

        var result = new ContactService(logger).Submit(bot);

        Assert.Equal(ContactOutcome.Discarded, result.Outcome);
        AssertNothingSentWasLogged(logger, bot);
    }

    private static void AssertNothingSentWasLogged(CapturingLogger logger, ContactRequest request)
    {
        Assert.NotEmpty(logger.Messages);
        Assert.All(logger.Messages, message =>
        {
            Assert.DoesNotContain(request.Name, message, StringComparison.Ordinal);
            Assert.DoesNotContain(request.Email, message, StringComparison.Ordinal);
            Assert.DoesNotContain(request.Message, message, StringComparison.Ordinal);
        });
    }

    /// <summary>The prefix and property names the README's query uses.</summary>
    private sealed record ReadmeQuery(string Prefix, IReadOnlyList<string> Properties)
    {
        public static ReadmeQuery Load()
        {
            // Linked into the test output from the repository root by the csproj.
            var readme = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "README.md"));

            var block = Regex.Match(readme, "```kusto\\n(?<query>.*?)```", RegexOptions.Singleline);
            Assert.True(block.Success, "README.md has no ```kusto block: the submissions query moved or was removed.");
            var query = block.Groups["query"].Value;

            var prefix = Regex.Match(query, "startswith \"(?<prefix>[^\"]+)\"");
            Assert.True(prefix.Success, "The README query no longer filters with startswith \"...\".");

            var properties = Regex.Matches(query, @"payload\.(?<name>\w+)")
                .Select(m => m.Groups["name"].Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            Assert.NotEmpty(properties);

            return new(prefix.Groups["prefix"].Value, properties);
        }
    }

    /// <summary>Keeps each message as the host would forward it: formatted text.</summary>
    private sealed class CapturingLogger : ILogger<ContactService>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
