using System.Collections.Concurrent;
using VideoPlayer.Service.Library.Models.Sources;

namespace VideoPlayer.Service.Library.SourceReader.SFtp
{
    public class SFTPConnectionManager
    {
        private object _collectionLock = new object();
        private ConcurrentQueue<SFTPConnection> _free = new ConcurrentQueue<SFTPConnection>();
        private ConcurrentDictionary<long, SFTPConnection> _inUse = new ConcurrentDictionary<long, SFTPConnection>();
        public SFTPConnectionManager(SFTPMediaSource mediaSource)
        {
            MediaSource = mediaSource;
        }
        public int ConcurrentConnections { get; set; } = 1;
        public SFTPMediaSource MediaSource { get; private set; }

        public async Task<SFTPConnection> Connect()
        {
            SFTPConnection connection = null;
            while (connection is null)
            {
                await Task.Delay(100);
                lock (_collectionLock)
                    if (!_free.TryDequeue(out connection))
                        connection = CreateNewConnection();
            }
            _inUse.AddOrUpdate(connection.Id, connection, (key, existing) => existing);
            return connection;
        }
        public void Release(SFTPConnection connection)
        {
            lock (_collectionLock)
            {
                if (!_inUse.Remove(connection.Id, out var inUseConnection))
                    connection.Dispose();
                else if (_free.Count() < ConcurrentConnections)
                    _free.Enqueue(inUseConnection);
                else
                    inUseConnection.Dispose();
            }
        }

        private SFTPConnection CreateNewConnection()
        {
            if (_inUse.Count() >= ConcurrentConnections)
                return null;
            return new SFTPConnection(MediaSource)
            {

            };
        }

        public void Clear()
        {
            MediaSource = null;
            ConcurrentConnections = 0;
        }
    }
}
