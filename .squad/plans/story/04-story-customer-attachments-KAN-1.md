# Story 04 — Customer Attachments (Story: KAN-1)

## Prerequisites

- [01-story-customer-core-crud-KAN-1.md](01-story-customer-core-crud-KAN-1.md) completed: requires the `Customer` entity, `IApplicationDbContext.Customers`, and `CustomersController`.
- Independent of [02-story-customer-interactions-KAN-1.md](02-story-customer-interactions-KAN-1.md) and [03-story-customer-notes-KAN-1.md](03-story-customer-notes-KAN-1.md) — attachments are a third, separate child aggregate of `Customer`. This plan is written assuming Stories 02 and 03 already landed (so `CustomersController` and `IApplicationDbContext` already carry their additions), but nothing here requires their specific code paths.
- Introduces a new cross-cutting abstraction, `IFileStorageService`, that does not exist anywhere in the codebase yet (confirmed: no `IFileStorage`, `IStorageService`, or similar interface exists under `src/AzmCrm.Application/Shared/Interfaces/`). Coordinate with whoever owns Infrastructure if a non-local storage backend (e.g. S3/Azure Blob) is already planned, since this story implements local-disk storage only.

## Story Goal

Let support agents upload file attachments to a customer profile and later list and download them, satisfying KAN-1's "Add notes and attachments to customer records" acceptance criterion (the attachments half — notes are Story 03).

Outcomes:
1. `POST /api/customers/{customerId}/attachments` (multipart/form-data) uploads a file and attaches it to a customer.
2. `GET /api/customers/{customerId}/attachments` lists a customer's attachments (metadata only), paginated, newest first.
3. `GET /api/customers/{customerId}/attachments/{attachmentId}/download` streams the file content back.

**Not in scope**: deleting an attachment, replacing/versioning an attachment, virus scanning, and any non-local storage backend (S3, Azure Blob, etc.) — the `IFileStorageService` abstraction introduced here is deliberately storage-backend-agnostic so a future story can swap `LocalFileStorageService` for a cloud implementation without touching the Application layer or the controller.

## Context — Read These Files First

