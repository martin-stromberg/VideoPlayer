namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Fake-Quell-Stream, der <c>payload</c> liefert und beim nächsten Lesezugriff eine
/// <see cref="IOException"/> wirft — modelliert einen Quell-Abbruch mittendrin
/// (z. B. einen SFTP-Verbindungsabbruch während <c>ReadAsync</c>). Mit gesetzter
/// <c>declaredLength</c> ist der Stream seekbar und meldet diese Länge, ohne sie
/// nicht-seekbar (<see cref="Stream.Length"/> wird nie abgefragt).
/// </summary>
internal sealed class FailingReadStream : Stream
{
    private readonly byte[] _payload;
    private readonly long? _declaredLength;
    private int _position;

    /// <summary>
    /// Erzeugt einen Stream, der <paramref name="payload"/> liefert und danach beim Lesen
    /// wirft. <paramref name="declaredLength"/> macht ihn seekbar mit gemeldeter Länge.
    /// </summary>
    public FailingReadStream(byte[] payload, long? declaredLength = null)
    {
        _payload = payload;
        _declaredLength = declaredLength;
    }

    public override bool CanRead => true;
    public override bool CanSeek => _declaredLength.HasValue;
    public override bool CanWrite => false;
    public override long Length => _declaredLength ?? throw new NotSupportedException();

    public override long Position
    {
        get => _position;
        set => _position = (int)value;
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position >= _payload.Length)
            throw new IOException("Verbindung zur Quelle abgebrochen (simuliert).");
        var n = Math.Min(count, _payload.Length - _position);
        Array.Copy(_payload, _position, buffer, offset, n);
        _position += n;
        return n;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_position >= _payload.Length)
            throw new IOException("Verbindung zur Quelle abgebrochen (simuliert).");
        var n = Math.Min(buffer.Length, _payload.Length - _position);
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
