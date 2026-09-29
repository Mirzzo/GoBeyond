using System.Globalization;
using GoBeyond.EmailConsumer.Options;
using Microsoft.Extensions.Options;

namespace GoBeyond.EmailConsumer.Services;

/// <summary>
/// Durable idempotency "brana" protiv duplog slanja emaila nakon pada procesa između uspješnog SMTP slanja i
/// BasicAck-a: RabbitMQ isporučuje poruku ponovo (at-least-once), pa bi se bez ovoga isti email poslao dvaput
/// (vidi BG-06). Pamti Id-eve (<c>EmailNotificationMessage.MessageId</c>, isti kao Id reda u OutboxMessages)
/// uspješno poslanih poruka u append-only tekstualnom fajlu (<see cref="SmtpOptions.SentMessageIdsFilePath"/>);
/// u docker-compose je taj fajl na imenovanom volume-u pa preživi restart kontejnera. Provjera (<see cref="WasSent"/>)
/// se radi PRIJE slanja, a upis (<see cref="MarkSent"/>) ODMAH NAKON uspješnog slanja i PRIJE ack-a - ako broker
/// dostavi istu poruku ponovo, consumer je prepozna i samo je ack-uje bez ponovnog slanja.
///
/// Fajl je namjerno jednostavan (bez baze/zaključavanja): jedini pisac je ovaj proces, a RabbitMQ prefetch=1
/// garantuje da se poruke obrađuju jedna po jedna (nema konkurentnog pristupa unutar procesa). Veličina je
/// ograničena na zadnjih <see cref="MaxEntries"/> Id-eva - fajl se periodično sažme (rewrite), umjesto da raste
/// beskonačno.
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
    /// <summary>Koliko zadnjih Id-eva fajl čuva (dovoljno za nekoliko sati saobraćaja u ovom projektu).</summary>
    public const int MaxEntries = 2000;

    /// <summary>Fajl se sažima tek kad preraste ograničenje za ovoliko - da se ne piše cijeli fajl na svaki send.</summary>
    private const int CompactSlack = 200;

    private readonly string _path;
    private readonly HashSet<int> _ids = new();
    private readonly Queue<int> _order = new();
    private readonly Lock _gate = new();

    public SentMessageIdStore(IOptions<SmtpOptions> options) : this(options.Value.SentMessageIdsFilePath)
    {
    }

    public SentMessageIdStore(string? path)
    {
        _path = path?.Trim() ?? string.Empty;
        Load();
    }

    /// <summary>Da li je perzistencija uopšte uključena (prazna putanja = isključeno, npr. u testovima).</summary>
    public bool IsEnabled => _path.Length > 0;

    private void Load()
    {
        if (!IsEnabled || !File.Exists(_path)) return;
        var lines = File.ReadAllLines(_path);
        var start = Math.Max(0, lines.Length - MaxEntries);
        for (var i = start; i < lines.Length; i++)
        {
            if (int.TryParse(lines[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && _ids.Add(id))
                _order.Enqueue(id);
        }
    }

    /// <summary>True ako je poruka sa ovim Id-em već ranije uspješno poslana (vjerovatan redelivery sa brokera).</summary>
    public bool WasSent(int messageId)
    {
        if (!IsEnabled) return false;
        lock (_gate) return _ids.Contains(messageId);
    }

    /// <summary>Bilježi uspješno slanje. Pozvati ODMAH nakon SMTP potvrde, PRIJE BasicAck-a (vidi napomenu iznad).</summary>
    public void MarkSent(int messageId)
    {
        if (!IsEnabled) return;
        lock (_gate)
        {
            if (!_ids.Add(messageId)) return; // već zabilježeno (npr. redelivery prije nego je stigao ack)
            _order.Enqueue(messageId);

            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.AppendAllText(_path, messageId.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);

            if (_order.Count > MaxEntries + CompactSlack)
            {
                while (_order.Count > MaxEntries)
                    _ids.Remove(_order.Dequeue());
                File.WriteAllLines(_path, _order.Select(id => id.ToString(CultureInfo.InvariantCulture)));
            }
        }
    }
}
