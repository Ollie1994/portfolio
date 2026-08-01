using Portfolio.Shared;

namespace Portfolio.Api.Services;

public enum ContactOutcome
{
    /// <summary>Valid and handed to delivery.</summary>
    Accepted,

    /// <summary>Failed validation. <see cref="ContactSubmissionResult.Validation"/> says why.</summary>
    Invalid,

    /// <summary>
    /// Caught by the honeypot. Reported to the caller as success on purpose —
    /// telling a bot it was detected only helps it adapt.
    /// </summary>
    Discarded
}

public sealed record ContactSubmissionResult(ContactOutcome Outcome, ValidationResult Validation)
{
    public static ContactSubmissionResult Accepted { get; } =
        new(ContactOutcome.Accepted, ValidationResult.Success);

    public static ContactSubmissionResult Discarded { get; } =
        new(ContactOutcome.Discarded, ValidationResult.Success);

    public static ContactSubmissionResult Invalid(ValidationResult validation) =>
        new(ContactOutcome.Invalid, validation);
}
