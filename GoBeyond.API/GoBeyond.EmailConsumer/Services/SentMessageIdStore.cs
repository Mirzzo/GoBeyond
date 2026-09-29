using GoBeyond.EmailConsumer.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GoBeyond.EmailConsumer.Services;

/// <summary>
/// Durable idempotency "brana" protiv duplog slanja emaila nakon pada procesa između uspješnog SMTP slanja i
/// BasicAck-a: RabbitMQ isporučuje poruku ponovo (at-least-once), pa bi se bez ovoga isti email poslao dvaput
/// (vidi BG-06). Pamti ključeve (<see cref="EmailIdempotencyKey"/> - NE samo <c>EmailNotificationMessage.MessageId</c>,
/// vidi tu klasu zašto) uspješno poslanih poruka u append-only tekstualnom fajlu
/// (<see cref="SmtpOptions.SentMessageIdsFilePath"/>); u docker-compose je taj fajl na imenovanom volume-u pa
/// preživi restart kontejnera. Provjera (<see cref="WasSent"/>) se radi PRIJE slanja, a upis
/// (<see cref="MarkSent"/>) ODMAH NAKON uspješnog slanja i PRIJE ack-a - ako broker dostavi istu poruku ponovo,
/// consumer je prepozna i samo je ack-uje bez ponovnog slanja.
///
/// Fajl je namjerno jednostavan (bez baze/zaključavanja): jedini pisac je ovaj proces, a RabbitMQ prefetch=1
/// garantuje da se poruke obrađuju jedna po jedna (nema konkurentnog pristupa unutar procesa). Veličina je
/// ograničena na zadnjih <see cref="MaxEntries"/> ključeva - fajl se periodično sažme (rewrite), umjesto da
/// raste beskonačno.
///
/// BOOKKEEPING GREŠKE NIKAD NE SMIJU IZGLEDATI KAO NEUSPJELO SLANJE (review defekt): <see cref="Load"/> i
/// <see cref="MarkSent"/> hvataju svoje IO greške (zaključan/nedostupan fajl - npr. repo je u OneDrive folderu
/// koji povremeno zaključava fajlove tokom sinhronizacije) i samo loguju upozorenje umjesto da bacaju izuzetak:
/// - Load() greška -> store kreće sa praznim in-memory skupom (čitanje NIKAD ne blokira slanje zauvijek), samo
///   gubi zaštitu od duplikata za već ranije zabilježene ključeve dok se fajl ponovo ne pročita.
/// - MarkSent() greška -> poziv se vraća bez izuzetka; Worker.HandleAsync ga zove NAKON uspješnog SMTP slanja i
///   PRIJE ack-a, pa email ostaje ispravno poslan i potvrđen (acked) čak i kad upis u fajl ne uspije - samo je
///   TA jedna poruka izložena rijetkom duplikatu ako dođe do redelivery-a prije nego se fajl popravi.
///
/// STARI FORMAT FAJLA (prije ovog fix-a: gole cifre, jedan <c>EmailNotificationMessage.MessageId</c> po redu)
/// se učitava bez greške - svaki red se tretira kao proizvoljan ključ. Pošto novi ključevi uvijek sadrže ':'
/// (MessageId:CreatedAtTicks:hash), stari red (npr. "109") nikad ne pogađa nijedan novi ključ - ostaje
/// bezopasno "mrtvo" dok se ne izbaci kompakcijom. Nema pada, nema posebnog parsiranja; samo privremeni gubitak
/// zaštite od duplikata za poruke poslane PRIJE ovog fix-a (dovoljno rijedak i bezopasan slučaj da ne
/// opravdava složeniju migraciju formata).
///
/// POŠTENA NAPOMENA: preostali mikroskopski prozor i dalje postoji - pad procesa TAČNO između uspješnog SMTP
/// odgovora i upisa u ovaj fajl (koji se dešava odmah nakon) i dalje može proizvesti rijedak duplikat. SMTP
/// protokol sam po sebi nema idempotenciju (nema "Message-Id" provjere na prijemu), pa se taj prozor ne može
/// potpuno zatvoriti bez transakcionog SMTP pošiljaoca ili two-phase commit-a sa brokerom. Ovo rješenje
/// pokriva realni slučaj iz BG-06 (kill/restart ili prekid konekcije NAKON što je upis već izvršen), ne
/// teorijski minimalan prozor.
/// </summary>
public sealed class SentMessageIdStore
{
    /// <summary>Koliko zadnjih ključeva fajl čuva (dovoljno za nekoliko sati saobraćaja u ovom projektu).</summary>
    public const int MaxEntries = 2000;

    /// <summary>Fajl se sažima tek kad preraste ograničenje za ovoliko - da se ne piše cijeli fajl na svaki send.</summary>
    private const int CompactSlack = 200;

    private readonly string _path;
    private readonly ILogger _logger;
    private readonly HashSet<string> _ids = new();
    private readonly Queue<string> _order = new();
    private readonly Lock _gate = new();

