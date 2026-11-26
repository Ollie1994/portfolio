using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Portfolio.Api.Http;
using Portfolio.Api.Services;
using Portfolio.Shared.Contact;

namespace Portfolio.Api.Functions;

/// <summary>
/// HTTP adapter for contact submissions. Reads the request within its limits,
/// delegates to <see cref="ContactService"/>, maps the outcome to a status code. No
/// business logic lives here.
/// </summary>
public sealed class ContactFunction
{
    private readonly ContactService _contacts;
    private readonly ILogger<ContactFunction> _logger;

    public ContactFunction(ContactService contacts, ILogger<ContactFunction> logger)
    {
        _contacts = contacts;
        _logger = logger;
    }

    [Function("Contact")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "contact")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        // A browser can submit a plain HTML form cross-site without any consent
        // from us, but only as form-encoded or plain text - it cannot set
        // application/json without a preflight, which same-origin policy governs.
        // Requiring JSON therefore removes trivial cross-site submission.
        if (!IsJsonRequest(req))
        {
            return new StatusCodeResult(StatusCodes.Status415UnsupportedMediaType);
        }

        var body = await JsonBody.ReadAsync<ContactRequest>(
            req.Body, req.ContentLength, ContactLimits.MaxRequestBodyBytes, cancellationToken);

        switch (body.Status)
        {
            case JsonBodyStatus.TooLarge:
                return new StatusCodeResult(StatusCodes.Status413PayloadTooLarge);

            case JsonBodyStatus.Malformed:
                // Expected for malformed input from an anonymous endpoint. Logged
                // without the body, and the caller learns nothing about our internals.
                _logger.LogInformation("Contact submission rejected: body was not valid JSON.");
                return new BadRequestObjectResult(new { message = "Request body was not valid JSON." });
        }

        // Only the JSON literal null gets here: an empty body is Malformed above.
        if (body.Value is not { } request)
        {
            return new BadRequestObjectResult(new { message = "Request body must be a JSON object." });
        }

        var result = _contacts.Submit(request);

        return result.Outcome switch
        {
            // Discarded submissions report success on purpose - see ContactOutcome.
            ContactOutcome.Accepted or ContactOutcome.Discarded => new OkObjectResult(new { message = "Thanks — your message has been sent." }),
            ContactOutcome.Invalid => new BadRequestObjectResult(new { errors = result.Validation.Errors }),
            _ => new StatusCodeResult(StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>
    /// True when the request declares a JSON body. Tolerates parameters such as
    /// "application/json; charset=utf-8", which browsers add.
    /// </summary>
    private static bool IsJsonRequest(HttpRequest req) =>
        req.ContentType is { } contentType &&
        contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);
}
