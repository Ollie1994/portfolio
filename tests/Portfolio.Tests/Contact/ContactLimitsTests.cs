using Portfolio.Shared.Contact;

namespace Portfolio.Tests.Contact;

/// <summary>
/// The form's maxlength attributes and the server's validation both read these
/// constants, so the limits only need to be consistent with each other — but a
/// careless edit could make a field unfillable or the body cap unreachable.
/// </summary>
public class ContactLimitsTests
{
    [Fact]
    public void MessageMinimum_IsBelowMaximum()
    {
        Assert.True(ContactLimits.MessageMinLength < ContactLimits.MessageMaxLength);
    }

    [Fact]
    public void BodyCap_FitsAFullLengthSubmission()
    {
        // Every field at maximum, as UTF-8, plus JSON structure. If the body cap
        // were below this, a legitimate maximum-length message would be rejected
        // with 413 before validation ever saw it.
        var worstCaseFields =
            ContactLimits.NameMaxLength +
            ContactLimits.EmailMaxLength +
            ContactLimits.MessageMaxLength;

        Assert.True(
            ContactLimits.MaxRequestBodyBytes > worstCaseFields,
            $"Body cap {ContactLimits.MaxRequestBodyBytes} must exceed {worstCaseFields} bytes of field content.");
    }
}
