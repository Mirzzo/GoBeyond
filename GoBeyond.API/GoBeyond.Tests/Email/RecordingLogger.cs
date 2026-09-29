using Microsoft.Extensions.Logging;

namespace GoBeyond.Tests.Email;

/// <summary>Minimalan <see cref="ILogger"/> koji pamti formatirane poruke, za provjeru log teksta u testovima.</summary>
internal sealed class RecordingLogger : ILogger
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Messages.Add(formatter(state, exception));
}
