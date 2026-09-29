using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace GoBeyond.EmailConsumer.Services;

/// <summary>Zašto se email ne šalje kroz stvarni SMTP (vidi <see cref="RecipientSuppression.Evaluate"/>).</summary>
public enum SuppressionReason
{
    /// <summary>Email se šalje.</summary>
    None,

    /// <summary>Domena primaoca je na listi zaštićenih domena ili je njena poddomena.</summary>
    ProtectedDomain,

    /// <summary>
    /// Host primaoca u obliku koji bi SmtpClient poslao nije ispravno DNS ime (dozvoljena su samo ASCII slova, cifre, '-' i
    /// '.'), npr. "edu@gobeyond.ba" ili "gobeyond.ba&gt;" nastali iz Unicode varijanti znakova '@' i '&gt;'.
    /// </summary>
    InvalidHostName
}

/// <summary>
/// Odlučuje da li se email NE šalje kroz stvarni SMTP: primalac je na "zaštićenoj" domeni (npr. seed/demo korisnici na
/// <c>@gobeyond.ba</c>, koja može pripadati trećoj strani) ili njenoj poddomeni, ili host primaoca nije ispravno DNS ime.
/// Supresija se primjenjuje SAMO kada SMTP host NIJE Mailpit - u Mailpitu email nikad ne napušta mašinu, pa je demo poštu
/// korisno vidjeti tamo bez ograničenja. Kada je host stvaran SMTP servis (npr. Gmail), supresija štiti taj nalog od
/// slanja na izmišljene/tuđe adrese.
/// </summary>
public static class RecipientSuppression
{
    /// <summary>Host vrijednosti koje se smatraju Mailpit-om (docker-compose servis, ili lokalni dev bez .env).</summary>
    private static readonly string[] MailpitHosts = ["mailpit", "localhost", "127.0.0.1"];

    private static readonly IdnMapping Idn = new();

    /// <summary>Niz znakova koji nisu dozvoljeni u DNS imenu hosta (sve osim ASCII slova, cifara, '-' i '.').</summary>
    private static readonly Regex NonHostCharacters = new("[^A-Za-z0-9.-]+", RegexOptions.CultureInvariant);

