using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Processor;

namespace VideoPlayer.Service.Log
{
    public class DatabaseLogger: TimerService, ILogger
    {
        public IDisposable BeginScope<TState>(TState state) => null!;
        private ConcurrentQueue<LogEntry> _Queue = new ConcurrentQueue<LogEntry>();
        private IMediaLibrary mediaLibrary;
        private bool _SkipEnqueue = false;

        public bool IsEnabled(LogLevel logLevel) => true;

        public DatabaseLogger(IProcessorCollection processorCollection)
            :base(nameof(DatabaseLogger), processorCollection, null)
        {
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!CheckStop())
                return;
            var entry = new LogEntry(null)
            {
                Timestamp = DateTime.Now,
                Message = formatter(state, exception),
                Level = logLevel,
            };
            _Queue.Enqueue(entry);
        }

        private bool CheckStop()
        {
            if (_Queue.Count > 15000)
            {
                _SkipEnqueue = true;
                return false;
            }

            if (_SkipEnqueue)
                if (_Queue.Count > 10)
                    return false;
                else
                    _SkipEnqueue = false;
            return true;
        }

        protected override void ExecuteTimerSync()
        {
            base.ExecuteTimerSync();
            while (_Queue.TryDequeue(out var entry))
                if (mediaLibrary is not null)
                    mediaLibrary.AddOrUpdateLogEntry(entry);
        }

        internal void Init(IMediaLibrary mediaLibrary, IProcessorCollection processorCollection)
        {
            this.mediaLibrary = mediaLibrary;
            ChangeProcessorCollection(processorCollection);
        }
    }
}
