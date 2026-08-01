namespace Portfolio.Shared.Contact;

/// <summary>
/// Size limits for contact submissions. Shared so the form's maxlength
/// attributes and the server's validation cannot drift apart, and so there is
/// one place to change them.
/// </summary>
public static class ContactLimits
{
    public const int NameMaxLength = 100;

    /// <summary>Longest address permitted by RFC 5321.</summary>
    public const int EmailMaxLength = 254;

    public const int MessageMinLength = 10;

    public const int MessageMaxLength = 2000;

    /// <summary>
    /// Hard cap on the raw request body, enforced before deserialisation.
    /// Comfortably above a full-length valid submission, and low enough that a
    /// large body cannot be used to burn execution time or the free-tier
    /// invocation budget.
    /// </summary>
    public const int MaxRequestBodyBytes = 16 * 1024;
}
