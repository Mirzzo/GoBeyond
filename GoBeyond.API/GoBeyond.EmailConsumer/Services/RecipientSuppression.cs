using System.Net.Mail;

namespace GoBeyond.EmailConsumer.Services;

/// <summary>
/// Odlučuje da li se email NE šalje kroz stvarni SMTP jer je primalac na "zaštićenoj" domeni
/// (npr. seed/demo korisnici na <c>@gobeyond.ba</c>, koja može pripadati trećoj strani i nije poštanski
/// sandbox pod našom kontrolom) ili na njenoj poddomeni (npr. <c>@edu.gobeyond.ba</c> - i dalje ista treća
/// strana). Supresija se primjenjuje SAMO kada SMTP host NIJE Mailpit: u Mailpitu email nikad ne napušta
/// mašinu (nema stvarnog SMTP naloga ni rizika za treće strane), pa je demo poštu seed korisnika
/// (registracija, obavijesti, poruke) korisno vidjeti tamo bez ikakvog ograničenja. Kada je host stvaran
/// SMTP servis (npr. Gmail), supresija štiti taj nalog od slanja na izmišljene/tuđe adrese.
/// </summary>
public static class RecipientSuppression
{
    /// <summary>Host vrijednosti koje se smatraju Mailpit-om (docker-compose servis, ili lokalni dev bez .env).</summary>
    private static readonly string[] MailpitHosts = ["mailpit", "localhost", "127.0.0.1"];

    /// <summary>Da li se zadati SMTP host smatra Mailpit-om (demo sandbox, ne stvarna dostava).</summary>
    public static bool IsMailpitHost(string? host) =>
        !string.IsNullOrWhiteSpace(host) && MailpitHosts.Contains(host.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Domena primaoca, ili null ako adresa nema domenu. Parsira preko <see cref="MailAddress"/> - ISTI
    /// parser koji <see cref="SmtpEmailSender"/> stvarno koristi za slanje (<c>MailMessage.To.Add</c>) - da bi
    /// supresija gledala TAČNO onu domenu na koju bi email stvarno otišao, ne sirovi queue string. Bez ovoga
    /// (review defekt) oblici koje MailAddress normalizuje - zavšna tačka ("x@gobeyond.ba."), uglaste zagrade
    /// ("&lt;x@gobeyond.ba&gt;") ili komentar ("x@gobeyond.ba(napomena)") - bi prošli mimo supresije iako bi se
    /// email stvarno poslao na "gobeyond.ba", jer bi sirovo dijeljenje po '@' vratilo drugačiji string.
    /// TrimEnd('.') pokriva slučaj kad MailAddress zadrži završnu tačku u Host-u.
    /// </summary>
    public static string? ExtractDomain(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        if (MailAddress.TryCreate(email.Trim(), out var address))
        {
            var host = address.Host.Trim().TrimEnd('.');
            return host.Length > 0 ? host : null;
        }

        // MailAddress odbija adresu koju smatra neispravnom (npr. "not-an-email", bez '@'); u tom slučaju
        // nema domene na koju bi se uopšte moglo poslati, pa se tretira isto kao i prije - bez supresije.
        var at = email.LastIndexOf('@');
        return at >= 0 && at < email.Length - 1 ? email[(at + 1)..].Trim().TrimEnd('.') : null;
    }

    /// <summary>
    /// True ako email na <paramref name="recipientEmail"/> treba biti preskočen (ne poslan, samo logovan i ACK-ovan)
    /// jer host nije Mailpit i domena primaoca je na listi <paramref name="suppressedDomains"/> ili je njena
    /// poddomena (npr. <c>edu.gobeyond.ba</c> za konfigurisano <c>gobeyond.ba</c> - ista treća strana kojoj
    /// postavka štiti poštu). Domena koja samo završava istim slovima bez tačke ispred (npr.
    /// <c>notgobeyond.ba</c>) NIJE poddomena i ne potiskuje se.
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
