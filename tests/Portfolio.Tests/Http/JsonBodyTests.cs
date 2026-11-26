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

    [Theory]
    [InlineData("{not json")]
    [InlineData("")]
    [InlineData("[1, 2, 3]")]
    public async Task NotJsonForTheType_IsMalformed(string body)
    {
        var result = await ReadAsync(Utf8(body), ByteCount(body), limit: 1024);

        Assert.Equal(JsonBodyStatus.Malformed, result.Status);
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
        var submitted = new ContactService(NullLogger<ContactService>.Instance).Submit(read.Value!);

        Assert.Equal(ContactOutcome.Invalid, submitted.Outcome);
        Assert.Equal(3, submitted.Validation.Errors.Count);
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