1. [01-story-customer-core-crud-KAN-1.md](01-story-customer-core-crud-KAN-1.md) — read in full. Reuses the same command/query/handler/validator/EF-configuration shape.
2. [02-story-customer-interactions-KAN-1.md](02-story-customer-interactions-KAN-1.md) Task 2 (`CreateCustomerInteractionCommandHandler`) — precedent for the "verify parent customer exists via `AnyAsync`, else throw `NotFoundException`" guard this story's upload/list/download handlers all reuse.
3. [src/AzmCrm.API/Program.cs](../../../src/AzmCrm.API/Program.cs) lines 29-33 — Kestrel is already configured with `options.Limits.MaxRequestBodySize = 52_428_800; // 50 MB`, with the comment `// Allow file uploads up to 50 MB` — confirms file uploads were anticipated at the hosting level; this story is the first to actually use that headroom. No change needed here, but the per-file cap this story enforces in `FileStorageSettings.MaxFileSizeBytes` (Task 3) must stay at or below this 50 MB request-body ceiling.
4. [src/AzmCrm.Infrastructure/AzmCrm.Infrastructure.csproj](../../../src/AzmCrm.Infrastructure/AzmCrm.Infrastructure.csproj) line 4 (`<FrameworkReference Include="Microsoft.AspNetCore.App" />`) — confirms `Microsoft.Extensions.Hosting.IHostEnvironment` (needed to resolve a filesystem root path) is already available to the Infrastructure project without adding a package.
5. [src/AzmCrm.Infrastructure/Identity/JwtSettings.cs](../../../src/AzmCrm.Infrastructure/Identity/JwtSettings.cs) — read in full (11 lines). Precedent for a settings class bound from configuration (`SectionName` constant + `required`/default-valued properties) — `FileStorageSettings` follows the same shape.
6. [src/AzmCrm.Infrastructure/DependencyInjection.cs](../../../src/AzmCrm.Infrastructure/DependencyInjection.cs) lines 31 (`services.Configure<JwtSettings>(...)`) and 81-85 (the `AddScoped<I...Service, ...>()` block) — this story adds one `Configure<FileStorageSettings>` call and one `AddScoped<IFileStorageService, LocalFileStorageService>()` call following these exact patterns, inserted after line 85 and before line 86 (`services.AddHttpContextAccessor();`).
7. [src/AzmCrm.API/appsettings.json](../../../src/AzmCrm.API/appsettings.json) lines 5-10 (`"JwtSettings"` section) — precedent for where/how to add the new `"FileStorage"` configuration section.
8. [src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs) — this story adds `DbSet<CustomerAttachment> CustomerAttachments { get; }` alongside the members added in Stories 01-03.
9. [src/AzmCrm.API/Controllers/Base/ApiControllerBase.cs](../../../src/AzmCrm.API/Controllers/Base/ApiControllerBase.cs) — read in full (29 lines). The download action uses `ControllerBase.File(Stream, string, string)` (inherited via `ApiControllerBase : ControllerBase`, line 10) rather than one of the `ToXResult` helpers, since it returns raw file content, not a `Result<T>` envelope.
10. [src/AzmCrm.API/Controllers/CustomersController.cs](../../../src/AzmCrm.API/Controllers/CustomersController.cs) — edited by Stories 01-03. This story adds three more actions to the same file.
11. [src/AzmCrm.Application/Localization/LocalizationKeys.cs](../../../src/AzmCrm.Application/Localization/LocalizationKeys.cs) lines 6-18 — add one new key, `FileTooLarge`, to the `Validation` nested class.
12. [src/AzmCrm.Infrastructure/Localization/Resources/Messages.en.json](../../../src/AzmCrm.Infrastructure/Localization/Resources/Messages.en.json) and [Messages.ar.json](../../../src/AzmCrm.Infrastructure/Localization/Resources/Messages.ar.json) — add the matching text for `Validation.FileTooLarge` under `"Validation"`.

## Implementation tasks

### 1 — Domain layer

**Create file: `src/AzmCrm.Domain/Features/Customers/CustomerAttachment.cs`**

```csharp
using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.Customers;

public sealed class CustomerAttachment : BaseEntity
{
    public required Guid CustomerId { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long FileSizeBytes { get; init; }
    public required string StorageKey { get; init; }

    public Customer Customer { get; init; } = null!;
}
```

### 2 — Application layer

**Create file: `src/AzmCrm.Application/Shared/Interfaces/IFileStorageService.cs`**

```csharp
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
```

**Create file: `src/AzmCrm.Application/Features/Customers/DTOs/CustomerAttachmentDto.cs`**

```csharp
namespace AzmCrm.Application.Features.Customers.DTOs;

public sealed record CustomerAttachmentDto(
    Guid Id,
    Guid CustomerId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    DateTime CreatedOn
);
```

**Create file: `src/AzmCrm.Application/Features/Customers/DTOs/CustomerAttachmentContentDto.cs`**

```csharp
namespace AzmCrm.Application.Features.Customers.DTOs;

public sealed record CustomerAttachmentContentDto(Stream Content, string ContentType, string FileName);
```

**Create file: `src/AzmCrm.Application/Features/Customers/Commands/UploadCustomerAttachment/UploadCustomerAttachmentCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Customers.Commands.UploadCustomerAttachment;

public sealed record UploadCustomerAttachmentCommand(
    Guid CustomerId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    Stream Content
) : IRequest<Result<Guid>>;
```

