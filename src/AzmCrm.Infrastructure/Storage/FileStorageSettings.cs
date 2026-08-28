namespace AzmCrm.Infrastructure.Storage;

public sealed class FileStorageSettings
{
    public const string SectionName = "FileStorage";

    public string RootPath { get; init; } = "App_Data/attachments";
    public long MaxFileSizeBytes { get; init; } = 10_485_760; // 10 MB per file
}
