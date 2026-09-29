using Microsoft.Extensions.Logging;

namespace GoBeyond.Tests.Email;

/// <summary>Minimalan <see cref="ILogger"/> koji pamti formatirane poruke, za provjeru log teksta u testovima.</summary>
internal class RecordingLogger : ILogger
{
    public List<string> Messages { get; } = [];

    /// <summary>Iste poruke sa nivoom logovanja.</summary>
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        Messages.Add(message);
        Entries.Add((logLevel, message));
    }
}

/// <summary><see cref="RecordingLogger"/> za klase koje traže <see cref="ILogger{TCategoryName}"/>.</summary>
internal sealed class RecordingLogger<T> : RecordingLogger, ILogger<T>;
