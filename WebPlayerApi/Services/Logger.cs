namespace WebPlayerApi.Services
{
    public class InMemoryLogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Exception { get; set; }
    }

    public class InMemoryLoggerProvider : ILoggerProvider
    {
        private readonly List<InMemoryLogEntry> _logs = new();
        private readonly object _lock = new();
        private const int MaxEntries = 1000;

        public IReadOnlyList<InMemoryLogEntry> Logs
        {
            get
            {
                lock (_lock)
                {
                    return _logs.ToList();
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => new InMemoryLogger(this);

        public void Dispose() { }

        private class InMemoryLogger : ILogger
        {
            private readonly InMemoryLoggerProvider _provider;

            public InMemoryLogger(InMemoryLoggerProvider provider)
            {
                _provider = provider;
            }

            public IDisposable BeginScope<TState>(TState state) => default!;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var entry = new InMemoryLogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = logLevel,
                    Message = formatter(state, exception),
                    Exception = exception?.ToString()
                };

                lock (_provider._lock)
                {
                    _provider._logs.Add(entry);
                    if (_provider._logs.Count > MaxEntries)
                        _provider._logs.RemoveAt(0);
                }
            }
        }
    }

}
