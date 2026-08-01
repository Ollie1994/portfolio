namespace Portfolio.Shared;

/// <summary>A single rejected field and why.</summary>
/// <param name="Field">Name of the offending property, matching the DTO.</param>
/// <param name="Message">Message safe to show to the person who submitted it.</param>
public sealed record ValidationError(string Field, string Message);

/// <summary>
/// The result of validating input. Expected failures are return values rather
/// than exceptions — see the error handling rules in CLAUDE.md. This is the one
/// shape used across the solution; don't introduce a second one.
/// </summary>
public sealed record ValidationResult
{
    private static readonly ValidationError[] None = [];

    private ValidationResult(IReadOnlyList<ValidationError> errors) => Errors = errors;

    public IReadOnlyList<ValidationError> Errors { get; }

    public bool IsValid => Errors.Count == 0;

    public static ValidationResult Success { get; } = new(None);

    public static ValidationResult Failure(IReadOnlyList<ValidationError> errors) =>
        errors.Count == 0 ? Success : new ValidationResult(errors);
}
