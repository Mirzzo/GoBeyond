namespace GoBeyond.Contracts;

/// <summary>RabbitMQ postavke (sekcija "RabbitMq" u appsettings.Shared.json) - dijele ih API i EmailConsumer.</summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string VirtualHost { get; set; } = "/";

    /// <summary>Queue sa email obavijestima (gobeyond.notifications).</summary>
    public string Queue { get; set; } = string.Empty;

    /// <summary>Queue u koji idu poruke nakon MaxAttempts neuspjelih pokušaja.</summary>
    public string DeadLetterQueue { get; set; } = string.Empty;

    /// <summary>Pauza prije ponovnog pokušaja (konekcija ili slanje).</summary>
    public int RetrySeconds { get; set; }

    /// <summary>Maksimalan broj pokušaja obrade jedne poruke (i u API outbox-u i u consumeru).</summary>
    public int MaxAttempts { get; set; }

    /// <summary>Koliko često OutboxDispatcher provjerava nove poruke.</summary>
    public int PollIntervalSeconds { get; set; }
}
