namespace GoBeyond.EmailConsumer.Options;

/// <summary>SMTP postavke (sekcija "Smtp" u appsettings.Shared.json; u docker-compose host je Mailpit).</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public bool UseSsl { get; set; }
}
