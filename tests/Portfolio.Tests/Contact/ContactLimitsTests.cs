using System.Net.Http.Json;
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
    public async Task BodyCap_FitsTheLargestBodyTheFormCanSend()
    {
        // Measured, not estimated: this is the client's own serialisation
        // (PostAsJsonAsync uses JsonContent), and its encoder escapes every
        // non-ASCII character as \uXXXX - 6 bytes for one character the form counts
        // as one. So the worst case is every field full of a character like 'ö',
        // not the character count. If the body cap were below this, a legitimate
        // maximum-length message in Swedish would be refused with 413.
        var worstCase = new ContactRequest
        {
            Name = new string('ö', ContactLimits.NameMaxLength),
            Email = new string('ö', ContactLimits.EmailMaxLength),
            Message = new string('ö', ContactLimits.MessageMaxLength),
            Website = string.Empty
        };

        var bytes = await JsonContent.Create(worstCase).ReadAsByteArrayAsync();

        Assert.True(
            bytes.Length <= ContactLimits.MaxRequestBodyBytes,
            $"The largest body the form can send is {bytes.Length} bytes, over the {ContactLimits.MaxRequestBodyBytes}-byte cap.");
    }
}
