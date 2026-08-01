using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Api.Services;
using Portfolio.Shared.Contact;

namespace Portfolio.Tests.Contact;

public class ContactServiceTests
{
    // Constructed directly - no Functions host, no HTTP. That is the point of
    // keeping the logic out of the function class.
    private static ContactService CreateService() =>
        new(NullLogger<ContactService>.Instance);

    private static ContactRequest Valid() => new()
    {
        Name = "Ada Lovelace",
        Email = "ada@example.com",
        Message = "Hello, I would like to talk about a role."
    };

    [Fact]
    public void Submit_ValidRequest_IsAccepted()
    {
        var result = CreateService().Submit(Valid());

        Assert.Equal(ContactOutcome.Accepted, result.Outcome);
        Assert.True(result.Validation.IsValid);
    }

    [Fact]
    public void Submit_InvalidRequest_IsInvalidAndCarriesErrors()
    {
        var request = Valid() with { Email = "nope" };

        var result = CreateService().Submit(request);

        Assert.Equal(ContactOutcome.Invalid, result.Outcome);
        Assert.Contains(result.Validation.Errors, e => e.Field == nameof(ContactRequest.Email));
    }

    [Fact]
    public void Submit_HoneypotFilled_IsDiscarded()
    {
        var request = Valid() with { Website = "http://spam.example" };

        var result = CreateService().Submit(request);

        Assert.Equal(ContactOutcome.Discarded, result.Outcome);
    }

    [Fact]
    public void Submit_HoneypotFilledOnOtherwiseInvalidRequest_IsDiscardedNotValidated()
    {
        // The honeypot short-circuits: a bot's submission should cost as little
        // as possible, and reporting validation errors back to it tells it how
        // to try again.
        var request = new ContactRequest
        {
            Name = "",
            Email = "nope",
            Message = "x",
            Website = "http://spam.example"
        };

        var result = CreateService().Submit(request);

        Assert.Equal(ContactOutcome.Discarded, result.Outcome);
        Assert.Empty(result.Validation.Errors);
    }

    [Fact]
    public void Submit_HoneypotWhitespaceOnly_IsTreatedAsEmpty()
    {
        // Browsers can autofill or trim; whitespace is not evidence of a bot.
        var request = Valid() with { Website = "   " };

        var result = CreateService().Submit(request);

        Assert.Equal(ContactOutcome.Accepted, result.Outcome);
    }

    [Fact]
    public void Submit_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CreateService().Submit(null!));
    }
}