**Create file: `src/AzmCrm.Application/Features/Customers/Commands/UploadCustomerAttachment/UploadCustomerAttachmentCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Customers.Commands.UploadCustomerAttachment;

internal sealed class UploadCustomerAttachmentCommandHandler(
    IApplicationDbContext dbContext,
    IFileStorageService fileStorage)
    : IRequestHandler<UploadCustomerAttachmentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(UploadCustomerAttachmentCommand request, CancellationToken ct)
    {
        var customerExists = await dbContext.Customers.AnyAsync(c => c.Id == request.CustomerId, ct);
        if (!customerExists)
            throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        var storageKey = await fileStorage.SaveAsync(request.Content, request.FileName, ct);

        var attachment = new CustomerAttachment
        {
            CustomerId = request.CustomerId,
            FileName = request.FileName,
            ContentType = request.ContentType,
            FileSizeBytes = request.FileSizeBytes,
            StorageKey = storageKey
        };

        dbContext.CustomerAttachments.Add(attachment);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(attachment.Id);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Customers/Commands/UploadCustomerAttachment/UploadCustomerAttachmentCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using AzmCrm.Application.Shared.Interfaces;
using FluentValidation;

namespace AzmCrm.Application.Features.Customers.Commands.UploadCustomerAttachment;

public sealed class UploadCustomerAttachmentCommandValidator : AbstractValidator<UploadCustomerAttachmentCommand>
{
    public UploadCustomerAttachmentCommandValidator(
        ILocalizationService localization, IFileStorageService fileStorage)
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Customer Id"]);

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "File Name"])
            .MaximumLength(255).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "File Name", 255]);

        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0)
                .WithMessage(localization[LocalizationKeys.Validation.MustBeGreaterThan, "File Size", 0])
            .LessThanOrEqualTo(fileStorage.MaxFileSizeBytes)
                .WithMessage(localization[
                    LocalizationKeys.Validation.FileTooLarge, fileStorage.MaxFileSizeBytes / 1_048_576]);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Customers/Queries/GetCustomerAttachments/GetCustomerAttachmentsQuery.cs`**

```csharp
using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomerAttachments;

public sealed record GetCustomerAttachmentsQuery(
    Guid CustomerId,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<Result<PaginatedResult<CustomerAttachmentDto>>>;
```

**Create file: `src/AzmCrm.Application/Features/Customers/Queries/GetCustomerAttachments/GetCustomerAttachmentsQueryHandler.cs`**

```csharp
using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomerAttachments;

internal sealed class GetCustomerAttachmentsQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetCustomerAttachmentsQuery, Result<PaginatedResult<CustomerAttachmentDto>>>
{
    public async Task<Result<PaginatedResult<CustomerAttachmentDto>>> Handle(
        GetCustomerAttachmentsQuery request, CancellationToken ct)
    {
        var customerExists = await dbContext.Customers.AnyAsync(c => c.Id == request.CustomerId, ct);
        if (!customerExists)
            throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        var query = dbContext.CustomerAttachments.Where(a => a.CustomerId == request.CustomerId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.CreatedOn)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new CustomerAttachmentDto(
                a.Id, a.CustomerId, a.FileName, a.ContentType, a.FileSizeBytes, a.CreatedOn))
            .ToListAsync(ct);

        var result = new PaginatedResult<CustomerAttachmentDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<CustomerAttachmentDto>>.Success(result);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Customers/Queries/GetCustomerAttachments/GetCustomerAttachmentsQueryValidator.cs`** — same paging-range rules as `GetCustomersListQueryValidator` (Story 01), plus `RuleFor(x => x.CustomerId).NotEmpty()...`.

**Create file: `src/AzmCrm.Application/Features/Customers/Queries/GetCustomerAttachmentContent/GetCustomerAttachmentContentQuery.cs`**

```csharp
using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomerAttachmentContent;

public sealed record GetCustomerAttachmentContentQuery(
    Guid CustomerId, Guid AttachmentId
) : IRequest<Result<CustomerAttachmentContentDto>>;
```

**Create file: `src/AzmCrm.Application/Features/Customers/Queries/GetCustomerAttachmentContent/GetCustomerAttachmentContentQueryHandler.cs`**

