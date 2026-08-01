using Portfolio.Shared.Contact;

namespace Portfolio.Tests.Contact;

public class ContactValidatorTests
{
    private static ContactRequest Valid(
        string? name = null,
        string? email = null,
        string? message = null) => new()
        {
            Name = name ?? "Ada Lovelace",
            Email = email ?? "ada@example.com",
            Message = message ?? "Hello, I would like to talk about a role."
        };

    [Fact]
    public void Validate_CompleteRequest_IsValid()
    {
        var result = ContactValidator.Validate(Valid());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankName_ReportsNameError(string name)
    {
        var result = ContactValidator.Validate(Valid(name: name));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == nameof(ContactRequest.Name));
    }

    [Fact]
    public void Validate_NameOverMaxLength_ReportsNameError()
    {
        var name = new string('a', ContactLimits.NameMaxLength + 1);

        var result = ContactValidator.Validate(Valid(name: name));

        Assert.Contains(result.Errors, e => e.Field == nameof(ContactRequest.Name));
    }

    [Fact]
    public void Validate_NameAtMaxLength_IsValid()
    {
        var name = new string('a', ContactLimits.NameMaxLength);

        var result = ContactValidator.Validate(Valid(name: name));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing@tld")]
    [InlineData("@example.com")]
    [InlineData("two@at@example.com")]
    [InlineData("has space@example.com")]
    [InlineData("trailing@example.")]
    public void Validate_MalformedEmail_ReportsEmailError(string email)
    {
        var result = ContactValidator.Validate(Valid(email: email));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == nameof(ContactRequest.Email));
    }

    [Theory]
    [InlineData("ada@example.com")]
    [InlineData("ada.lovelace+jobs@sub.example.co.uk")]
    public void Validate_WellFormedEmail_IsValid(string email)
    {
        var result = ContactValidator.Validate(Valid(email: email));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_MessageBelowMinimumLength_ReportsMessageError()
    {
        var result = ContactValidator.Validate(Valid(message: "too short"));

        Assert.Contains(result.Errors, e => e.Field == nameof(ContactRequest.Message));
    }

    [Fact]
    public void Validate_MessageOverMaxLength_ReportsMessageError()
    {
        var message = new string('a', ContactLimits.MessageMaxLength + 1);

        var result = ContactValidator.Validate(Valid(message: message));

        Assert.Contains(result.Errors, e => e.Field == nameof(ContactRequest.Message));
    }

    [Fact]
    public void Validate_WhitespacePaddedShortMessage_ReportsMessageError()
    {
        // Padding must not be a way past the minimum length.
        var result = ContactValidator.Validate(Valid(message: "  hi   " + new string(' ', 40)));

        Assert.Contains(result.Errors, e => e.Field == nameof(ContactRequest.Message));
    }

    // Email header injection: a line break in a single-line field would let an
    // attacker append headers if these values are ever composed into an email.
    [Theory]
    [InlineData("Ada\r\nBcc: victim@example.com")]
    [InlineData("Ada\nBcc: victim@example.com")]
    [InlineData("Ada\rBcc: victim@example.com")]
    public void Validate_LineBreakInName_IsRejected(string name)
    {
        var result = ContactValidator.Validate(Valid(name: name));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == nameof(ContactRequest.Name));
    }

    [Theory]
    [InlineData("ada@example.com\r\nBcc: victim@example.com")]
    [InlineData("ada@example.com\nBcc: victim@example.com")]
    public void Validate_LineBreakInEmail_IsRejected(string email)
    {
        var result = ContactValidator.Validate(Valid(email: email));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == nameof(ContactRequest.Email));
    }

    [Fact]
    public void Validate_MultipleBadFields_ReportsAllOfThem()
    {
        var result = ContactValidator.Validate(new ContactRequest
        {
            Name = "",
            Email = "nope",
            Message = "x"
        });

        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void Validate_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ContactValidator.Validate(null!));
    }
}
