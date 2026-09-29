using System.Security.Cryptography;
using System.Text;
using GoBeyond.Contracts.Messages;

namespace GoBeyond.EmailConsumer.Services;

/// <summary>
/// Gradi ključ koji <see cref="SentMessageIdStore"/> koristi da prepozna da li je email već poslan.
/// <see cref="EmailNotificationMessage.MessageId"/> (Id reda u OutboxMessages) sam nije dovoljan: restartuje se
/// od 1 kad se baza koja ga je proizvela obriše i ponovo napravi, pa bi trajni store nakon toga nečujno
/// preskočio potpuno drugačiji email koji slučajno dobije isti Id. Zato ključ kombinuje MessageId sa
/// <see cref="EmailNotificationMessage.CreatedAtTicks"/> i SHA-256 hash-em sadržaja (EventType/RecipientEmail/
/// Subject/Body) - pravi redelivery iste poruke ima identičan ključ, dok drugačija poruka nakon reseta baze
/// skoro sigurno ne.
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
