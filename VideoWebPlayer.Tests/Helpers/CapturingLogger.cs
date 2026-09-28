using Microsoft.Extensions.Logging;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Minimal <see cref="ILogger{T}"/> test double that records whether an error (with an exception) was
/// logged, along with the exception's string representation - shared by tests that only need a presence
/// check (<see cref="ErrorLogged"/>) and tests that want the logged error text for failure messages or
/// assertions (<see cref="LastError"/>).
/// </summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    public bool ErrorLogged { get; private set; }
    public string? LastError { get; private set; }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.Error && exception != null)
        {
            ErrorLogged = true;
            LastError = exception.ToString();
        }
    }
}
