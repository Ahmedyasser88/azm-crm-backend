using AzmCrm.Application.Shared.Interfaces;

namespace AzmCrm.Application.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IFileStorageService"/> fake for handler/validator tests — no real
/// filesystem access. Tracks whether SaveAsync was called so handler tests can assert the
/// customer-existence check runs before any storage write is attempted.
/// </summary>
public sealed class StubFileStorageService : IFileStorageService
{
    private readonly Dictionary<string, byte[]> _store = [];

    public long MaxFileSizeBytes { get; init; } = 10_485_760;
    public bool SaveAsyncWasCalled { get; private set; }

    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        SaveAsyncWasCalled = true;

        using var memoryStream = new MemoryStream();
        await content.CopyToAsync(memoryStream, ct);

        var storageKey = $"{Guid.NewGuid()}_{fileName}";
        _store[storageKey] = memoryStream.ToArray();

        return storageKey;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(storageKey, out var bytes))
            throw new FileNotFoundException($"No stored content for key '{storageKey}'.");

        return Task.FromResult<Stream>(new MemoryStream(bytes));
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        _store.Remove(storageKey);
        return Task.CompletedTask;
    }
}
