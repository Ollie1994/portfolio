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
        var query = ReadmeQuery.Load("Reading submissions");
        Assert.True(
            query.Text.Contains(Extraction, StringComparison.Ordinal),
            $"The README query no longer extracts the payload with {Extraction}, which is the step this test repeats.");
        Assert.NotEmpty(query.Properties);

        var logger = new CapturingLogger();

        var result = new ContactService(logger).Submit(Submission);

        Assert.Equal(ContactOutcome.Accepted, result.Outcome);
        // Ignoring case, as KQL's startswith does (startswith_cs is the one that doesn't).
        var trace = Assert.Single(logger.Messages, m => m.StartsWith(query.Prefix, StringComparison.OrdinalIgnoreCase));

        // What the query does, checked above to be what it still says.
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

    [Theory]
    [InlineData("Hello, I would like to talk about a role.", "True")]
    [InlineData("short", "False")]
    public void HoneypotSubmission_SaysWhetherTheRestWasValid(string message, string expected)
    {
        // A valid message caught by the honeypot is most likely a person whose browser
        // or password manager filled the hidden field. The log says so, and only that.
        var logger = new CapturingLogger();

        new ContactService(logger).Submit(Submission with { Message = message, Website = "https://example.com" });

        var line = Assert.Single(logger.Messages);
        Assert.Equal($"Contact submission discarded by honeypot. The other fields were valid: {expected}", line);
    }

    [Fact]
    public void CompleteHoneypotDiscard_IsCountedByTheReadmeQuery()
    {
        // The README's honeypot query finds discards by their prefix and counts the
        // complete ones by their ending. Both have to match what is logged.
        var query = ReadmeQuery.Load("Messages caught by the honeypot");
        var ending = Regex.Match(query.Text, "endswith \"(?<ending>[^\"]+)\"");
        Assert.True(ending.Success, "The README honeypot query no longer counts with endswith \"...\".");
        var logger = new CapturingLogger();

        new ContactService(logger).Submit(Submission with { Website = "https://example.com" });

        var line = Assert.Single(logger.Messages);
        // Ignoring case, as KQL's startswith and endswith do.
        Assert.StartsWith(query.Prefix, line, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(ending.Groups["ending"].Value, line, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertNothingSentWasLogged(CapturingLogger logger, ContactRequest request)
    {
        // The honeypot value too: it is the one field that path reads, and it comes from
        // whoever is attacking the endpoint.
        var sent = new[] { request.Name, request.Email, request.Message, request.Website }
            .Where(value => !string.IsNullOrEmpty(value))
            .Cast<string>()
            .ToList();

        Assert.NotEmpty(logger.Messages);
        Assert.All(logger.Messages, message => Assert.All(sent, value =>
        {
            Assert.DoesNotContain(value, message, StringComparison.Ordinal);

            // And as JSON, the way Deliver logs it: the serializer escapes non-ASCII, so
            // "Åsa" goes out as "Åsa", which the raw value would never match.
            Assert.DoesNotContain(JsonSerializer.Serialize(value).Trim('"'), message, StringComparison.Ordinal);
        }));
    }

    /// <summary>The submissions query's step that the test repeats in C#, so it must still use it.</summary>
    private const string Extraction = "parse_json(substring(message, indexof(message, \"{\")))";

    /// <summary>A Kusto query from the README: its text, prefix and the payload names it reads.</summary>
    private sealed record ReadmeQuery(string Text, string Prefix, IReadOnlyList<string> Properties)
    {
        /// <param name="heading">The ### heading the query sits under.</param>
        public static ReadmeQuery Load(string heading)
        {
            // Linked into the test output from the repository root by the csproj.
            var readme = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "README.md"));

            // The query under its own heading, up to the next heading, so another Kusto
            // block elsewhere in the README is never taken for it. \r? in case an editor
            // saved the file with Windows line endings.
            var section = Regex.Match(
                readme,
                $@"^### {Regex.Escape(heading)}[ \t]*\r?$(?<section>.*?)(?=^#{{1,3}} |\z)",
                RegexOptions.Singleline | RegexOptions.Multiline);
            Assert.True(section.Success, $"README.md has no \"### {heading}\" section.");

            var block = Regex.Match(section.Groups["section"].Value, "```kusto[ \\t]*\\r?\\n(?<query>.*?)```", RegexOptions.Singleline);
            Assert.True(block.Success, $"\"{heading}\" in README.md has no ```kusto block: the query moved or was removed.");
            var query = block.Groups["query"].Value;

            var prefix = Regex.Match(query, "startswith \"(?<prefix>[^\"]+)\"");
            Assert.True(prefix.Success, $"The README query under \"{heading}\" no longer filters with startswith \"...\".");

            var properties = Regex.Matches(query, @"payload\.(?<name>\w+)")
                .Select(m => m.Groups["name"].Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return new(query, prefix.Groups["prefix"].Value, properties);
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