```csharp
using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomerAttachmentContent;

internal sealed class GetCustomerAttachmentContentQueryHandler(
    IApplicationDbContext dbContext,
    IFileStorageService fileStorage)
    : IRequestHandler<GetCustomerAttachmentContentQuery, Result<CustomerAttachmentContentDto>>
{
    public async Task<Result<CustomerAttachmentContentDto>> Handle(
        GetCustomerAttachmentContentQuery request, CancellationToken ct)
    {
        var attachment = await dbContext.CustomerAttachments
            .FirstOrDefaultAsync(a => a.Id == request.AttachmentId && a.CustomerId == request.CustomerId, ct)
            ?? throw new NotFoundException(
                $"Attachment '{request.AttachmentId}' was not found for customer '{request.CustomerId}'.");

        var stream = await fileStorage.OpenReadAsync(attachment.StorageKey, ct);

        var dto = new CustomerAttachmentContentDto(stream, attachment.ContentType, attachment.FileName);

        return Result<CustomerAttachmentContentDto>.Success(dto);
    }
}
```

Note the compound `FirstOrDefaultAsync(a => a.Id == request.AttachmentId && a.CustomerId == request.CustomerId, ct)` filter — this deliberately scopes the lookup to the given `customerId` so `/api/customers/{customerId}/attachments/{attachmentId}/download` 404s if `attachmentId` exists but belongs to a *different* customer, rather than leaking cross-customer attachment content by id alone.

**Edit file: `src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs`** — add `DbSet<CustomerAttachment> CustomerAttachments { get; }` alongside the members added in Stories 01-03.

**Edit file: `src/AzmCrm.Application/Localization/LocalizationKeys.cs`** — add to the `Validation` nested class:
```csharp
public const string FileTooLarge = "Validation.FileTooLarge";
```

**Edit file: `src/AzmCrm.Infrastructure/Localization/Resources/Messages.en.json`** — add to the `"Validation"` object: `"FileTooLarge": "File exceeds the maximum allowed size of {0} MB."`.

**Edit file: `src/AzmCrm.Infrastructure/Localization/Resources/Messages.ar.json`** — add to the `"Validation"` object: `"FileTooLarge": "حجم الملف يتجاوز الحد الأقصى المسموح به وهو {0} ميغابايت."`.

### 3 — Infrastructure layer

**Create file: `src/AzmCrm.Infrastructure/Storage/FileStorageSettings.cs`**

```csharp
namespace AzmCrm.Infrastructure.Storage;

public sealed class FileStorageSettings
{
    public const string SectionName = "FileStorage";

    public string RootPath { get; init; } = "App_Data/attachments";
    public long MaxFileSizeBytes { get; init; } = 10_485_760; // 10 MB per file
}
```

**Create file: `src/AzmCrm.Infrastructure/Storage/LocalFileStorageService.cs`**

```csharp
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
```

**Edit file: `src/AzmCrm.Infrastructure/Data/Configurations/`** — **create file: `CustomerAttachmentConfiguration.cs`**:

```csharp
using AzmCrm.Domain.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class CustomerAttachmentConfiguration : IEntityTypeConfiguration<CustomerAttachment>
{
    public void Configure(EntityTypeBuilder<CustomerAttachment> builder)
    {
        builder.ToTable("CustomerAttachments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .ValueGeneratedNever();

        builder.Property(a => a.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.ContentType)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.StorageKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasOne(a => a.Customer)
            .WithMany()
            .HasForeignKey(a => a.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(a => !a.IsDeleted);

        builder.HasIndex(a => a.CustomerId);
    }
}
```

**Edit file: `src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs`** — add `public DbSet<CustomerAttachment> CustomerAttachments => Set<CustomerAttachment>();` next to the properties added in Stories 01-03.

**Edit file: `src/AzmCrm.Infrastructure/DependencyInjection.cs`** — insert, after line 85 (`services.AddScoped<IIdentityQueryService, IdentityQueryService>();`) and before line 86 (`services.AddHttpContextAccessor();`):

```csharp
services.Configure<FileStorageSettings>(configuration.GetSection(FileStorageSettings.SectionName));
services.AddScoped<IFileStorageService, LocalFileStorageService>();
```

Add `using AzmCrm.Infrastructure.Storage;` to the file's `using` block.

**Edit file: `src/AzmCrm.API/appsettings.json`** — add a new top-level section (e.g. after the `"Cors"` section, lines 48-52):

