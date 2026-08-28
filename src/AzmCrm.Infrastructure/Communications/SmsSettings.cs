namespace AzmCrm.Infrastructure.Communications;

public sealed class SmsSettings
{
    public const string SectionName = "Sms";

    public string ApiBaseUrl { get; init; } = "";
    public string ApiKey { get; init; } = "CHANGE_ME";
    public string SenderId { get; init; } = "AzmCRM";
}
