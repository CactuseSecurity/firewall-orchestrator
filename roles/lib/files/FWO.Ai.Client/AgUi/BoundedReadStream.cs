namespace FWO.Ai.Client.AgUi;

/// <summary>
/// Read-only stream passing on at most a given number of bytes of another stream. The SSE parser buffers each event
/// until its end, so without a bound a broken or hostile response could make it buffer without limit.
/// </summary>
internal sealed class BoundedReadStream : Stream
{
    private readonly Stream innerStream;
    private readonly long maxBytes;
    private long bytesRead;

    /// <summary>
    /// Wraps a stream, which the caller disposes.
    /// </summary>
    /// <param name="innerStream">Stream to read from</param>
    /// <param name="maxBytes">Maximum number of bytes to read</param>
    internal BoundedReadStream(Stream innerStream, long maxBytes)
    {
        this.innerStream = innerStream;
        this.maxBytes = maxBytes;
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    /// <exception cref="AgentClientException">The stream exceeds the maximum number of bytes.</exception>
    public override int Read(byte[] buffer, int offset, int count)
    {
        return Count(innerStream.Read(buffer, offset, count));
    }

    /// <inheritdoc/>
    /// <exception cref="AgentClientException">The stream exceeds the maximum number of bytes.</exception>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return Count(await innerStream.ReadAsync(buffer, cancellationToken));
    }

    /// <inheritdoc/>
    /// <exception cref="AgentClientException">The stream exceeds the maximum number of bytes.</exception>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        // nothing to flush on a read-only stream
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Counts the bytes read against the limit.
    /// </summary>
    /// <exception cref="AgentClientException">The stream exceeds the maximum number of bytes.</exception>
    private int Count(int readBytes)
    {
        bytesRead += readBytes;
        if (bytesRead > maxBytes)
        {
            throw AgUiFailures.Protocol($"The response of the run is larger than {maxBytes} bytes.");
        }
        return readBytes;
    }
}
