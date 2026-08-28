using AzmCrm.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace AzmCrm.Infrastructure.Tests.Storage;

public sealed class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly LocalFileStorageService _service;

    public LocalFileStorageServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var environment = new StubHostEnvironment { ContentRootPath = _tempRoot };
        var settings = Options.Create(new FileStorageSettings { RootPath = "attachments" });

        _service = new LocalFileStorageService(environment, settings);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Fact]
    public async Task SaveAsync_writes_file_and_returns_storage_key()
    {
        var content = new MemoryStream([1, 2, 3, 4]);

        var storageKey = await _service.SaveAsync(content, "invoice.pdf");

        Assert.EndsWith("_invoice.pdf", storageKey);
        var savedPath = Path.Combine(_tempRoot, "attachments", storageKey);
        Assert.True(File.Exists(savedPath));
        Assert.Equal([1, 2, 3, 4], await File.ReadAllBytesAsync(savedPath));
    }

    [Fact]
    public async Task OpenReadAsync_returns_written_content()
    {
        var content = new MemoryStream([5, 6, 7]);
        var storageKey = await _service.SaveAsync(content, "notes.txt");

        await using var readStream = await _service.OpenReadAsync(storageKey);
        using var memoryStream = new MemoryStream();
        await readStream.CopyToAsync(memoryStream);

        Assert.Equal([5, 6, 7], memoryStream.ToArray());
    }

    [Fact]
    public async Task DeleteAsync_removes_file()
    {
        var content = new MemoryStream([1]);
        var storageKey = await _service.SaveAsync(content, "temp.txt");
        var savedPath = Path.Combine(_tempRoot, "attachments", storageKey);
        Assert.True(File.Exists(savedPath));

        await _service.DeleteAsync(storageKey);

        Assert.False(File.Exists(savedPath));
    }

    [Fact]
    public async Task ResolveSafePath_strips_directory_traversal_from_storageKey()
    {
        // A well-behaved caller only ever passes back a key produced by SaveAsync, but
        // OpenReadAsync/DeleteAsync must not let a maliciously-crafted key escape _rootPath.
        // Save one real file first so the attachments directory exists — otherwise a missing
        // directory (rather than the sandboxing itself) would produce a different exception.
        await _service.SaveAsync(new MemoryStream([1]), "real.txt");

        var maliciousKey = "../../etc/passwd";

        // Path.GetFileName reduces "../../etc/passwd" to "passwd", which does not exist inside
        // the sandboxed attachments directory — proving the ".." segments never reached the
        // filesystem call, since a successful traversal would instead 404 outside _tempRoot
        // entirely or throw UnauthorizedAccessException on a real system file.
        var ex = await Assert.ThrowsAsync<FileNotFoundException>(() => _service.OpenReadAsync(maliciousKey));
        Assert.Contains("passwd", ex.FileName);
        Assert.StartsWith(Path.Combine(_tempRoot, "attachments"), ex.FileName);

        // DeleteAsync no-ops on a missing file rather than throwing.
        await _service.DeleteAsync(maliciousKey);
    }
}
