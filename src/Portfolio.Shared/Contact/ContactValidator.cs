namespace Portfolio.Shared.Contact;

/// <summary>
/// Validation rules for a contact submission.
///
/// Deliberately a pure function over the request with no I/O and no framework
/// dependencies: the client runs it to give immediate feedback, and the API
/// runs it again as the authoritative check. The client's copy is a
/// convenience — it can be bypassed entirely by calling the API directly, so
/// the server result is the one that counts.
/// </summary>
public static class ContactValidator
{
    public static ValidationResult Validate(ContactRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<ValidationError>();

        ValidateName(request.Name, errors);
        ValidateEmail(request.Email, errors);
        ValidateMessage(request.Message, errors);

        return ValidationResult.Failure(errors);
    }

    private static void ValidateName(string name, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new ValidationError(nameof(ContactRequest.Name), "Please tell me your name."));
        }
        else if (name.Length > ContactLimits.NameMaxLength)
        {
            errors.Add(new ValidationError(
                nameof(ContactRequest.Name),
                $"Name must be {ContactLimits.NameMaxLength} characters or fewer."));
        }
        else if (ContainsLineBreak(name))
        {
            errors.Add(new ValidationError(nameof(ContactRequest.Name), "Name must be a single line."));
        }
    }

    private static void ValidateEmail(string email, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            errors.Add(new ValidationError(nameof(ContactRequest.Email), "Please give an email address so I can reply."));
            return;
        }

        if (email.Length > ContactLimits.EmailMaxLength)
        {
            errors.Add(new ValidationError(
                nameof(ContactRequest.Email),
                $"Email must be {ContactLimits.EmailMaxLength} characters or fewer."));
            return;
        }

        if (ContainsLineBreak(email))
        {
            errors.Add(new ValidationError(nameof(ContactRequest.Email), "Email must be a single line."));
            return;
        }

        if (!LooksLikeEmail(email))
        {
            errors.Add(new ValidationError(nameof(ContactRequest.Email), "That doesn't look like an email address."));
        }
    }

    private static void ValidateMessage(string message, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            errors.Add(new ValidationError(nameof(ContactRequest.Message), "The message is empty."));
        }
        else if (message.Trim().Length < ContactLimits.MessageMinLength)
        {
            errors.Add(new ValidationError(
                nameof(ContactRequest.Message),
                $"Please write at least {ContactLimits.MessageMinLength} characters."));
        }
        else if (message.Length > ContactLimits.MessageMaxLength)
        {
            errors.Add(new ValidationError(
                nameof(ContactRequest.Message),
                $"Message must be {ContactLimits.MessageMaxLength} characters or fewer."));
        }
    }

    /// <summary>
    /// Carriage returns and newlines are rejected in single-line fields. If these
    /// values are ever composed into an email, a line break in the name or address
    /// lets an attacker inject additional headers and use the endpoint as a relay.
    /// Rejecting them at the boundary removes the class of bug regardless of what
    /// the delivery mechanism turns out to be.
    /// </summary>
    private static bool ContainsLineBreak(string value) =>
        value.Contains('\r', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal);

    /// <summary>
    /// Intentionally permissive. Full RFC 5322 validation by regular expression
    /// is a well-known way to reject valid addresses; the only real proof an
    /// address works is sending to it. This catches typos and obvious rubbish.
    /// </summary>
    private static bool LooksLikeEmail(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);

        // Needs something before the @, and a dot with something either side after it.
        if (at <= 0 || at == email.Length - 1)
        {
            return false;
        }

        var domain = email[(at + 1)..];

        // A second @ makes it ambiguous; reject rather than guess.
        if (domain.Contains('@', StringComparison.Ordinal))
        {
            return false;
        }

        var dot = domain.IndexOf('.', StringComparison.Ordinal);

        return dot > 0 && dot < domain.Length - 1 && !email.Contains(' ', StringComparison.Ordinal);
    }
}
