using System.Security.Cryptography;
using System.Text;
using GoBeyond.Contracts.Messages;

namespace GoBeyond.EmailConsumer.Services;

/// <summary>
/// Gradi ključ koji <see cref="SentMessageIdStore"/> koristi da prepozna da li je email već jednom uspješno
/// poslan (review defekt/majorna primjedba nad prethodnom verzijom BG-06 fixa).
///
/// Ključ NIJE samo <see cref="EmailNotificationMessage.MessageId"/> (Id reda u OutboxMessages) - taj Id se
/// restartuje od 1 kad se baza koja ga je proizvela obriše i ponovo napravi (svježa dev/test baza, docker
/// volume koji preživi reset baze, druga instalacija...). Da je ključ samo taj Id, trajni store bi nakon
/// resetovanja baze i dalje "pamtio" stare Id-eve i potpuno RAZLIČIT, nov email koji slučajno dobije isti Id
/// bi bio nečujno preskočen (acked bez slanja) - gori ishod (gubitak emaila) od rijetkog duplikata koji je
/// BG-06 fix trebao spriječiti.
///
/// Zato ključ kombinuje MessageId sa <see cref="EmailNotificationMessage.CreatedAtTicks"/> (vrijeme upisa u
/// outbox, popunjava ga OutboxDispatcher) i SHA-256 hash-em stvarnog sadržaja (EventType/RecipientEmail/
/// Subject/Body). Pravi redelivery iste poruke sa brokera (isti bajtovi, samo isporučeni dvaput) ima identičan
/// MessageId+CreatedAtTicks+sadržaj -> isti ključ -> prepoznaje se i ne šalje ponovo. Različita poruka koja
/// slučajno dobije isti MessageId nakon reseta baze skoro sigurno ima drugačiji CreatedAtTicks (i/ili sadržaj)
/// -> drugačiji ključ -> šalje se normalno.
/// </summary>
public static class EmailIdempotencyKey
{
    /// <summary>Idempotencijski ključ za <paramref name="message"/> - vidi napomenu na klasi.</summary>
    public static string For(EmailNotificationMessage message)
    {
        var content = string.Join('\u0001', message.EventType, message.RecipientEmail, message.Subject, message.Body);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        return $"{message.MessageId}:{message.CreatedAtTicks}:{hash}";
    }
}
