using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GoBeyond.Tests.Email;

/// <summary>
/// Minimalan SMTP server (EHLO/MAIL/RCPT/DATA/QUIT) na 127.0.0.1 za testove koji trebaju stvarno poslat
/// (ne mockovan) sadržaj - <see cref="SmtpEmailSenderTests"/> mora vidjeti tačno ono što bi otišlo na žicu
/// (multipart granice, transfer-encoding, header-e), što se ne može provjeriti mockovanjem <c>IEmailSender</c>.
/// </summary>
internal sealed class FakeSmtpServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly Task _acceptLoop;
    private readonly CancellationTokenSource _cts = new();
    public int Port { get; }
    public string? LastRcptTo { get; private set; }
    public string? LastDataRaw { get; private set; }

    private FakeSmtpServer(TcpListener listener)
    {
        _listener = listener;
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _acceptLoop = AcceptLoopAsync(_cts.Token);
    }

    public static FakeSmtpServer Start()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return new FakeSmtpServer(listener);
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(token);
                await HandleAsync(client, token);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task HandleAsync(TcpClient client, CancellationToken token)
    {
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);
        await using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" };
        await writer.WriteLineAsync("220 fake.local ESMTP");
        string? line;
        while ((line = await reader.ReadLineAsync(token)) != null)
        {
            if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("250-fake.local");
                await writer.WriteLineAsync("250 OK");
            }
            else if (line.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("250 OK");
            }
            else if (line.StartsWith("RCPT TO", StringComparison.OrdinalIgnoreCase))
            {
                LastRcptTo = line;
                await writer.WriteLineAsync("250 OK");
            }
            else if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("354 Send data");
                var data = new StringBuilder();
                while ((line = await reader.ReadLineAsync(token)) != null)
                {
                    if (line == ".") break;
                    data.AppendLine(line);
                }
                LastDataRaw = data.ToString();
                await writer.WriteLineAsync("250 OK queued");
            }
            else if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("221 Bye");
                break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();
        try { await _acceptLoop; } catch { /* stopping */ }
    }
}
