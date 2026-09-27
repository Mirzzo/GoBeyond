using System.Net;
using System.Net.Mail;
using System.Text;
using GoBeyond.EmailConsumer.Options;
using Microsoft.Extensions.Options;

namespace GoBeyond.EmailConsumer.Services;

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

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromEmail, settings.FromName),
            Subject = subject,
            Body = body,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false
        };
        message.To.Add(to);
        await client.SendMailAsync(message, cancellationToken);
    }
}
