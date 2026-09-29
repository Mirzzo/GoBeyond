using GoBeyond.EmailConsumer.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GoBeyond.EmailConsumer.Services;

/// <summary>
/// Durable idempotency "brana" protiv duplog slanja emaila nakon pada procesa između uspješnog SMTP slanja i
/// BasicAck-a: RabbitMQ isporučuje poruku ponovo (at-least-once), pa bi se bez ovoga isti email poslao dvaput
/// nakon restarta. Za redelivery DOK je proces živ i prvi pokušaj još u toku vidi
/// <see cref="InFlightSendGate"/> - ovaj store to ne pokriva jer se upisuje tek nakon uspješnog slanja.
///
/// Pamti ključeve (<see cref="EmailIdempotencyKey"/>) uspješno poslanih poruka u append-only tekstualnom fajlu
/// (<see cref="SmtpOptions.SentMessageIdsFilePath"/>); u docker-compose je taj fajl na imenovanom volume-u pa
/// preživi restart kontejnera. Provjera (<see cref="WasSent"/>) se radi prije slanja, a upis
/// (<see cref="MarkSent"/>) odmah nakon uspješnog slanja i prije ack-a. Jedini pisac je ovaj proces, a RabbitMQ
/// prefetch=1 garantuje obradu jedne poruke odjednom, pa fajlu ne treba baza ni zaključavanje. Veličina je
/// ograničena na zadnjih <see cref="MaxEntries"/> ključeva - fajl se periodično sažme umjesto da raste beskonačno.
///
/// <see cref="Load"/> i <see cref="MarkSent"/> hvataju svoje IO greške (zaključan/nedostupan fajl, npr. tokom
/// OneDrive sinhronizacije) i samo loguju upozorenje: Load() kreće sa praznim in-memory skupom umjesto da sruši
/// pokretanje servisa, a MarkSent() greška ne smije izgledati kao neuspjelo slanje - poruka je u tom trenutku
/// već uspješno poslana i acked, samo je trajni zapis izgubljen do sljedećeg uspješnog upisa.
///
/// Stari format fajla (gole cifre, jedan MessageId po redu) se učitava bez greške - takav red nikad ne sadrži
/// ':' pa ne pogađa nijedan novi ključ i ostaje bezopasan dok se ne izbaci kompakcijom.
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
    /// trenutnog radnog direktorija - to bi upisalo fajl unutar git repozitorija - nego protiv OS temp foldera
    /// (isti direktorij koji Worker već koristi za svoj health-check fajl), u pod-folderu za ovaj servis.
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
                // Poruka je u ovom trenutku već uspješno poslana preko SMTP-a (ovaj metod se zove poslije
                // SendAsync-a, prije BasicAck-a) - neuspio upis na disk ne smije izgledati kao neuspjelo slanje.
                // In-memory zapis (_ids.Add gore) ostaje, pa ovaj proces i dalje prepoznaje redelivery dok radi;
                // samo je trajni zapis izgubljen, pa restart procesa gubi tu zaštitu za baš ovu poruku.
                _logger.LogWarning(ex, "Could not persist Email {Key} to the sent-ids store at '{Path}'; a redelivery after a " +
                    "process restart could resend it once. The email itself was already sent successfully.", key, _path);
            }
        }
    }
}
