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
    /// TODO: no delivery mechanism is wired up yet — the message is written to
    /// the log and nothing else. Until a provider is chosen (Azure Communication
    /// Services, SendGrid, or similar) and Application Insights is enabled on the
    /// Static Web App, submissions are effectively discarded after validation.
    /// The form tells the sender it succeeded, which is only honest once this is
    /// real. See README.
    /// </summary>
    private void Deliver(ContactRequest request)
    {
        _logger.LogInformation(
            "Contact submission accepted from {Name} <{Email}>: {Message}",
            request.Name,
            request.Email,
            request.Message);
    }
}
