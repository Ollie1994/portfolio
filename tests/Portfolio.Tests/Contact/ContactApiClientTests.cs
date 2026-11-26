using System.Net;
using System.Text;
using Portfolio.Client.Services;
using Portfolio.Shared.Contact;

namespace Portfolio.Tests.Contact;

/// <summary>
/// The client turns every response into a result the form can render. A wrong
/// mapping fails quietly: the form shows nothing, or the wrong thing.
/// </summary>
public class ContactApiClientTests
{
    private static readonly ContactRequest Request = new()
    {
        Name = "Ada Lovelace",
        Email = "ada@example.com",
        Message = "Hello, I would like to talk about a role."
    };

    [Fact]
    public async Task Success_IsSent()
    {
        var result = await SendAsync(HttpStatusCode.OK, """{"message":"Thanks"}""");

        Assert.Equal(ContactSendStatus.Sent, result.Status);
    }

    [Fact]
    public async Task BadRequestWithFieldErrors_IsInvalidAndCarriesThem()
    {
        var result = await SendAsync(
            HttpStatusCode.BadRequest,
            """{"errors":[{"field":"Email","message":"That doesn't look like an email address."}]}""");

        Assert.Equal(ContactSendStatus.Invalid, result.Status);
        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(ContactRequest.Email), error.Field);
    }

    [Theory]
    [InlineData("""{"message":"Request body was not valid JSON."}""")]
    [InlineData("""{"errors":[]}""")]
    [InlineData("""{"errors":null}""")]
    public async Task BadRequestWithoutFieldErrors_IsFailed(string body)
    {
        // Invalid with nothing to show beside a field left the form blank.
        var result = await SendAsync(HttpStatusCode.BadRequest, body);

        Assert.Equal(ContactSendStatus.Failed, result.Status);
    }

    [Fact]
    public async Task BadRequestThatIsNotJson_IsFailed()
    {
        var result = await SendAsync(HttpStatusCode.BadRequest, "<html>Bad Request</html>", "text/html");

        Assert.Equal(ContactSendStatus.Failed, result.Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge)]
    [InlineData(HttpStatusCode.UnsupportedMediaType)]
    public async Task OtherFailureStatus_IsFailed(HttpStatusCode status)
    {
        var result = await SendAsync(status, "");

        Assert.Equal(ContactSendStatus.Failed, result.Status);
    }

    [Fact]
    public async Task NetworkFailure_IsFailed()
    {
        var client = CreateClient(new StubHandler(_ => throw new HttpRequestException("offline")));

        var result = await client.SendAsync(Request);

        Assert.Equal(ContactSendStatus.Failed, result.Status);
    }

    private static Task<ContactSendResult> SendAsync(HttpStatusCode status, string body, string mediaType = "application/json") =>
        CreateClient(new StubHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType)
        })).SendAsync(Request);

    private static ContactApiClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") });

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