    public SentMessageIdStore(IOptions<SmtpOptions> options, ILogger<SentMessageIdStore> logger)
        : this(options.Value.SentMessageIdsFilePath, logger)
    {
    }

    public SentMessageIdStore(string? path, ILogger? logger = null)
    {
        _path = ResolvePath(path);
        _logger = logger ?? NullLogger.Instance;
        Load();
    }

    /// <summary>Da li je perzistencija uopšte uključena (prazna putanja = isključeno, npr. u testovima).</summary>
    public bool IsEnabled => _path.Length > 0;

    /// <summary>
    /// Putanja fajla za perzistenciju: prazna vrijednost ostaje prazna (isključeno). Apsolutna putanja (npr.
    /// docker-compose-ovo <c>/data/gobeyond-consumer-sent-ids.txt</c> na imenovanom volume-u) se koristi
    /// tačno takva. Relativna putanja (podrazumijevana vrijednost iz appsettings.Shared.json kad se servis
    /// pokrene van docker-a, npr. golim <c>dotnet run</c> iz root-a repozitorija) se NIKAD ne rješava protiv
    /// trenutnog radnog direktorija (review defekt: to bi upisalo fajl unutar git repozitorija, u OneDrive
    /// sinhronizovan folder) - umjesto toga se rješava protiv OS temp foldera (isti direktorij koji Worker
    /// već koristi za svoj health-check fajl), pod-folderom specifičnim za ovaj servis.
    /// </summary>
    public static string ResolvePath(string? configuredPath)
    {
        var trimmed = configuredPath?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) return string.Empty;
        if (Path.IsPathRooted(trimmed)) return trimmed;
        return Path.Combine(Path.GetTempPath(), "gobeyond-email-consumer", trimmed);
    }

    private void Load()
    {
        if (!IsEnabled) return;
        try
        {
            if (!File.Exists(_path)) return;
            var lines = File.ReadAllLines(_path);
            var start = Math.Max(0, lines.Length - MaxEntries);
            for (var i = start; i < lines.Length; i++)
            {
                var key = lines[i].Trim();
                if (key.Length > 0 && _ids.Add(key)) _order.Enqueue(key);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ne blokira pokretanje ni slanje: store nastavlja sa praznim in-memory skupom (vidi napomenu na
            // klasi). Bez ovoga bi neuspješno čitanje fajla (npr. zaključan od strane OneDrive sinhronizacije)
            // srušilo konstrukciju ovog singleton-a i time cijeli hosted servis pri pokretanju.
            _logger.LogWarning(ex, "Could not read the sent-ids store at '{Path}'; starting with an empty in-memory record " +
                "(a message this process already sent before could be resent once if the broker redelivers it now). Startup and sending continue.", _path);
        }
    }

    /// <summary>True ako je poruka sa ovim ključem (<see cref="EmailIdempotencyKey"/>) već ranije uspješno poslana.</summary>
    public bool WasSent(string key)
    {
        if (!IsEnabled || string.IsNullOrEmpty(key)) return false;
        lock (_gate) return _ids.Contains(key);
    }

    /// <summary>Bilježi uspješno slanje. Pozvati ODMAH nakon SMTP potvrde, PRIJE BasicAck-a (vidi napomenu iznad).</summary>
    public void MarkSent(string key)
    {
        if (!IsEnabled || string.IsNullOrEmpty(key)) return;
        lock (_gate)
        {
            if (!_ids.Add(key)) return; // već zabilježeno (npr. redelivery prije nego je stigao ack)
            _order.Enqueue(key);

            try
            {
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.AppendAllText(_path, key + Environment.NewLine);

                if (_order.Count > MaxEntries + CompactSlack)
                {
                    while (_order.Count > MaxEntries)
                        _ids.Remove(_order.Dequeue());
                    File.WriteAllLines(_path, _order);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Poruka je u ovom trenutku VEĆ uspješno poslana preko SMTP-a (vidi Worker.HandleAsync - ovaj
                // metod se zove poslije SendAsync-a, prije BasicAck-a). Neuspio upis na disk ne smije izgledati
                // kao neuspjelo slanje (review defekt: prije ovoga bi izuzetak ovdje pao u isti catch blok kao
                // SMTP greška, pa bi se već poslan email tretirao kao neuspjeh - retry/dead-letter - dok bi
                // redelivery iste poruke onda bio pogrešno prepoznat kao "već poslana" preko in-memory skupa
                // gore, pa se NIKAD ne bi zapravo poslala druga kopija, ali bi se DLQ/retry log lažno žalio).
                // In-memory zapis (_ids.Add gore) ostaje - ovaj proces i dalje prepoznaje redelivery dok radi;
                // samo je trajni zapis izgubljen, pa restart procesa gubi tu zaštitu za baš ovu poruku.
                _logger.LogWarning(ex, "Could not persist Email {Key} to the sent-ids store at '{Path}'; a redelivery after a " +
                    "process restart could resend it once. The email itself was already sent successfully.", key, _path);
            }
        }
    }
}
