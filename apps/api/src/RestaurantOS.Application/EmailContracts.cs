namespace RestaurantOS.Application;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public sealed record EmailMessage(
    string To,
    string Subject,
    string BodyText,
    string? BodyHtml = null);

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; init; } = "Logging";
    public string FromAddress { get; init; } = "noreply@restaurantos.local";
    public string FromName { get; init; } = "RestaurantOS";
    public SmtpEmailOptions Smtp { get; init; } = new();
}

public sealed class SmtpEmailOptions
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 587;
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public bool UseStartTls { get; init; } = true;
}
