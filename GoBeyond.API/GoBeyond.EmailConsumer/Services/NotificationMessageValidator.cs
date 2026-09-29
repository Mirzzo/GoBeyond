using System.Diagnostics.CodeAnalysis;
using GoBeyond.Contracts.Messages;

namespace GoBeyond.EmailConsumer.Services;

/// <summary>
/// Provjerava da li je <see cref="EmailNotificationMessage"/> sa queue-a potpun. <see cref="EmailNotificationMessage"/>
/// deklariše sva polja kao ne-null stringove, ali System.Text.Json to ne provjerava pri deserijalizaciji -
/// poruka bez <c>Subject</c>/<c>Body</c> (npr. ručno objavljena na queue mimo API outbox-a) bi se inače poslala
/// kao prazan email. Takva poruka se tretira isto kao nedostajući primalac: ide u dead-letter queue.
/// </summary>
public static class NotificationMessageValidator
{
    /// <summary>True ako poruka ima sve što je potrebno da se stvarno pošalje email.</summary>
    public static bool IsValid([NotNullWhen(true)] EmailNotificationMessage? message) =>
        message is not null
        && message.MessageId > 0
        && !string.IsNullOrWhiteSpace(message.RecipientEmail)
        && !string.IsNullOrWhiteSpace(message.Subject)
        && !string.IsNullOrWhiteSpace(message.Body)
        && !string.IsNullOrWhiteSpace(message.EventType);
}