    /// <summary>Da li se zadati SMTP host smatra Mailpit-om (demo sandbox, ne stvarna dostava).</summary>
    public static bool IsMailpitHost(string? host) =>
        !string.IsNullOrWhiteSpace(host) && MailpitHosts.Contains(host.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Prva domena iz <see cref="ExtractDomains"/>, ili null ako adresa nema domenu.</summary>
    public static string? ExtractDomain(string? email) => ExtractDomains(email).FirstOrDefault();

    /// <summary>
    /// Domene primalaca onakve kakve bi SmtpClient stvarno poslao. Adresa se parsira kao u <see cref="SmtpEmailSender"/>
    /// (<see cref="MailAddressCollection"/>, pa i lista adresa odvojenih zarezom daje sve primaoce), pa oblici poput
    /// "x@gobeyond.ba." ili "&lt;x@gobeyond.ba&gt;" daju istu domenu kao "x@gobeyond.ba"; Unicode varijante (fullwidth
    /// slova, ideografska tačka, zero-width razmak) se svode na ASCII oblik - vidi <see cref="NormalizeHost"/>.
    /// </summary>
    public static IReadOnlyList<string> ExtractDomains(string? email)
    {
        if (TryParseHosts(email) is { } hosts) return hosts.Where(x => x.Length > 0).ToList();
        return FallbackHost(email) is { Length: > 0 } host ? [host] : [];
    }

    /// <summary>
    /// Razlog zbog kojeg email na <paramref name="recipientEmail"/> treba biti preskočen (samo logovan i ack-ovan), ili
    /// <see cref="SuppressionReason.None"/>. Kad host nije Mailpit:
    /// <list type="bullet">
    /// <item><see cref="SuppressionReason.ProtectedDomain"/> - domena primaoca je na listi <paramref name="suppressedDomains"/>
    /// ili je njena poddomena (npr. <c>edu.gobeyond.ba</c> za konfigurisano <c>gobeyond.ba</c>). Poredi se i svaki ispravan
    /// dio neispravnog hosta (npr. "gobeyond.ba" u "edu@gobeyond.ba" ili "gobeyond.ba&gt;"). Domena koja samo završava istim
    /// slovima bez tačke ispred (npr. <c>notgobeyond.ba</c>) nije poddomena i ne potiskuje se.</item>
    /// <item><see cref="SuppressionReason.InvalidHostName"/> - host nije ispravno DNS ime, pa bi SmtpClient poslao
    /// neispravnu adresu (npr. <c>RCPT TO:&lt;x@edu@gobeyond.ba&gt;</c>) čije tumačenje zavisi od servera.</item>
    /// </list>
    /// Adresa koju ni SmtpEmailSender ne može parsirati se ionako ne šalje (slanje baca izuzetak i ide kroz retry/dead-letter),
    /// pa se za nju poredi samo dio iza zadnjeg '@' sa zaštićenim domenama.
    /// </summary>
    public static SuppressionReason Evaluate(string? host, string? recipientEmail, IReadOnlyCollection<string>? suppressedDomains)
    {
        if (IsMailpitHost(host)) return SuppressionReason.None;

        var protectedDomains = (suppressedDomains ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => NormalizeHost(x.Trim()))
            .Where(x => x.Length > 0)
            .ToList();

        if (TryParseHosts(recipientEmail) is not { } hosts)
            return FallbackHost(recipientEmail) is { } fallback && IsProtected(fallback, protectedDomains)
                ? SuppressionReason.ProtectedDomain
                : SuppressionReason.None;

        if (hosts.Any(x => IsProtected(x, protectedDomains))) return SuppressionReason.ProtectedDomain;
        if (hosts.Any(x => !IsDnsHostName(x))) return SuppressionReason.InvalidHostName;
        return SuppressionReason.None;
    }

    /// <summary>True ako email ne treba slati kroz zadati SMTP host (<see cref="Evaluate"/> nije <see cref="SuppressionReason.None"/>).</summary>
    public static bool ShouldSuppress(string? host, string? recipientEmail, IReadOnlyCollection<string>? suppressedDomains) =>
        Evaluate(host, recipientEmail, suppressedDomains) != SuppressionReason.None;

    /// <summary>
    /// Hostovi svih primalaca kako ih parsira <see cref="MailAddressCollection"/> (kao <c>MailMessage.To.Add</c> u
    /// <see cref="SmtpEmailSender"/>), u obliku iz <see cref="NormalizeHost"/>; null ako parser adresu odbija.
    /// </summary>
    private static List<string>? TryParseHosts(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var addresses = new MailAddressCollection();
        try
        {
            addresses.Add(email);
        }
        catch (FormatException)
        {
            return null;
        }
        return addresses.Count == 0 ? null : addresses.Select(x => NormalizeHost(x.Host)).ToList();
    }

    /// <summary>Dio iza zadnjeg '@' za adresu koju parser odbija (npr. "not-an-email" nema domenu).</summary>
    private static string? FallbackHost(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var at = email.LastIndexOf('@');
        return at >= 0 && at < email.Length - 1 ? NormalizeHost(email[(at + 1)..]) : null;
    }

    /// <summary>
    /// Host u obliku koji SmtpClient stavlja na žicu: ne-ASCII host prolazi kroz <see cref="IdnMapping.GetAscii"/>
    /// (kao u <see cref="MailAddress"/>), a završna tačka se skida tek POSLIJE toga, jer konverzija i Unicode
    /// tačke (U+3002, U+FF0E, U+FF61) ili "." sa ignorisanim znakom iza (U+200B) pretvara u završnu ASCII tačku.
    /// Konverzija može dati i znakove koji nisu dio DNS imena (npr. U+FF20 → '@', U+FE65 → '&gt;', U+00A0 → ' '), pa
    /// se host ne trimuje: razmak koji SmtpClient pošalje ostaje vidljiv provjeri DNS imena.
    /// </summary>
    private static string NormalizeHost(string host)
    {
        var ascii = host;
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

        return ascii.TrimEnd('.');
    }

    /// <summary>Ispravno DNS ime hosta: neprazno i samo ASCII slova, cifre, '-' i '.'.</summary>
    private static bool IsDnsHostName(string host) => host.Length > 0 && !NonHostCharacters.IsMatch(host);

    /// <summary>
    /// True ako je host (ili bilo koji njegov dio između znakova koji nisu dio DNS imena) zaštićena domena ili njena
    /// poddomena.
    /// </summary>
    private static bool IsProtected(string host, IReadOnlyCollection<string> protectedDomains)
    {
        if (protectedDomains.Count == 0) return false;
        foreach (var part in NonHostCharacters.Split(host))
        {
            var candidate = part.Trim('.');
            if (candidate.Length == 0) continue;
            foreach (var configured in protectedDomains)
            {
                if (string.Equals(candidate, configured, StringComparison.OrdinalIgnoreCase)) return true;
                if (candidate.EndsWith("." + configured, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        return false;
    }
}
