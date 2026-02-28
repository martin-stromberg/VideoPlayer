using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace VideoWebPlayer.Tests.Helpers;

public sealed class ListLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<string> _messages;

    public ListLogger(ConcurrentQueue<string> messages)
    {
        _messages = messages;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        if (exception is not null)
        {
            message = $"{message}{Environment.NewLine}{exception}";
        }

        _messages.Enqueue(message);
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
