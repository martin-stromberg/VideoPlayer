using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Library;

namespace VideoPlayer.Service.Log
{
    public class DatabaseLogger: TimerService, ILogger
    {
        public IDisposable BeginScope<TState>(TState state) => null!;
        private ConcurrentQueue<LogEntry> _Queue = new ConcurrentQueue<LogEntry>();
        private IMediaLibrary mediaLibrary;

        public bool IsEnabled(LogLevel logLevel) => true;

        public DatabaseLogger()
            :base(null)
        {
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var entry = new LogEntry(null)
            {
                Timestamp = DateTime.Now,
                Message = formatter(state, exception),
                Level = logLevel,
            };
            _Queue.Enqueue(entry);
        }

        protected override Task ExecuteTimerAsync()
        {
            while (_Queue.TryDequeue(out var entry))
                if (mediaLibrary is not null)
                    mediaLibrary.AddOrUpdateLogEntry(entry);
            return Task.CompletedTask;
        }

        internal void Init(IMediaLibrary mediaLibrary)
        {
            this.mediaLibrary = mediaLibrary;
        }
    }
}
