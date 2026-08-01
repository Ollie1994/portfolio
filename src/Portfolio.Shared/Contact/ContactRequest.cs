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
    /// Honeypot. Hidden from users with CSS, so a human never fills it in;
    /// naive bots fill every field they find. A non-empty value means the
    /// submission is discarded.
    /// </summary>
    public string? Website { get; init; }
}
