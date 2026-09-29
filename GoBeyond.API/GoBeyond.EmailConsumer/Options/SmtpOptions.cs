namespace GoBeyond.EmailConsumer.Options;

/// <summary>SMTP postavke (sekcija "Smtp" u appsettings.Shared.json; u docker-compose host je Mailpit).</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public bool UseSsl { get; set; }

    /// <summary>
    /// Domene primaoca (npr. "gobeyond.ba") kojima se email NE šalje kroz stvarni SMTP kada host nije Mailpit
    /// (vidi <see cref="Services.RecipientSuppression"/>). Štiti realni SMTP nalog i treće strane od demo/seed
    /// adresa koje nisu prave poštanske adrese pod našom kontrolom. Pokriva i poddomene (npr. "edu.gobeyond.ba"
    /// za konfigurisano "gobeyond.ba").
    /// </summary>
    public string[] SuppressedRecipientDomains { get; set; } = [];

    /// <summary>
    /// Putanja append-only fajla sa idempotencijskim ključevima (<see cref="Services.EmailIdempotencyKey"/>)
    /// poruka koje su već uspješno poslane preko SMTP-a - idempotencija kod redelivery-a sa brokera nakon
    /// pada procesa između SMTP slanja i BasicAck-a (vidi <see cref="Services.SentMessageIdStore"/>). U
    /// docker-compose je postavljena na apsolutnu putanju na imenovanom volume-u (<c>/data/...</c>) da
    /// preživi restart/recreate kontejnera. Prazna vrijednost isključuje provjeru (npr. u testovima).
    /// Relativna vrijednost (podrazumijevana u appsettings.Shared.json, za pokretanje van docker-a) se NIKAD
    /// ne rješava protiv trenutnog radnog direktorija - <see cref="Services.SentMessageIdStore.ResolvePath"/>
    /// je umjesto toga rješava protiv OS temp foldera, da golo <c>dotnet run</c> iz root-a repozitorija ne
    /// upiše fajl u sam repozitorij.
    /// </summary>
    public string SentMessageIdsFilePath { get; set; } = string.Empty;
}
