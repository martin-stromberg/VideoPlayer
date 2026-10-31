namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Seekbarer Fake-Quell-Stream, dessen <see cref="Stream.Length"/> größer ist als die
/// tatsächlich gelieferten Bytes — provoziert den <c>Truncated</c>-Pfad der Eingabe-Pumpe,
/// der eine mittendrin abbrechende Quelle (SFTP-Leseabbruch, Teildatei) modelliert.
/// </summary>
internal sealed class TruncatingReadStream : Stream
{
    private readonly byte[] _payload;
    private int _position;

    /// <summary>
    /// Erzeugt einen Stream, der <paramref name="payload"/> liefert, aber
    /// <paramref name="declaredLength"/> als Länge meldet.
    /// </summary>
    public TruncatingReadStream(byte[] payload, long declaredLength)
    {
        _payload = payload;
        Length = declaredLength;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length { get; }

    public override long Position
    {
        get => _position;
        set => _position = (int)value;
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var remaining = _payload.Length - _position;
        if (remaining <= 0)
            return 0;
        var n = Math.Min(count, remaining);
        Array.Copy(_payload, _position, buffer, offset, n);
        _position += n;
        return n;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var remaining = _payload.Length - _position;
        if (remaining <= 0)
            return new ValueTask<int>(0);
        var n = Math.Min(buffer.Length, remaining);
        _payload.AsMemory(_position, n).CopyTo(buffer);
        _position += n;
        return new ValueTask<int>(n);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => Task.FromResult(Read(buffer, offset, count));

    public override long Seek(long offset, SeekOrigin origin)
        => Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => Length + offset,
            _ => Position
        };

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
