using System.Text;
using System.Text.RegularExpressions;
using GoBeyond.EmailConsumer.Options;
using GoBeyond.EmailConsumer.Services;
using Microsoft.Extensions.Options;

namespace GoBeyond.Tests.Email;

public class SmtpEmailSenderTests
{
    private const string ParagraphStart = "<p style=\"margin:0 0 12px;\">";

    // Provjerava tačno ono što ide na žicu (multipart granice, transfer-encoding, header-e) preko FakeSmtpServer,
    // ne preko mockovanog IEmailSender.
    [Fact]
    public async Task SendAsync_SendsMultipartAlternative_WithPlainAndHtmlParts_AndAMessageIdOnTheFromDomain()
    {
        var (data, rcptTo) = await SendAndCaptureAsync("Pozdrav Č,\n\nDrugi paragraf sa dijakritikom čćžšđ.\n\nVaš GoBeyond tim");

        Assert.Equal("RCPT TO:<client@example.org>", rcptTo);
        Assert.Contains("Content-Type: multipart/alternative;", data);
        Assert.Matches(@"Message-ID: <[^@]+@gobeyond\.ba>", data);

        var plain = FindPart(data, "text/plain");
        Assert.Contains("Content-Type: text/plain; charset=utf-8", plain.Headers);
        Assert.Equal("Pozdrav Č,\r\n\r\nDrugi paragraf sa dijakritikom čćžšđ.\r\n\r\nVaš GoBeyond tim", plain.Decode());

        var html = FindPart(data, "text/html");
        Assert.Contains("Content-Type: text/html; charset=utf-8", html.Headers);
        Assert.Contains("Content-Transfer-Encoding: quoted-printable", html.Headers);
        var htmlText = html.Decode();
        Assert.Contains(ParagraphStart + "Drugi paragraf sa dijakritikom čćžšđ.</p>", htmlText);
        Assert.Contains(ParagraphStart + "Vaš GoBeyond tim</p>", htmlText);
        Assert.Contains("GoBeyond</p>", htmlText); // malo zaglavlje sa imenom pošiljaoca
        Assert.Contains("Ovo je automatska poruka GoBeyond aplikacije.", htmlText);

        // Bez linkova i slika (nema "tracking pixel"-a).
        Assert.DoesNotContain("<img", htmlText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<a ", htmlText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=", htmlText, StringComparison.OrdinalIgnoreCase);
    }

    // NotificationSender gradi tekst sa golim "\n"; text/plain dio mora nositi CRLF prijelome reda (RFC 2045),
    // a ne jedan red sa kodiranim "=0A" ili "=0D=0A".
    [Fact]
    public async Task SendAsync_SendsThePlainPartAsBase64_WithCrlfLineBreaks()
    {
        var (data, _) = await SendAndCaptureAsync("Pozdrav,\n\nVaš termin je sutra.\rZadnji red\r\nKraj");

        var plain = FindPart(data, "text/plain");
        Assert.Contains("Content-Transfer-Encoding: base64", plain.Headers);
        Assert.Equal("Pozdrav,\r\n\r\nVaš termin je sutra.\r\nZadnji red\r\nKraj", plain.Decode());
    }

    [Fact]
    public async Task SendAsync_TreatsSeveralBlankLinesAsOneParagraphBreak_InTheHtmlPart()
    {
        var (data, _) = await SendAndCaptureAsync("Prvi paragraf\n\n\nDrugi paragraf\n\n\n\nTreći paragraf");

        var htmlText = FindPart(data, "text/html").Decode();
        Assert.Contains(ParagraphStart + "Drugi paragraf</p>", htmlText);
        Assert.Contains(ParagraphStart + "Treći paragraf</p>", htmlText);
        Assert.DoesNotContain("<br>", htmlText);
    }

    [Fact]
    public async Task SendAsync_HtmlEncodesTheBody_SoItCannotInjectMarkup()
    {
        var (data, _) = await SendAndCaptureAsync("<script>alert(1)</script> & tekst");

        var htmlText = FindPart(data, "text/html").Decode();
        Assert.DoesNotContain("<script>alert", htmlText);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt; &amp; tekst", htmlText);
    }

    private static async Task<(string Data, string? RcptTo)> SendAndCaptureAsync(string body)
    {
        await using var server = FakeSmtpServer.Start();
        var sender = new SmtpEmailSender(Options.Create(new SmtpOptions
        {
            Host = "127.0.0.1",
            Port = server.Port,
            FromEmail = "noreply@gobeyond.ba",
            FromName = "GoBeyond"
        }));

        await sender.SendAsync("client@example.org", "Test", body);
        return (server.LastDataRaw!, server.LastRcptTo);
    }

    /// <summary>Dio multipart poruke sa zadatim media type-om: header-i i redovi tijela kako su došli na žicu.</summary>
    private static MimePart FindPart(string data, string mediaType)
    {
        var boundary = Regex.Match(data, "boundary=\"?([^\"\\s;]+)").Groups[1].Value;
        Assert.NotEmpty(boundary);

        var parts = new List<List<string>>();
        foreach (var line in data.Split('\n').Select(line => line.TrimEnd('\r')))
        {
            if (line.StartsWith("--" + boundary, StringComparison.Ordinal)) parts.Add([]);
            else if (parts.Count > 0) parts[^1].Add(line);
        }

        foreach (var part in parts)
        {
            var headerEnd = part.IndexOf(string.Empty);
            if (headerEnd < 0) continue;
            var headers = string.Join("\n", part.Take(headerEnd));
            if (headers.Contains($"Content-Type: {mediaType};", StringComparison.Ordinal))
                return new MimePart(headers, part.Skip(headerEnd + 1).ToList());
        }
        throw new Xunit.Sdk.XunitException($"No {mediaType} part in:\n{data}");
    }

    private sealed record MimePart(string Headers, IReadOnlyList<string> BodyLines)
    {
        /// <summary>Tijelo dekodirano prema Content-Transfer-Encoding (base64 ili quoted-printable), kao UTF-8.</summary>
        public string Decode()
        {
            var lines = BodyLines.Reverse().SkipWhile(line => line.Length == 0).Reverse().ToList();
            if (Headers.Contains("Content-Transfer-Encoding: base64", StringComparison.Ordinal))
                return Encoding.UTF8.GetString(Convert.FromBase64String(string.Concat(lines)));

            Assert.Contains("Content-Transfer-Encoding: quoted-printable", Headers);
            var bytes = new List<byte>();
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var softBreak = line.EndsWith('=');
                if (softBreak) line = line[..^1];
                for (var j = 0; j < line.Length; j++)
                {
                    if (line[j] == '=')
                    {
                        bytes.Add(Convert.ToByte(line.Substring(j + 1, 2), 16));
                        j += 2;
                    }
                    else bytes.Add((byte)line[j]);
                }
                if (!softBreak && i < lines.Count - 1) bytes.AddRange("\r\n"u8.ToArray());
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }
    }
}
