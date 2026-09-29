using System.Globalization;
using System.Net.Mail;

namespace GoBeyond.EmailConsumer.Services;

/// <summary>
/// Odlučuje da li se email NE šalje kroz stvarni SMTP jer je primalac na "zaštićenoj" domeni (npr. seed/demo
/// korisnici na <c>@gobeyond.ba</c>, koja može pripadati trećoj strani) ili na njenoj poddomeni. Supresija se
/// primjenjuje SAMO kada SMTP host NIJE Mailpit - u Mailpitu email nikad ne napušta mašinu, pa je demo poštu
/// korisno vidjeti tamo bez ograničenja. Kada je host stvaran SMTP servis (npr. Gmail), supresija štiti taj
/// nalog od slanja na izmišljene/tuđe adrese.
/// </summary>
public static class RecipientSuppression
{
    /// <summary>Host vrijednosti koje se smatraju Mailpit-om (docker-compose servis, ili lokalni dev bez .env).</summary>
    private static readonly string[] MailpitHosts = ["mailpit", "localhost", "127.0.0.1"];

    private static readonly IdnMapping Idn = new();

    /// <summary>Da li se zadati SMTP host smatra Mailpit-om (demo sandbox, ne stvarna dostava).</summary>
    public static bool IsMailpitHost(string? host) =>
        !string.IsNullOrWhiteSpace(host) && MailpitHosts.Contains(host.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Domena primaoca, ili null ako adresa nema domenu. Parsira preko <see cref="MailAddress"/> - isti parser
    /// koji <see cref="SmtpEmailSender"/> stvarno koristi za slanje - da bi supresija gledala tačno onu domenu
    /// na koju bi email stvarno otišao, ne sirovi queue string (oblici koje MailAddress normalizuje, npr.
    /// "x@gobeyond.ba." ili "&lt;x@gobeyond.ba&gt;", inače bi prošli mimo supresije). Domena se dodatno
    /// propušta kroz <see cref="IdnMapping.GetAscii"/> - istu konverziju koju SmtpClient radi nad hostom prije
    /// slanja - da bi Unicode oblici koje ta konverzija svodi na zaštićenu domenu (fullwidth slova, ideografska
    /// tačka, zero-width razmak) bili prepoznati kao ista domena.
    /// </summary>
    public static string? ExtractDomain(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        if (MailAddress.TryCreate(email.Trim(), out var address))
            return NormalizeHost(address.Host);

        // MailAddress odbija adresu koju smatra neispravnom (npr. "not-an-email", bez '@'); u tom slučaju
        // nema domene na koju bi se uopšte moglo poslati.
        var at = email.LastIndexOf('@');
        return at >= 0 && at < email.Length - 1 ? NormalizeHost(email[(at + 1)..]) : null;
    }

    private static string? NormalizeHost(string host)
    {
        var trimmed = host.Trim().TrimEnd('.');
        if (trimmed.Length == 0) return null;
        try
        {
            return Idn.GetAscii(trimmed);
        }
        catch (ArgumentException)
        {
            // Host koji IdnMapping odbija (npr. prekratak/predugačak label) - koristi kako jeste, ista
            // domena koju bi u tom slučaju vidio i sam SmtpClient.
            return trimmed;
        }
    }

    /// <summary>
    /// True ako email na <paramref name="recipientEmail"/> treba biti preskočen (samo logovan i ack-ovan) jer
    /// host nije Mailpit i domena primaoca je na listi <paramref name="suppressedDomains"/> ili je njena
    /// poddomena (npr. <c>edu.gobeyond.ba</c> za konfigurisano <c>gobeyond.ba</c>). Domena koja samo završava
    /// istim slovima bez tačke ispred (npr. <c>notgobeyond.ba</c>) nije poddomena i ne potiskuje se.
    /// </summary>
    public static bool ShouldSuppress(string? host, string? recipientEmail, IReadOnlyCollection<string>? suppressedDomains)
    {
        if (IsMailpitHost(host)) return false;
        if (suppressedDomains is null || suppressedDomains.Count == 0) return false;

        var domain = ExtractDomain(recipientEmail);
        if (domain is null) return false;

        foreach (var suppressed in suppressedDomains)
        {
            var configured = suppressed.Trim().TrimEnd('.');
            if (configured.Length == 0) continue;
            if (string.Equals(configured, domain, StringComparison.OrdinalIgnoreCase)) return true;
            if (domain.EndsWith("." + configured, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