```json
"FileStorage": {
  "RootPath": "App_Data/attachments",
  "MaxFileSizeBytes": 10485760
}
```

**Generate migration:**

```bash
dotnet ef migrations add AddCustomerAttachments --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API
```

### 4 — API layer

**Edit file: `src/AzmCrm.API/Controllers/CustomersController.cs`** — add three actions (with corresponding `using` statements for the new command/query namespaces and `CustomerAttachmentDto`):

```csharp
[HttpPost("{customerId:guid}/attachments")]
[ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> UploadAttachment(Guid customerId, IFormFile file, CancellationToken ct)
{
    await using var stream = file.OpenReadStream();

    var command = new UploadCustomerAttachmentCommand(
        customerId, file.FileName, file.ContentType, file.Length, stream);

    var result = await mediator.Send(command, ct);

    return ToCreatedResult(result, id => $"/api/customers/{customerId}/attachments/{id}");
}

[HttpGet("{customerId:guid}/attachments")]
[ProducesResponseType(typeof(Result<PaginatedResult<CustomerAttachmentDto>>), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetAttachments(
    Guid customerId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
{
    var result = await mediator.Send(new GetCustomerAttachmentsQuery(customerId, pageNumber, pageSize), ct);
    return ToResult(result);
}

[HttpGet("{customerId:guid}/attachments/{attachmentId:guid}/download")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> DownloadAttachment(Guid customerId, Guid attachmentId, CancellationToken ct)
{
    var result = await mediator.Send(new GetCustomerAttachmentContentQuery(customerId, attachmentId), ct);

    return File(result.Data!.Content, result.Data.ContentType, result.Data.FileName);
}
```

`DownloadAttachment` does not need a `result.IsFailure` branch: `GetCustomerAttachmentContentQueryHandler` never returns a `Failure` — it throws `NotFoundException` instead, which `ExceptionHandlingMiddleware` turns into a 404 before this action body would ever see a failed `Result`.

## Edge Cases & Failure Modes

- **`customerId` does not resolve to an existing, non-deleted customer** — `UploadCustomerAttachmentCommandHandler` and `GetCustomerAttachmentsQueryHandler` both check `dbContext.Customers.AnyAsync(...)` and throw `NotFoundException` → 404, same pattern as Stories 02-03.
- **`attachmentId` exists but belongs to a different customer than the `customerId` in the route** — `GetCustomerAttachmentContentQueryHandler`'s `FirstOrDefaultAsync(a => a.Id == request.AttachmentId && a.CustomerId == request.CustomerId, ct)` requires both to match, so this 404s rather than serving the file — prevents an agent who knows one customer's attachment URL pattern from downloading another customer's file by guessing/enumerating `attachmentId` values alone.
- **Uploaded file exceeds `FileStorageSettings.MaxFileSizeBytes` (10 MB default)** — rejected by `UploadCustomerAttachmentCommandValidator`'s `LessThanOrEqualTo(fileStorage.MaxFileSizeBytes)` rule, surfaced as a 400 via the existing `ValidationBehavior` pipeline. This is a separate, lower ceiling than Kestrel's 50 MB `MaxRequestBodySize` (`Program.cs:29-33`) — a request between 10 MB and 50 MB reaches the validator and gets a clean 400 with a `Validation.FileTooLarge` message; a request over 50 MB is rejected by Kestrel itself before ASP.NET Core model binding even runs, with a generic connection-level error rather than this story's JSON error shape. Document this two-tier limit for API consumers.
- **Zero-byte file upload** — rejected by `GreaterThan(0)` on `FileSizeBytes` in the validator.
- **Empty/missing `file` in the multipart body** — `IFormFile file` is a required action parameter with no `[FromForm]`/nullable annotation; ASP.NET Core's model binding returns 400 automatically if the `file` part is absent, before the command is even constructed.
- **`ResolveSafePath` in `LocalFileStorageService`** — every `storageKey` stored in the database was generated by `SaveAsync` as `{Guid}_{original file name}` and is never taken from user input at read time (the controller passes `attachment.StorageKey` straight from the database row, never a raw query-string value), and `Path.GetFileName` strips any directory traversal segment (`..`) regardless — this closes off path traversal even if a future caller passed an attacker-influenced key.
- **Uploaded `FileName` containing path-unsafe characters** (e.g. `../../etc/passwd`, or OS-reserved characters) — `LocalFileStorageService.SaveAsync` calls `Path.GetFileName(fileName)` before building the storage key, stripping any directory component from the *original* uploaded name too, so the on-disk file always lands directly under `_rootPath` regardless of what the client claims the file is named. The original (unsafe) name is still preserved as `CustomerAttachment.FileName` for display/download purposes — only the storage key is sanitized.
- **Concurrent uploads to the same customer** — no locking; each upload creates an independent `CustomerAttachment` row and an independent on-disk file (unique `Guid`-prefixed key), so there is no race condition between concurrent uploads.
- **Deleting a customer does not remove or hide its attachments, nor does it delete the underlying files on disk** — same caveat as Stories 02-03's cascade note: `OnDelete(DeleteBehavior.Cascade)` only fires on a hard delete of `Customer`, which never happens in this codebase; a soft-deleted customer's attachment rows and files remain, but become unreachable via the API because the parent-existence check 404s. Orphaned on-disk files after a hypothetical future hard-delete path are a known, undocumented-until-now gap — flag it if a hard-delete or GDPR-erasure story is ever planned.
- **`App_Data/attachments` directory does not exist on first run** — `LocalFileStorageService.SaveAsync` calls `Directory.CreateDirectory(_rootPath)` (idempotent) before every write, so no separate provisioning step is required in any environment.

