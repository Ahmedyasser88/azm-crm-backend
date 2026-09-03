namespace AzmCrm.Infrastructure.AiFeatures;

public sealed class OpenAiSettings
{
    public const string SectionName = "OpenAi";

    public string ApiBaseUrl { get; init; } = "https://api.openai.com/v1";
    public string ApiKey { get; init; } = "CHANGE_ME_OpenAiApiKey";
    public string Model { get; init; } = "gpt-4o-mini";
}
