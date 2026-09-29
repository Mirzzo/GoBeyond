namespace GoBeyond.EmailConsumer.Services;

/// <summary>
/// Odlučuje da li se email NE šalje kroz stvarni SMTP jer je primalac na "zaštićenoj" domeni
/// (npr. seed/demo korisnici na <c>@gobeyond.ba</c>, koja može pripadati trećoj strani i nije poštanski
/// sandbox pod našom kontrolom). Supresija se primjenjuje SAMO kada SMTP host NIJE Mailpit: u Mailpitu
/// email nikad ne napušta mašinu (nema stvarnog SMTP naloga ni rizika za treće strane), pa je demo poštu
/// seed korisnika (registracija, obavijesti, poruke) korisno vidjeti tamo bez ikakvog ograničenja. Kada je
/// host stvaran SMTP servis (npr. Gmail), supresija štiti taj nalog od slanja na izmišljene/tuđe adrese.
/// </summary>
public static class RecipientSuppression
{
    /// <summary>Host vrijednosti koje se smatraju Mailpit-om (docker-compose servis, ili lokalni dev bez .env).</summary>
    private static readonly string[] MailpitHosts = ["mailpit", "localhost", "127.0.0.1"];

    /// <summary>Da li se zadati SMTP host smatra Mailpit-om (demo sandbox, ne stvarna dostava).</summary>
    public static bool IsMailpitHost(string? host) =>
        !string.IsNullOrWhiteSpace(host) && MailpitHosts.Contains(host.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Domena primaoca (dio iza zadnjeg '@'), ili null ako adresa nema domenu.</summary>
    public static string? ExtractDomain(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var at = email.LastIndexOf('@');
        return at >= 0 && at < email.Length - 1 ? email[(at + 1)..].Trim() : null;
    }

    /// <summary>
    /// True ako email na <paramref name="recipientEmail"/> treba biti preskočen (ne poslan, samo logovan i ACK-ovan)
    /// jer host nije Mailpit i domena primaoca je na listi <paramref name="suppressedDomains"/>.
    /// </summary>
    public static bool ShouldSuppress(string? host, string? recipientEmail, IReadOnlyCollection<string>? suppressedDomains)
    {
        if (IsMailpitHost(host)) return false;
        if (suppressedDomains is null || suppressedDomains.Count == 0) return false;

        var domain = ExtractDomain(recipientEmail);
        if (domain is null) return false;

        foreach (var suppressed in suppressedDomains)
        {
            if (string.Equals(suppressed.Trim(), domain, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
