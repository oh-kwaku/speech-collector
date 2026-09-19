using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace RecordingApp.Infrastructure.Notifications;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default);
}

/// <summary>
/// Sends mail through a Gmail account over SMTP (host app password), per project decision.
/// </summary>
public class GmailSmtpEmailSender(IOptions<SmtpOptions> options, ILogger<GmailSmtpEmailSender> logger)
    : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.StartTls, ct);
            await client.AuthenticateAsync(_options.Username, _options.AppPassword, ct);
            await client.SendAsync(message, ct);
        }
        finally
        {
            if (client.IsConnected) await client.DisconnectAsync(true, ct);
        }
        logger.LogInformation("Sent email to {Email}: {Subject}", toEmail, subject);
    }
}
