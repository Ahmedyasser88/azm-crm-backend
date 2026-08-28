namespace AzmCrm.Application.Shared.Interfaces;

/// <summary>
/// Storage-backend-agnostic abstraction for persisting uploaded files. The Application
/// layer never touches the filesystem directly — swap the Infrastructure-layer
/// implementation (e.g. to S3/Azure Blob) without changing any command/query/handler.
/// </summary>
public interface IFileStorageService
{
    long MaxFileSizeBytes { get; }

    Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}
