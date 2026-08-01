using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Portfolio.Api.Services;
using Portfolio.Shared.Contact;

namespace Portfolio.Api.Functions;

/// <summary>
/// HTTP adapter for contact submissions. Reads and bounds the request, delegates
/// to <see cref="ContactService"/>, maps the outcome to a status code. No
/// business logic lives here.
/// </summary>
public sealed class ContactFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ContactService _contacts;
    private readonly ILogger<ContactFunction> _logger;

    public ContactFunction(ContactService contacts, ILogger<ContactFunction> logger)
    {
        _contacts = contacts;
        _logger = logger;
    }

    [Function("Contact")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "contact")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        // A browser can submit a plain HTML form cross-site without any consent
        // from us, but only as form-encoded or plain text - it cannot set
        // application/json without a preflight, which same-origin policy governs.
        // Requiring JSON therefore removes trivial cross-site submission.
        if (!IsJsonRequest(req))
        {
            return new StatusCodeResult(StatusCodes.Status415UnsupportedMediaType);
        }

        // Reject an oversized body on the declared length before reading a byte
        // of it. The read below is bounded too, since Content-Length is supplied
        // by the caller and cannot be trusted on its own.
        if (req.ContentLength > ContactLimits.MaxRequestBodyBytes)
        {
            return new StatusCodeResult(StatusCodes.Status413PayloadTooLarge);
        }

        ContactRequest? request;
        try
        {
            request = await ReadRequestAsync(req.Body, cancellationToken);
        }
        catch (JsonException)
        {
            // Expected for malformed input from an anonymous endpoint. Log without
            // the body, and tell the caller nothing about our internals.
            _logger.LogInformation("Contact submission rejected: body was not valid JSON.");
            return new BadRequestObjectResult(new { message = "Request body was not valid JSON." });
        }
        catch (InvalidOperationException)
        {
            return new StatusCodeResult(StatusCodes.Status413PayloadTooLarge);
        }

        if (request is null)
        {
            return new BadRequestObjectResult(new { message = "Request body was empty." });
        }

        var result = _contacts.Submit(request);

        return result.Outcome switch
        {
            // Discarded submissions report success on purpose - see ContactOutcome.
            ContactOutcome.Accepted or ContactOutcome.Discarded => new OkObjectResult(new { message = "Thanks — your message has been sent." }),
            ContactOutcome.Invalid => new BadRequestObjectResult(new { errors = result.Validation.Errors }),
            _ => new StatusCodeResult(StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>
    /// True when the request declares a JSON body. Tolerates parameters such as
    /// "application/json; charset=utf-8", which browsers add.
    /// </summary>
    private static bool IsJsonRequest(HttpRequest req) =>
        req.ContentType is { } contentType &&
        contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);

    private static async Task<ContactRequest?> ReadRequestAsync(Stream body, CancellationToken cancellationToken)
    {
        // Independent of Content-Length: a caller can lie about it, or send a
        // chunked body with none at all.
        using var limited = new LimitedStream(body, ContactLimits.MaxRequestBodyBytes);

        return await JsonSerializer.DeserializeAsync<ContactRequest>(limited, JsonOptions, cancellationToken);
    }

    /// <summary>
    /// Read-only wrapper that throws once more than <paramref name="limit"/> bytes
    /// have been read, so deserialisation of a hostile body stops early rather
    /// than buffering it all.
    /// </summary>
    private sealed class LimitedStream(Stream inner, long limit) : Stream
    {
        private long _read;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _read;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Track(inner.Read(buffer, offset, count));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Track(await inner.ReadAsync(buffer, cancellationToken));

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Track(await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken));

        private int Track(int bytesRead)
        {
            _read += bytesRead;

            if (_read > limit)
            {
                throw new InvalidOperationException("Request body exceeded the permitted size.");
            }

            return bytesRead;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
