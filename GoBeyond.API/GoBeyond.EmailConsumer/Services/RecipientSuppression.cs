using System.Globalization;
using System.Net.Mail;
using System.Text;

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
    /// Domena primaoca onakva kakvu bi SmtpClient stvarno poslao, ili null ako adresa nema domenu. Parsira preko
    /// <see cref="MailAddress"/> (isti parser kao <see cref="SmtpEmailSender"/>), pa oblici poput "x@gobeyond.ba."
    /// ili "&lt;x@gobeyond.ba&gt;" daju istu domenu kao "x@gobeyond.ba"; Unicode varijante (fullwidth slova,
    /// ideografska tačka, zero-width razmak) se svode na ASCII oblik - vidi <see cref="NormalizeHost"/>.
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

    /// <summary>
    /// Host u obliku koji SmtpClient stavlja na žicu: ne-ASCII host prolazi kroz <see cref="IdnMapping.GetAscii"/>
    /// (kao u <see cref="MailAddress"/>), a završna tačka se skida tek POSLIJE toga, jer konverzija i Unicode
    /// tačke (U+3002, U+FF0E, U+FF61) ili "." sa ignorisanim znakom iza (U+200B) pretvara u završnu ASCII tačku.
    /// </summary>
    private static string? NormalizeHost(string host)
    {
        var ascii = host.Trim();
        if (!Ascii.IsValid(ascii))
        {
            try
            {
                ascii = Idn.GetAscii(ascii);
            }
            catch (ArgumentException)
            {
                // Takav host odbija i SmtpClient (email ne ode); poredi se sirovi oblik.
            }
        }

        ascii = ascii.TrimEnd('.');
        return ascii.Length == 0 ? null : ascii;
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
            var configured = NormalizeHost(suppressed);
            if (configured is null) continue;
            if (string.Equals(configured, domain, StringComparison.OrdinalIgnoreCase)) return true;
            if (domain.EndsWith("." + configured, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
