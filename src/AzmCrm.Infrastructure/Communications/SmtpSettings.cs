namespace AzmCrm.Infrastructure.Communications;

public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 587;
    public bool EnableSsl { get; init; } = true;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string FromAddress { get; init; } = "support@azm.com.sa";
    public string FromName { get; init; } = "Azm CRM Support";
    public string InboundWebhookSecret { get; init; } = "CHANGE_ME";
}