## Migration / Rollback

- The EF Core migration in Task 3 only adds the `CustomerAttachments` table — additive, safe on top of Stories 01-03's schema.
- **Rollback**: `dotnet ef database update AddCustomerNotes --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` (i.e. back to the migration Story 03 added) drops the `CustomerAttachments` table. This does **not** delete any files already written to `App_Data/attachments` on disk — those become orphaned and must be cleaned up manually (or left, since they're inert without matching database rows) if a rollback is ever performed after files were uploaded in production.
- **Half-applied state**: if `LocalFileStorageService.SaveAsync` succeeds (file written to disk) but the subsequent `dbContext.SaveChangesAsync(ct)` in `UploadCustomerAttachmentCommandHandler` fails (e.g. a dropped database connection), the file is orphaned on disk with no corresponding `CustomerAttachments` row. This is a known gap in this story's scope — a fully transactional two-phase-commit across the filesystem and the database is not implemented. Flag this explicitly in code review; a future hardening pass could add a background cleanup job for orphaned files (matched by scanning `App_Data/attachments` against known `StorageKey`s) if this proves to be a real-world problem.

## Test Plan

1. **Edit `tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs`** (created in Story 01) — add `public DbSet<CustomerAttachment> CustomerAttachments => Set<CustomerAttachment>();`, and mirror `CustomerAttachmentConfiguration.HasQueryFilter(a => !a.IsDeleted)` in `OnModelCreating` (see Stories 01-03).
2. **Create file: `tests/AzmCrm.Application.Tests/TestDoubles/StubFileStorageService.cs`** — an in-memory `IFileStorageService` fake (`Dictionary<string, byte[]>` backing store, `MaxFileSizeBytes` settable per test) so handler tests don't touch the real filesystem.
3. **Create file: `tests/AzmCrm.Application.Tests/Features/Customers/UploadCustomerAttachmentCommandHandlerTests.cs`** — `Upload_for_existing_customer_persists_metadata_and_calls_storage`; `Upload_for_missing_customer_throws_NotFoundException_and_does_not_call_storage` (assert the stub's `SaveAsync` was never invoked, proving the existence check runs before the storage write).
4. **Create file: `tests/AzmCrm.Application.Tests/Features/Customers/GetCustomerAttachmentsQueryHandlerTests.cs`** — `List_returns_attachments_ordered_by_CreatedOn_desc`; `List_for_missing_customer_throws_NotFoundException`.
5. **Create file: `tests/AzmCrm.Application.Tests/Features/Customers/GetCustomerAttachmentContentQueryHandlerTests.cs`** — `Download_returns_content_for_matching_customer_and_attachment`; `Download_throws_NotFoundException_when_attachment_belongs_to_different_customer`; `Download_throws_NotFoundException_when_attachment_missing`.
6. **Create file: `tests/AzmCrm.Application.Tests/Features/Customers/UploadCustomerAttachmentCommandValidatorTests.cs`** — `Zero_byte_file_fails`; `File_over_MaxFileSizeBytes_fails`; `Empty_FileName_fails`; `Valid_command_passes` — use `StubLocalizationService` (Story 01) and `StubFileStorageService` with a small `MaxFileSizeBytes` to make the over-limit case cheap to construct.
7. **Create file: `tests/AzmCrm.Infrastructure.Tests/Storage/LocalFileStorageServiceTests.cs`** — **new test project required** (no `AzmCrm.Infrastructure.Tests` project currently exists — only `tests/AzmCrm.Application.Tests/` does). Add `tests/AzmCrm.Infrastructure.Tests/AzmCrm.Infrastructure.Tests.csproj` mirroring `tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`'s `TargetFramework`/`Nullable`/`ImplicitUsings`/xUnit package versions, referencing `src/AzmCrm.Infrastructure/AzmCrm.Infrastructure.csproj`, and add it to the solution (`dotnet sln add ...`). `LocalFileStorageService` is `internal`, matching this codebase's handler convention — add `src/AzmCrm.Infrastructure/AssemblyInfo.cs` with `[assembly: InternalsVisibleTo("AzmCrm.Infrastructure.Tests")]` (parallel to the `AzmCrm.Application/AssemblyInfo.cs` from Story 01), or the test project cannot construct it. `IHostEnvironment` needs a minimal hand-written stub (`StubHostEnvironment`, only `ContentRootPath` is exercised) since no mocking library is referenced. Tests: `SaveAsync_writes_file_and_returns_storage_key`; `OpenReadAsync_returns_written_content`; `DeleteAsync_removes_file`; `ResolveSafePath_strips_directory_traversal_from_storageKey` (save one real file first so the attachments directory exists, then pass a `storageKey` containing `"../"` and assert the resolved path — from the thrown `FileNotFoundException.FileName` — stays under the test's temp root; without an existing directory the traversal-stripped, still-missing file throws `DirectoryNotFoundException` instead, which is a different, also-safe outcome but breaks a same-exception-type assertion). Use a unique temp directory (e.g. `Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())`) per test class as `FileStorageSettings.RootPath`, cleaned up via `IDisposable`.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj` and `dotnet test tests/AzmCrm.Infrastructure.Tests/AzmCrm.Infrastructure.Tests.csproj`.
3. **Migration applies cleanly:** `dotnet ef database update --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` (or let the API apply it automatically on startup).
4. **Manual smoke test:** create a customer (Story 01), `POST /api/customers/{customerId}/attachments` with a small multipart file, confirm 201; `GET /api/customers/{customerId}/attachments` lists it; `GET /api/customers/{customerId}/attachments/{attachmentId}/download` streams the original bytes back byte-for-byte; repeat the download against a different, valid customer's id and confirm 404; upload a file larger than the configured `MaxFileSizeBytes` and confirm a 400 with a `Validation.FileTooLarge`-derived message.

## Done Criteria

- [ ] `CustomerAttachment` entity, `IFileStorageService`/`LocalFileStorageService`, EF configuration, and migration exist and apply cleanly on top of Stories 01-03's schema.
- [ ] `POST`, `GET` (list), and `GET .../download` all work end-to-end against a real Postgres database and the local filesystem.
- [ ] A downloaded file's bytes, `ContentType`, and `FileName` match what was uploaded.
- [ ] Cross-customer attachment access (`attachmentId` valid but for a different `customerId`) returns 404.
- [ ] Oversized and zero-byte uploads are both rejected with a 400.
- [ ] All new handler, validator, and `LocalFileStorageService` unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.
