using FWO.Ai.Client;
using FWO.Ai.Client.AgUi;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests the stream bounding the response of a run: it passes reads on up to the limit, refuses more, and supports
/// reading only.
/// </summary>
[TestFixture]
[Parallelizable]
internal class AgUiBoundedReadStreamTest
{
    private const int kLimit = 8;
    private const int kBufferSize = 16;

    /// <summary>
    /// Verifies that the bytes up to the limit are read, synchronously and asynchronously.
    /// </summary>
    [Test]
    public async Task Read_UpToLimit_ReturnsBytes()
    {
        byte[] content = new byte[kLimit];
        using BoundedReadStream syncStream = new(new MemoryStream(content), kLimit);
        using BoundedReadStream asyncStream = new(new MemoryStream(content), kLimit);
        byte[] buffer = new byte[kBufferSize];

        int syncBytes = syncStream.Read(buffer, 0, buffer.Length);
        int asyncBytes = await asyncStream.ReadAsync(buffer, 0, buffer.Length);

        Assert.That(syncBytes, Is.EqualTo(kLimit));
        Assert.That(asyncBytes, Is.EqualTo(kLimit));
        Assert.That(syncStream.Read(buffer, 0, buffer.Length), Is.Zero);
    }

    /// <summary>
    /// Verifies that reading beyond the limit is a protocol error.
    /// </summary>
    [Test]
    public void Read_BeyondLimit_ThrowsProtocolError()
    {
        byte[] content = new byte[kBufferSize];
        using BoundedReadStream stream = new(new MemoryStream(content), kLimit);
        byte[] buffer = new byte[kBufferSize];

        AgentClientException? exception = Assert.Throws<AgentClientException>(() => stream.ReadExactly(buffer));

        Assert.That(exception?.Failure, Is.EqualTo(AgentClientFailure.ProtocolError));
    }

    /// <summary>
    /// Verifies that the stream can only be read.
    /// </summary>
    [Test]
    public void Stream_SupportsReadingOnly()
    {
        using BoundedReadStream stream = new(new MemoryStream(), kLimit);
        byte[] buffer = new byte[kBufferSize];

        stream.Flush();

        Assert.That(stream.CanRead, Is.True);
        Assert.That(stream.CanSeek, Is.False);
        Assert.That(stream.CanWrite, Is.False);
        Assert.Throws<NotSupportedException>(() => _ = stream.Length);
        Assert.Throws<NotSupportedException>(() => _ = stream.Position);
        Assert.Throws<NotSupportedException>(() => stream.Position = 0);
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
        Assert.Throws<NotSupportedException>(() => stream.Write(buffer, 0, buffer.Length));
    }
}
