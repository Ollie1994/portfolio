namespace Portfolio.Shared.Contact;

/// <summary>
/// A message submitted through the contact form. Shared by the client and the
/// API so both compile against one definition of the contract.
/// </summary>
public sealed record ContactRequest
{
    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Honeypot. Hidden from people with CSS; bots that fill every field they find
    /// fill it too. A non-empty value means the submission is discarded, while the
    /// sender is told it was sent. A browser or password manager can still fill it
    /// for a person, whose message is then lost: the form's field is named so
    /// autofill has nothing to match, and carries each password manager's opt-out
    /// (see ContactForm.razor). The property keeps its name as part of the API
    /// contract; nothing a person sees uses it.
    /// </summary>
    public string? Website { get; init; }
}
