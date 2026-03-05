using Microsoft.Extensions.Options;
using GoBeyond.EmailConsumer.Options;

namespace GoBeyond.EmailConsumer.Services;

public class ConsoleEmailSender(
    ILogger<ConsoleEmailSender> logger,
    IOptions<SmtpOptions> options) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[Email] host={Host}:{Port} to={To} subject={Subject} body={Body}",
            options.Value.Host,
            options.Value.Port,
            to,
            subject,
            body);

        return Task.CompletedTask;
    }
}
