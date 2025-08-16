using System;
using System.IO;
using Renci.SshNet;

namespace VideoWebPlayer.Services
{
    public class SftpStreamWrapper : Stream
    {
        private readonly Stream _innerStream;
        private readonly SftpClient _client;
        private bool _disposed;

        public SftpStreamWrapper(Stream innerStream, SftpClient client)
        {
            _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => _innerStream.CanSeek;
        public override bool CanWrite => _innerStream.CanWrite;
        public override long Length => _innerStream.Length;
        public override long Position
        {
            get => _innerStream.Position;
            set => _innerStream.Position = value;
        }

        public override void Flush() => _innerStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _innerStream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
        public override void SetLength(long value) => _innerStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);

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