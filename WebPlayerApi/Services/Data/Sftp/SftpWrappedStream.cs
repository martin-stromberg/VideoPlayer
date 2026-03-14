using Renci.SshNet;

namespace WebPlayerApi.Service.Data.SFtp
{
    public class SftpWrappedStream : Stream
    {
        private readonly Stream _innerStream;
        private SFTPConnection _sftpClient;
        private bool _disposed = false;

        public SftpWrappedStream(Stream innerStream, SFTPConnection sftpClient)
        {
            _innerStream = innerStream;
            _sftpClient = sftpClient;
        }

        public event EventHandler<SFTPConnection> ConnectionClosed;

        public override void Close()
        {
            base.Close();
            Dispose(true);
        }
        public Stream InnerStream { get =>  _innerStream; }
        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                try
                {
                    _innerStream?.Dispose();
                }
                catch (Exception ex)
                {
                }

                try
                {                    
                    if (ConnectionClosed is null)
                    {
                        _sftpClient.Dispose();
                    }
                    else
                    {
                        ConnectionClosed.Invoke(this, _sftpClient);
                    }
                    _sftpClient = null;
                }
                catch (Exception ex)
                {
                }
            }
            _disposed = true;
            base.Dispose(disposing);
        }

        // Delegiere alle anderen Stream-Methoden
        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => _innerStream.CanSeek;
        public override bool CanWrite => _innerStream.CanWrite;
        public override long Length => _innerStream.Length;
        public override long Position { get => _innerStream.Position; set => _innerStream.Position = value; }
        public override void Flush() => _innerStream.Flush();
        public override int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                return _innerStream.Read(buffer, offset, count);
            }
            catch (IOException ex)
            {
                throw;
            }
        }
        public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
        public override void SetLength(long value) => _innerStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
