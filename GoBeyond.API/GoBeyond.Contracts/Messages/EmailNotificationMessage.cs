namespace GoBeyond.Contracts.Messages;

/// <summary>Poruka na RabbitMQ queue-u (<c>gobeyond.notifications</c>) - jedan email koji treba poslati.</summary>
/// <param name="MessageId">
/// Id reda u OutboxMessages. NIJE globalno jedinstven kroz vrijeme: restartuje se od 1 kad se baza koja ga je
/// proizvela obriše i ponovo napravi (svježa dev/test baza, druga instalacija). Zato se NE koristi sam kao
/// idempotencijski ključ kod EmailConsumer-a - vidi <see cref="CreatedAtTicks"/> i
/// <c>GoBeyond.EmailConsumer.Services.EmailIdempotencyKey</c>.
/// </param>
/// <param name="CreatedAtTicks">
/// <c>OutboxMessage.CreatedAt.Ticks</c> (UTC) u trenutku kad je API upisao red u outbox (popunjava
/// <c>OutboxDispatcher</c>). Zajedno sa <see cref="MessageId"/> i sadržajem poruke čini idempotencijski ključ
/// (<c>GoBeyond.EmailConsumer.Services.EmailIdempotencyKey</c>): pravi redelivery iste poruke sa brokera ima
/// identičan MessageId+CreatedAtTicks+sadržaj, dok drugačija poruka koja slučajno dobije isti MessageId nakon
/// resetovanja baze skoro sigurno ima drugačiji CreatedAtTicks (pa se ne tretira kao već poslana).
/// Podrazumijevano 0 za poruke serijalizovane prije ovog polja (stare u letu/DLQ) - i dalje se sigurno
/// deserijalizuju, samo bez zaštite od reuse-a Id-a nakon reseta baze za TU konkretnu poruku.
/// </param>
public sealed record EmailNotificationMessage(
    int MessageId, string EventType, string RecipientEmail, string Subject, string Body, long CreatedAtTicks = 0);
