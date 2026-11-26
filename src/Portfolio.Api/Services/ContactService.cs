using System.Text.Json;
using Microsoft.Extensions.Logging;
using Portfolio.Shared.Contact;

namespace Portfolio.Api.Services;

/// <summary>
/// Handles a contact submission: honeypot, validation, then delivery.
///
/// Deliberately a plain class with no Functions or HTTP types, so it can be
/// constructed directly in a test. The function class is only an adapter over
/// this.
/// </summary>
public sealed class ContactService
{
    private readonly ILogger<ContactService> _logger;

    public ContactService(ILogger<ContactService> logger) => _logger = logger;

    public ContactSubmissionResult Submit(ContactRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Checked before validation, so a bot is told it succeeded and never learns
        // which of its fields were wrong.
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            // Whether the rest would have passed, and nothing of what was sent. The form
            // refuses to send an invalid message, so True means it came through the form
            // complete: a bot driving a browser, or a person whose browser or password
            // manager filled the hidden field, which nothing here can tell apart. False
            // means someone called the API directly. On a site this quiet, a run of True
            // is the thing to look into; the README has the query. The validation is pure
            // and the request body is capped at 16 KB, so it costs nothing worth saving.
            _logger.LogInformation(
                "Contact submission discarded by honeypot. The other fields were valid: {OtherFieldsValid}",
                ContactValidator.Validate(request).IsValid);

            return ContactSubmissionResult.Discarded;
        }

        var validation = ContactValidator.Validate(request);
        if (!validation.IsValid)
        {
            // Field names only. The values are the sender's personal data and do
            // not belong in logs.
            _logger.LogInformation(
                "Contact submission rejected. Invalid fields: {Fields}",
                string.Join(", ", validation.Errors.Select(e => e.Field)));

            return ContactSubmissionResult.Invalid(validation);
        }

        Deliver(request);

        return ContactSubmissionResult.Accepted;
    }

    /// <summary>
    /// Delivery. The structured log entry <em>is</em> the delivery mechanism:
    /// Application Insights is enabled on the Static Web App, so this reaches a
    /// queryable store that survives the invocation, and an alert rule can turn
    /// a match into an email notification.
    ///
    /// Chosen over a mail provider deliberately. Azure Communication Services
    /// bills per message with no free allowance, which makes an anonymous public
    /// endpoint a billable amplifier; SendGrid withdrew its free tier in 2025.
    /// Logging has a genuine 5 GB/month free grant and a daily cap, so it
    /// degrades instead of invoicing. See CLAUDE.md.
    ///
    /// Note this deliberately records personal data — name, address and message
    /// body — for the retention period configured on the resource. That is the
    /// point of the feature, and the form says so.
    /// </summary>
    private void Deliver(ContactRequest request)
    {
        // Logged as a JSON payload rather than as separate template properties.
        //
        // Structured log properties do NOT survive forwarding from the isolated
        // worker to the Functions host: customDimensions in Application Insights
        // carries only host metadata (Category, InvocationId, LogLevel...), and
        // the template values are flattened into the message text. Verified by
        // inspecting a real trace - see CLAUDE.md.
        //
        // Serialising to JSON keeps the fields machine-readable, and JSON
        // escaping means submitted content cannot corrupt the structure the way
        // a delimiter-based format could.
        //
        // The query in README.md ("Reading submissions") is the only reader. It finds
        // the trace by the message prefix and reads these exact property names, and
        // property access on a dynamic value in KQL is case-sensitive: so default
        // (PascalCase) serialisation, not the Web defaults JsonBody reads with.
        // SubmissionTraceTests checks the two agree.
        var payload = JsonSerializer.Serialize(new
        {
            request.Name,
            request.Email,
            request.Message
        });

        _logger.LogInformation("Contact submission received: {Payload}", payload);
    }
}
