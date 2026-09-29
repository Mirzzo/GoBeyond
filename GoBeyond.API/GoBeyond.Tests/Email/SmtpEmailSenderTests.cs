using GoBeyond.EmailConsumer.Options;
using GoBeyond.EmailConsumer.Services;
using Microsoft.Extensions.Options;

namespace GoBeyond.Tests.Email;

public class SmtpEmailSenderTests
{
    // Gmail i slični spam-filteri sumnjiče poruke bez HTML dijela; ovaj test provjerava tačno ono što ide na
    // žicu (multipart granice, transfer-encoding, header-e) preko FakeSmtpServer, ne preko mockovanog IEmailSender.
    [Fact]
    public async Task SendAsync_SendsMultipartAlternative_WithPlainAndHtmlParts_AndAMessageIdOnTheFromDomain()
    {
        await using var server = FakeSmtpServer.Start();
        var options = Options.Create(new SmtpOptions
        {
            Host = "127.0.0.1",
            Port = server.Port,
            FromEmail = "noreply@gobeyond.ba",
            FromName = "GoBeyond"
        });
        var sender = new SmtpEmailSender(options);

        await sender.SendAsync("client@example.org", "Test naslov", "Pozdrav Č,\n\nDrugi paragraf sa dijakritikom čćžšđ.\n\nVaš GoBeyond tim");

        Assert.Equal("RCPT TO:<client@example.org>", server.LastRcptTo);
        var data = server.LastDataRaw!;

        Assert.Contains("Content-Type: multipart/alternative;", data);
        Assert.Contains("Content-Type: text/plain; charset=utf-8", data);
        Assert.Contains("Content-Type: text/html; charset=utf-8", data);
        Assert.Contains("Content-Transfer-Encoding: quoted-printable", data);
        Assert.Matches(@"Message-ID: <[^@]+@gobeyond\.ba>", data);

        // Dijakritici u oba dijela su quoted-printable, ali tekst mora biti prisutan (dekodiran ručno ovdje
        // provjerom hex sekvenci bi bilo krhko) - dovoljno je da su oba paragrafa i header/footer prisutni.
        Assert.Contains("Drugi paragraf", data);
        Assert.Contains("Va=C5=A1 GoBeyond tim", data); // "Vaš GoBeyond tim" quoted-printable
        Assert.Contains("GoBeyond</p>", data); // malo zaglavlje sa imenom pošiljaoca
        Assert.Contains("Ovo je automatska poruka GoBeyond aplikacije.", data);

        // Bez linkova i slika (nema "tracking pixel"-a).
        Assert.DoesNotContain("<img", data, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<a ", data, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=", data, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAsync_HtmlEncodesTheBody_SoItCannotInjectMarkup()
    {
        await using var server = FakeSmtpServer.Start();
        var options = Options.Create(new SmtpOptions
        {
            Host = "127.0.0.1",
            Port = server.Port,
            FromEmail = "noreply@gobeyond.ba",
            FromName = "GoBeyond"
        });
        var sender = new SmtpEmailSender(options);

        await sender.SendAsync("client@example.org", "Test", "<script>alert(1)</script> & tekst");

        var data = server.LastDataRaw!;
        var htmlPart = data[data.IndexOf("Content-Type: text/html", StringComparison.Ordinal)..];
        Assert.DoesNotContain("<script>alert", htmlPart);
        Assert.Contains("&lt;script&gt;", htmlPart);
    }
}
