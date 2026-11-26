using System.Text.Json;

namespace Portfolio.Api.Http;

public enum JsonBodyStatus
{
    /// <summary>Parsed. <see cref="JsonBodyResult{T}.Value"/> is null only for a body of
    /// the JSON literal <c>null</c>.</summary>
    Read,

    /// <summary>Not valid JSON for the target type, including an empty body.</summary>
    Malformed,

    /// <summary>Over the limit, by the declared length or by the bytes actually sent.</summary>
    TooLarge
}

/// <summary>The outcome of reading a request body. Expected failures are values here,
/// not exceptions, so the function only maps them to status codes.</summary>
public sealed record JsonBodyResult<T>(JsonBodyStatus Status, T? Value) where T : class;

/// <summary>
/// Reads a JSON request body without ever buffering more than a fixed number of
/// bytes. Takes a plain <see cref="Stream"/> rather than an HttpRequest, so it can be
/// tested without the Functions host.
/// </summary>
public static class JsonBody
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static async Task<JsonBodyResult<T>> ReadAsync<T>(
        Stream body,
        long? declaredLength,
        long limit,
        CancellationToken cancellationToken) where T : class
    {
        ArgumentNullException.ThrowIfNull(body);

        // Rejected on the declared length before a byte is read.
        if (declaredLength > limit)
        {
            return new(JsonBodyStatus.TooLarge, null);
        }

        // And bounded again while reading, since the caller supplies the declared
        // length: it can be wrong, or missing for a chunked body. Static Web Apps
        // currently refuses chunked requests before they reach the function, so in
        // production this is the backstop rather than the check that fires.
        using var limited = new LimitedStream(body, limit);

        try
        {
            var value = await JsonSerializer.DeserializeAsync<T>(limited, Options, cancellationToken);
            return new(JsonBodyStatus.Read, value);
        }
        catch (JsonException)
        {
            return new(JsonBodyStatus.Malformed, null);
        }
        catch (LimitExceededException)
        {
            return new(JsonBodyStatus.TooLarge, null);
        }
    }

    /// <summary>
    /// Thrown only by <see cref="LimitedStream"/>, so catching it cannot mistake some
    /// other failure inside the serializer for an oversized body.
    /// </summary>
    private sealed class LimitExceededException : Exception
    {
        public LimitExceededException()
            : base("Request body exceeded the permitted size.")
        {
        }
    }

    /// <summary>
    /// Read-only wrapper that throws once more than <paramref name="limit"/> bytes
    /// have been read, so deserialising a hostile body stops early rather than
    /// buffering it all.
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
                throw new LimitExceededException();
            }

            return bytesRead;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
