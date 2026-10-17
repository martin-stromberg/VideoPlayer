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

    /// <summary>
    /// Aufgezeichnete Einträge inklusive Log-Level — für Assertions, bei denen
    /// das Level mitgeprüft werden soll (<c>_messages</c> verwirft es).
    /// </summary>
    /// <value>Queue der protokollierten Einträge mit Level und Nachricht.</value>
    public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();

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
        Entries.Enqueue((logLevel, message));
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
