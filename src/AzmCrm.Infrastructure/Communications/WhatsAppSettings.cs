namespace AzmCrm.Infrastructure.Communications;

public sealed class WhatsAppSettings
{
    public const string SectionName = "WhatsApp";

    public string ApiBaseUrl { get; init; } = "https://graph.facebook.com/v21.0";
    public string PhoneNumberId { get; init; } = "";
    public string AccessToken { get; init; } = "CHANGE_ME";
    public string WebhookVerifyToken { get; init; } = "CHANGE_ME";
}
