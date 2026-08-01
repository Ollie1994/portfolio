using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Portfolio.Shared;
using Portfolio.Shared.Contact;

namespace Portfolio.Client.Services;

public enum ContactSendStatus
{
    Sent,
    Invalid,

    /// <summary>Network failure, or the API returned something unexpected.</summary>
    Failed
}

public sealed record ContactSendResult(ContactSendStatus Status, IReadOnlyList<ValidationError> Errors)
{
    public static ContactSendResult Sent { get; } = new(ContactSendStatus.Sent, []);
    public static ContactSendResult Failed { get; } = new(ContactSendStatus.Failed, []);
    public static ContactSendResult Invalid(IReadOnlyList<ValidationError> errors) => new(ContactSendStatus.Invalid, errors);
}

/// <summary>
/// The only place the contact endpoint is called. Components talk to this rather
/// than to <see cref="HttpClient"/>, so deserialisation and failure handling
/// exist once instead of in every component that needs them.
/// </summary>
public sealed class ContactApiClient
{
    private readonly HttpClient _http;

    public ContactApiClient(HttpClient http) => _http = http;

    public async Task<ContactSendResult> SendAsync(ContactRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Relative URL - the origin differs between local, PR previews and production.
            var response = await _http.PostAsJsonAsync("api/contact", request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return ContactSendResult.Sent;
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var problem = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken);
                return ContactSendResult.Invalid(problem?.Errors ?? []);
            }

            return ContactSendResult.Failed;
        }
        catch (HttpRequestException)
        {
            return ContactSendResult.Failed;
        }
        catch (JsonException)
        {
            return ContactSendResult.Failed;
        }
        catch (TaskCanceledException)
        {
            // Covers both a timeout and the caller cancelling.
            return ContactSendResult.Failed;
        }
    }

    private sealed record ErrorResponse(IReadOnlyList<ValidationError>? Errors);
}
