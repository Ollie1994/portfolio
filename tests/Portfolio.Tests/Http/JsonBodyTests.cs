using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Api.Http;
using Portfolio.Api.Services;
using Portfolio.Shared.Contact;

namespace Portfolio.Tests.Http;

/// <summary>
/// The request body limit is a security boundary: it is what stops an anonymous
/// caller making the function buffer an arbitrarily large body.
/// </summary>
public class JsonBodyTests
{
    private const string ValidJson = """{"name":"Ada","email":"ada@example.com","message":"Hello, I would like to talk."}""";

    private static Task<JsonBodyResult<ContactRequest>> ReadAsync(Stream body, long? declaredLength, long limit) =>
        JsonBody.ReadAsync<ContactRequest>(body, declaredLength, limit, CancellationToken.None);

    private static MemoryStream Utf8(string text) => new(Encoding.UTF8.GetBytes(text));

    private static int ByteCount(string text) => Encoding.UTF8.GetByteCount(text);

    [Fact]
    public async Task UnderTheLimit_IsRead()
    {
        var result = await ReadAsync(Utf8(ValidJson), ByteCount(ValidJson), limit: 1024);

        Assert.Equal(JsonBodyStatus.Read, result.Status);
        Assert.Equal("Ada", result.Value?.Name);
    }

    [Fact]
    public async Task ExactlyAtTheLimit_IsRead()
    {
        var result = await ReadAsync(Utf8(ValidJson), declaredLength: null, limit: ByteCount(ValidJson));

        Assert.Equal(JsonBodyStatus.Read, result.Status);
    }

    [Fact]
    public async Task OneByteOverTheLimit_WithNoDeclaredLength_IsTooLarge()
    {
        // A chunked body has no Content-Length, so only the bounded read catches it.
        var result = await ReadAsync(Utf8(ValidJson), declaredLength: null, limit: ByteCount(ValidJson) - 1);

        Assert.Equal(JsonBodyStatus.TooLarge, result.Status);
    }

    [Fact]
    public async Task DeclaredLengthOverTheLimit_IsTooLargeWithoutReading()
    {
        var result = await ReadAsync(new UnreadableStream(), declaredLength: 2048, limit: 1024);

        Assert.Equal(JsonBodyStatus.TooLarge, result.Status);
    }

    [Fact]
    public async Task DeclaredLengthThatUnderstatesTheBody_IsStillTooLarge()
    {
        var body = $$"""{"message":"{{new string('a', 5000)}}"}""";

        var result = await ReadAsync(Utf8(body), declaredLength: 10, limit: 1024);

        Assert.Equal(JsonBodyStatus.TooLarge, result.Status);
    }

    [Fact]
    public async Task OversizedBody_IsNeverReadMoreThanOneBytePastTheLimit()
    {
        // One long string token, so the serializer keeps asking for whole buffers.
        // Checking the total only after each read let one read pull 16 KB or more
        // past the limit before the body was refused.
        const int limit = 1024;
        var body = new CountingStream(Utf8($$"""{"message":"{{new string('a', 200_000)}}"}"""));

        var result = await ReadAsync(body, declaredLength: null, limit);

        Assert.Equal(JsonBodyStatus.TooLarge, result.Status);
        Assert.InRange(body.BytesRead, limit + 1, limit + 1);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("")]
    [InlineData("[1, 2, 3]")]
    public async Task NotJsonForTheType_IsMalformed(string body)
    {
        var result = await ReadAsync(Utf8(body), ByteCount(body), limit: 1024);

        Assert.Equal(JsonBodyStatus.Malformed, result.Status);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("application/json; charset=utf-8")]
    [InlineData("Application/JSON")]
    [InlineData("application/json;charset=utf-8")]
    [InlineData("application/json ; charset=utf-8")]
    public void JsonContentType_IsAccepted(string contentType)
    {
        Assert.True(JsonBody.IsJsonContentType(contentType));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("text/plain")]
    [InlineData("application/x-www-form-urlencoded")]
    [InlineData("application/jsonx")]
    [InlineData("application/json-seq")]
    [InlineData("application/json/extra")]
    [InlineData("application/json;")]
    [InlineData("application/json; charset=\"utf-8")]
    public void OtherContentType_IsNotJson(string? contentType)
    {
        // Form-encoded and plain text are what a cross-site HTML form can send without
        // a preflight, so those must never pass. The others were let through by the old
        // prefix match: the last two are malformed headers, which no browser sends and
        // which are refused on purpose rather than guessed at.
        Assert.False(JsonBody.IsJsonContentType(contentType));
    }

    [Fact]
    public async Task JsonNull_IsReadWithNoValue()
    {
        var result = await ReadAsync(Utf8("null"), 4, limit: 1024);

        Assert.Equal(JsonBodyStatus.Read, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task FieldsSentAsNull_AreRejectedByValidationWithoutThrowing()
    {
        // ContactRequest's strings are non-nullable, but JSON can still set them to
        // null. Nothing may touch them before the validator, which has to cope.
        const string body = """{"name":null,"email":null,"message":null,"website":null}""";

        var read = await ReadAsync(Utf8(body), ByteCount(body), limit: 1024);
        Assert.Equal(JsonBodyStatus.Read, read.Status);
        var request = Assert.IsType<ContactRequest>(read.Value);

        var submitted = new ContactService(NullLogger<ContactService>.Instance).Submit(request);

        Assert.Equal(ContactOutcome.Invalid, submitted.Outcome);
        Assert.Equal(3, submitted.Validation.Errors.Count);
    }

    /// <summary>Counts the bytes actually taken from the underlying body.</summary>
    private sealed class CountingStream(Stream inner) : Stream
    {
        public long BytesRead { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => BytesRead; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Count(await inner.ReadAsync(buffer, cancellationToken));

        private int Count(int bytesRead)
        {
            BytesRead += bytesRead;
            return bytesRead;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Fails the test if anything reads from it.</summary>
    private sealed class UnreadableStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("The body was read.");
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => throw new InvalidOperationException("The body was read.");
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
