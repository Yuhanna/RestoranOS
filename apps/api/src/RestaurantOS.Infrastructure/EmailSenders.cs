using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantOS.Application;

#pragma warning disable CA1848

namespace RestaurantOS.Infrastructure;

public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Email to {Recipient}: {Subject}", message.To, message.Subject);
        }

        return Task.CompletedTask;
    }
}

public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var config = options.Value;
        var smtp = config.Smtp;
        if (string.IsNullOrWhiteSpace(smtp.Host))
        {
            throw new InvalidOperationException("Email:Smtp:Host is required when Email:Provider is Smtp.");
        }

        using var client = new System.Net.Mail.SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.UseStartTls,
            DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network,
        };
        if (!string.IsNullOrWhiteSpace(smtp.Username))
        {
            client.Credentials = new System.Net.NetworkCredential(smtp.Username, smtp.Password);
        }

        using var mail = new System.Net.Mail.MailMessage
        {
            From = new System.Net.Mail.MailAddress(config.FromAddress, config.FromName),
            Subject = message.Subject,
            Body = message.BodyHtml ?? message.BodyText,
            IsBodyHtml = message.BodyHtml is not null,
        };
        mail.To.Add(message.To);
        if (message.BodyHtml is not null)
        {
            mail.AlternateViews.Add(
                System.Net.Mail.AlternateView.CreateAlternateViewFromString(
                    message.BodyText,
                    null,
                    "text/plain"));
        }

        await client.SendMailAsync(mail, cancellationToken);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("SMTP email sent to {Recipient}", message.To);
        }
    }
}
