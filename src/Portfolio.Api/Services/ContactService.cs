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

        // Checked before validation: a bot-filled honeypot should cost as little
        // as possible, and its other fields are not worth validating.
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            _logger.LogInformation("Contact submission discarded by honeypot.");
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
        // Structured, so each field is queryable in customDimensions rather than
        // buried in a formatted string.
        _logger.LogInformation(
            "Contact submission received. Name={Name} Email={Email} Message={Message}",
            request.Name,
            request.Email,
            request.Message);
    }
}
