using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using GoBeyond.EmailConsumer.Options;
using Microsoft.Extensions.Options;

namespace GoBeyond.EmailConsumer.Services;

/// <summary>
/// Šalje email kao multipart/alternative (text/plain + text/html) da bi izgledao kao stvarna poruka umjesto
/// poruke bez HTML dijela, koju spam filteri (Gmail) više sumnjiče. HTML dio je sadržajno isti tekst - bez
/// linkova i slika (nema "tracking pixel"-a), samo HTML-escape-ovan sa paragrafima i zaglavljem/podnožjem.
/// </summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseSsl,
            UseDefaultCredentials = false
        };
        if (!string.IsNullOrWhiteSpace(settings.Username))
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);

        var from = new MailAddress(settings.FromEmail, settings.FromName);
        using var message = new MailMessage
        {
            From = from,
            Subject = subject,
            SubjectEncoding = Encoding.UTF8
        };
        message.To.Add(to);
        message.Headers.Add("Message-ID", NewMessageId(from.Host));

        // Base64, ne quoted-printable: .NET-ov QP enkoder svaki CRLF piše kao "=0D=0A" (nikad kao pravi prijelom
        // reda), pa bi cijeli tekst bio jedan QP red. Base64 prenosi tekst sa CRLF prijelomima bez izmjene.
        var plainView = AlternateView.CreateAlternateViewFromString(NormalizeLineBreaks(body, "\r\n"), Encoding.UTF8, MediaTypeNames.Text.Plain);
        plainView.TransferEncoding = TransferEncoding.Base64;
        message.AlternateViews.Add(plainView);

        var htmlView = AlternateView.CreateAlternateViewFromString(BuildHtml(body, settings.FromName), Encoding.UTF8, MediaTypeNames.Text.Html);
        htmlView.TransferEncoding = TransferEncoding.QuotedPrintable;
        message.AlternateViews.Add(htmlView);

        await client.SendMailAsync(message, cancellationToken);
    }

    /// <summary>Message-ID sa domenom pošiljaoca, umjesto da SmtpClient sam smisli jednu.</summary>
    private static string NewMessageId(string fromDomain) => $"<{Guid.NewGuid():N}@{fromDomain}>";

    /// <summary>Svodi svaki prijelom reda (CRLF, LF ili CR) na <paramref name="lineBreak"/>.</summary>
    private static string NormalizeLineBreaks(string text, string lineBreak) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", lineBreak);

    /// <summary>
    /// Pretvara običan tekst poruke u minimalan, siguran HTML: paragrafi po praznom redu (isti razmak koji
    /// NotificationSender koristi), sav sadržaj HTML-escape-ovan, bez linkova i slika. Malo zaglavlje sa
    /// imenom pošiljaoca i fiksno podnožje.
    /// </summary>
    private static string BuildHtml(string body, string fromName)
    {
        var html = new StringBuilder();
        html.Append("<!doctype html><html><head><meta charset=\"utf-8\"></head>")
            .Append("<body style=\"font-family:Arial,sans-serif;font-size:14px;color:#222222;\">")
            .Append("<p style=\"font-weight:bold;font-size:16px;margin:0 0 16px;\">")
            .Append(WebUtility.HtmlEncode(fromName))
            .Append("</p>");

        var normalized = NormalizeLineBreaks(body, "\n");
        foreach (var paragraph in normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var lines = paragraph.Split('\n').Select(WebUtility.HtmlEncode);
            html.Append("<p style=\"margin:0 0 12px;\">").Append(string.Join("<br>", lines)).Append("</p>");
        }

        html.Append("<p style=\"margin-top:24px;color:#777777;font-size:12px;\">Ovo je automatska poruka GoBeyond aplikacije.</p>")
            .Append("</body></html>");
        return html.ToString();
    }
}
