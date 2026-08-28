using AzmCrm.Application.Shared.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AzmCrm.Infrastructure.Storage;

internal sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;
    private readonly FileStorageSettings _settings;

    public LocalFileStorageService(IHostEnvironment environment, IOptions<FileStorageSettings> settings)
    {
        _settings = settings.Value;
        _rootPath = Path.Combine(environment.ContentRootPath, _settings.RootPath);
    }

    public long MaxFileSizeBytes => _settings.MaxFileSizeBytes;

    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_rootPath);

        var storageKey = $"{Guid.CreateVersion7()}_{Path.GetFileName(fileName)}";
        var fullPath = Path.Combine(_rootPath, storageKey);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, ct);

        return storageKey;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        Stream stream = File.OpenRead(ResolveSafePath(storageKey));
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = ResolveSafePath(storageKey);
        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    // storageKey is always the bare "{Guid}_{fileName}" segment produced by SaveAsync, never a
    // caller-supplied path — Path.GetFileName strips any directory component (including "..")
    // a malicious storageKey might carry, so reads/deletes can never escape _rootPath.
    private string ResolveSafePath(string storageKey) => Path.Combine(_rootPath, Path.GetFileName(storageKey));
}
