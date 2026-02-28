using System;
using System.IO;
using Renci.SshNet;

namespace VideoWebPlayer.Services
{
    /// <summary>
    /// Wraps an SFTP stream and disposes the underlying client when finished.
    /// </summary>
    public class SftpStreamWrapper : Stream
    {
        private readonly Stream _innerStream;
        private readonly SftpClient _client;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SftpStreamWrapper"/> class.
        /// </summary>
        /// <param name="innerStream">The underlying stream.</param>
        /// <param name="client">The SFTP client to dispose.</param>
        public SftpStreamWrapper(Stream innerStream, SftpClient client)
        {
            _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <inheritdoc />
        public override bool CanRead => _innerStream.CanRead;
        /// <inheritdoc />
        public override bool CanSeek => _innerStream.CanSeek;
        /// <inheritdoc />
        public override bool CanWrite => _innerStream.CanWrite;
        /// <inheritdoc />
        public override long Length => _innerStream.Length;
        /// <inheritdoc />
        public override long Position
        {
            get => _innerStream.Position;
            set => _innerStream.Position = value;
        }

        /// <inheritdoc />
        public override void Flush() => _innerStream.Flush();
        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => _innerStream.Read(buffer, offset, count);
        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
        /// <inheritdoc />
        public override void SetLength(long value) => _innerStream.SetLength(value);
        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _innerStream.Dispose();
                    _client.Dispose();
                }
                _disposed = true;
            }
            base.Dispose(disposing);
        }
    }
}