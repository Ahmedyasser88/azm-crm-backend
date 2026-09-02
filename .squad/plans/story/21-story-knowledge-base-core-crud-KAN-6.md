# Story 21 — Knowledge Base Core CRUD & FAQ Entries (Story: KAN-6)

## Prerequisites

- None — this story creates a brand-new aggregate independent of every prior KAN-1..KAN-5 story, mirroring how [17-story-sla-policies-KAN-5.md](17-story-sla-policies-KAN-5.md) and [15-story-quick-reply-templates-KAN-4.md](15-story-quick-reply-templates-KAN-4.md) each started a new, unrelated table.
- [15-story-quick-reply-templates-KAN-4.md](15-story-quick-reply-templates-KAN-4.md) — read as the worked example for this story's team-shared CRUD shape (`QuickReplyTemplate`/`QuickReplyTemplatesController`): create/update/delete/get/list, no per-agent ownership, kebab-case route override.
- [17-story-sla-policies-KAN-5.md](17-story-sla-policies-KAN-5.md) — read as the worked example for a list query with `enum`-typed filters instead of free-text `search` (`GetSlaPoliciesListQuery`'s `Priority`/`IsActive` filters), which this story's `GetKnowledgeArticlesListQuery` follows for its `Type`/`Status`/`Category` filters.

## Story Goal

Let an agent create, categorize, and manage FAQ entries, help articles, and step-by-step guides as a single `KnowledgeArticle` aggregate, satisfying KAN-6's "Create and manage FAQ entries" acceptance criterion and laying the entity groundwork [22-story-knowledge-article-publishing-KAN-6.md](22-story-knowledge-article-publishing-KAN-6.md), [23-story-knowledge-article-guide-steps-KAN-6.md](23-story-knowledge-article-guide-steps-KAN-6.md), and [24-story-knowledge-base-search-KAN-6.md](24-story-knowledge-base-search-KAN-6.md) build on.

Outcomes:
1. A single `KnowledgeArticle` entity models all three content shapes named in KAN-6's description (FAQ entries, help articles, solution guides) via a `KnowledgeArticleType` enum (`Faq`, `Article`, `Guide`) — one table, one CRUD surface, instead of three near-duplicate entities. This mirrors how [05-story-ticket-core-crud-KAN-2.md](05-story-ticket-core-crud-KAN-2.md) modeled every ticket shape as one `Ticket` entity distinguished by fields rather than by subclassing.
2. Every article also carries a `KnowledgeArticleStatus` (`Draft`, `Published`) — this story only ever creates articles as `Draft` and never exposes a way to change that status; **flipping** it is Story 22's entire job. Introducing the field now (rather than adding it in Story 22) avoids a later migration that would have to backfill an implicit "already published" default onto every row created by this story, the same "introduce the enum now, wire its transition later" split KAN-2 Story 07 used for `Ticket.Status`/`IsEscalated` versus KAN-5 Story 19's escalation trigger.
3. `POST/PUT/DELETE /api/knowledge-articles` and `GET /api/knowledge-articles`, `GET /api/knowledge-articles/{id}` let an agent manage articles, with the list endpoint filterable by `Type`, `Status`, and `Category`.
4. Every KAN-6 acceptance criterion the later three stories satisfy (publishing, step-by-step guides, full-text search) is expressed as `Type`/`Status`/child-table extensions of this same `KnowledgeArticle` row — there is deliberately no separate `Faq`, `HelpArticle`, or `Guide` table.

**Not in scope**: publishing/unpublishing an article (Story 22); step-by-step guide content — `KnowledgeArticleType.Guide` exists as a value now but no story-21 endpoint attaches ordered steps to it (Story 23); any search beyond the list endpoint's exact-match `Type`/`Status`/`Category` filters (Story 24); a public, unauthenticated read surface — every action on `KnowledgeArticlesController` in this story requires the same `[Authorize]` `ApiControllerBase` default every other management controller uses (Story 22 adds the first `[AllowAnonymous]` actions); view counts, ratings, or "was this helpful" feedback; article versioning/revision history; and rich-text/HTML sanitization — `Content` is stored and returned as plain `string`, exactly like `QuickReplyTemplate.Body` and `TicketComment.Content`.

## Context — Read These Files First

1. [src/AzmCrm.Domain/Features/QuickReplies/QuickReplyTemplate.cs](../../../src/AzmCrm.Domain/Features/QuickReplies/QuickReplyTemplate.cs) (9 lines, read in full) — `KnowledgeArticle`'s shape follows this exactly, substituting `Title`/`Body` for `Title`/`Content` plus four more properties.
2. [src/AzmCrm.Domain/Features/Tickets/TicketPriority.cs](../../../src/AzmCrm.Domain/Features/Tickets/TicketPriority.cs) (9 lines, read in full) — the exact shape `KnowledgeArticleType`/`KnowledgeArticleStatus` follow: a bare `namespace` + `public enum` block, no attributes.
3. [src/AzmCrm.Application/Features/QuickReplies/Commands/CreateQuickReplyTemplate/CreateQuickReplyTemplateCommand.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Commands/CreateQuickReplyTemplate/CreateQuickReplyTemplateCommand.cs), [CreateQuickReplyTemplateCommandHandler.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Commands/CreateQuickReplyTemplate/CreateQuickReplyTemplateCommandHandler.cs), and [CreateQuickReplyTemplateCommandValidator.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Commands/CreateQuickReplyTemplate/CreateQuickReplyTemplateCommandValidator.cs) — read all three in full; `CreateKnowledgeArticleCommand`/Handler/Validator copy this exact three-file shape.
4. [src/AzmCrm.Application/Features/QuickReplies/Commands/UpdateQuickReplyTemplate/UpdateQuickReplyTemplateCommand.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Commands/UpdateQuickReplyTemplate/UpdateQuickReplyTemplateCommand.cs), [UpdateQuickReplyTemplateCommandHandler.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Commands/UpdateQuickReplyTemplate/UpdateQuickReplyTemplateCommandHandler.cs), and [UpdateQuickReplyTemplateCommandValidator.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Commands/UpdateQuickReplyTemplate/UpdateQuickReplyTemplateCommandValidator.cs) — same, for `UpdateKnowledgeArticleCommand`.
5. [src/AzmCrm.Application/Features/QuickReplies/Commands/DeleteQuickReplyTemplate/DeleteQuickReplyTemplateCommandHandler.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Commands/DeleteQuickReplyTemplate/DeleteQuickReplyTemplateCommandHandler.cs) (23 lines, read in full) — note it takes `ICurrentUserService` to stamp `DeletedBy`; `DeleteKnowledgeArticleCommandHandler` copies this exactly, substituting `dbContext.QuickReplyTemplates`/`Knowledge article '{request.Id}'`.
6. [src/AzmCrm.Application/Features/QuickReplies/Queries/GetQuickReplyTemplateById/GetQuickReplyTemplateByIdQuery.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Queries/GetQuickReplyTemplateById/GetQuickReplyTemplateByIdQuery.cs) and [GetQuickReplyTemplateByIdQueryHandler.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Queries/GetQuickReplyTemplateById/GetQuickReplyTemplateByIdQueryHandler.cs) — same shape for `GetKnowledgeArticleByIdQuery`.
7. [src/AzmCrm.Application/Features/Sla/Queries/GetSlaPoliciesList/GetSlaPoliciesListQueryHandler.cs](../../../src/AzmCrm.Application/Features/Sla/Queries/GetSlaPoliciesList/GetSlaPoliciesListQueryHandler.cs) — the `AsQueryable()` + sequential `if (request.X is not null) query = query.Where(...)` shape `GetKnowledgeArticlesListQueryHandler` follows for its `Type`/`Status`/`Category` filters (three enum/string equality filters instead of SLA's two).
8. [src/AzmCrm.Application/Features/QuickReplies/Queries/GetQuickReplyTemplatesList/GetQuickReplyTemplatesListQueryValidator.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Queries/GetQuickReplyTemplatesList/GetQuickReplyTemplatesListQueryValidator.cs) (15 lines, read in full) — `GetKnowledgeArticlesListQueryValidator`'s `PageNumber`/`PageSize` rules copy this exactly.
9. [src/AzmCrm.API/Controllers/QuickReplyTemplatesController.cs](../../../src/AzmCrm.API/Controllers/QuickReplyTemplatesController.cs) (71 lines, read in full) — `KnowledgeArticlesController`'s shape, including the kebab-case `[Route("api/knowledge-articles")]` override (`ApiControllerBase`'s `api/[controller]` would otherwise resolve to `api/KnowledgeArticles`, never the hyphenated form every other multi-word controller in this codebase uses).
10. [src/AzmCrm.Infrastructure/Data/Configurations/QuickReplyTemplateConfiguration.cs](../../../src/AzmCrm.Infrastructure/Data/Configurations/QuickReplyTemplateConfiguration.cs) (27 lines, read in full) and [src/AzmCrm.Infrastructure/Data/Configurations/SlaPolicyConfiguration.cs](../../../src/AzmCrm.Infrastructure/Data/Configurations/SlaPolicyConfiguration.cs) (lines 388-417 of [17-story-sla-policies-KAN-5.md](17-story-sla-policies-KAN-5.md), which created it) — `KnowledgeArticleConfiguration` combines both: string properties like the first, `.HasConversion<string>().HasMaxLength(20)` enum properties like the second.
11. [src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs) (read in full, 8 `Domain.Features.*` usings + one `DbSet<T>` line per aggregate ending in `DbSet<EscalationRule> EscalationRules { get; }`), [src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs](../../../src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs) (same shape, `DbSet<T> X => Set<T>();` per line), and [tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs](../../../tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs) (same, plus one `modelBuilder.Entity<T>().HasQueryFilter(x => !x.IsDeleted);` line per aggregate in `OnModelCreating`) — each needs one new `DbSet<KnowledgeArticle> KnowledgeArticles` line (after the `EscalationRule` line) and, for the third file, one new query-filter line.
12. [src/AzmCrm.Application/Shared/Exceptions/NotFoundException.cs](../../../src/AzmCrm.Application/Shared/Exceptions/NotFoundException.cs) — the exception type every `Get*ById`/`Update*`/`Delete*` handler above throws on a missing row.
13. [src/AzmCrm.Application/Localization/LocalizationKeys.cs](../../../src/AzmCrm.Application/Localization/LocalizationKeys.cs) lines 8-18 — reuses `Validation.Required`, `Validation.MaxLength`, `Validation.InvalidValue`, `Validation.MustBeGreaterThan`. No new keys or `Messages.*.json` edits needed.

## Implementation tasks

### 1 — Domain layer

**Create file: `src/AzmCrm.Domain/Features/KnowledgeBase/KnowledgeArticleType.cs`**

```csharp
namespace AzmCrm.Domain.Features.KnowledgeBase;

public enum KnowledgeArticleType
{
    Faq,
    Article,
    Guide
}
```

**Create file: `src/AzmCrm.Domain/Features/KnowledgeBase/KnowledgeArticleStatus.cs`**

```csharp
namespace AzmCrm.Domain.Features.KnowledgeBase;

public enum KnowledgeArticleStatus
{
    Draft,
    Published
}
```

**Create file: `src/AzmCrm.Domain/Features/KnowledgeBase/KnowledgeArticle.cs`**

```csharp
using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.KnowledgeBase;

public sealed class KnowledgeArticle : BaseEntity
{
    public required string Title { get; set; }
    public required string Content { get; set; }
    public required KnowledgeArticleType Type { get; set; }
    public KnowledgeArticleStatus Status { get; set; } = KnowledgeArticleStatus.Draft;
    public string? Category { get; set; }
    public string? Tags { get; set; }

    // Stamped by Story 22's PublishKnowledgeArticleCommand/UnpublishKnowledgeArticleCommand;
    // both remain null for every article created by this story.
    public DateTime? PublishedOn { get; set; }
    public Guid? PublishedBy { get; set; }
}
```

### 2 — Application layer

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/DTOs/KnowledgeArticleDto.cs`**

```csharp
using AzmCrm.Domain.Features.KnowledgeBase;

namespace AzmCrm.Application.Features.KnowledgeBase.DTOs;

public sealed record KnowledgeArticleDto(
    Guid Id, string Title, string Content, KnowledgeArticleType Type, KnowledgeArticleStatus Status,
    string? Category, string? Tags, DateTime? PublishedOn, Guid? PublishedBy,
    DateTime CreatedOn, DateTime? UpdatedOn);
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/DTOs/KnowledgeArticleListItemDto.cs`**

```csharp
using AzmCrm.Domain.Features.KnowledgeBase;

namespace AzmCrm.Application.Features.KnowledgeBase.DTOs;

public sealed record KnowledgeArticleListItemDto(
    Guid Id, string Title, KnowledgeArticleType Type, KnowledgeArticleStatus Status,
    string? Category, DateTime CreatedOn);
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/DTOs/CreateKnowledgeArticleRequest.cs`**

```csharp
using AzmCrm.Domain.Features.KnowledgeBase;

namespace AzmCrm.Application.Features.KnowledgeBase.DTOs;

public sealed record CreateKnowledgeArticleRequest(
    string Title, string Content, KnowledgeArticleType Type, string? Category, string? Tags);
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/DTOs/UpdateKnowledgeArticleRequest.cs`**

```csharp
using AzmCrm.Domain.Features.KnowledgeBase;

namespace AzmCrm.Application.Features.KnowledgeBase.DTOs;

public sealed record UpdateKnowledgeArticleRequest(
    string Title, string Content, KnowledgeArticleType Type, string? Category, string? Tags);
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/CreateKnowledgeArticle/CreateKnowledgeArticleCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.CreateKnowledgeArticle;

public sealed record CreateKnowledgeArticleCommand(
    string Title, string Content, KnowledgeArticleType Type, string? Category, string? Tags)
    : IRequest<Result<Guid>>;
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/CreateKnowledgeArticle/CreateKnowledgeArticleCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.CreateKnowledgeArticle;

internal sealed class CreateKnowledgeArticleCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreateKnowledgeArticleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateKnowledgeArticleCommand request, CancellationToken ct)
    {
        var article = new KnowledgeArticle
        {
            Title = request.Title,
            Content = request.Content,
            Type = request.Type,
            Category = request.Category,
            Tags = request.Tags
        };

        dbContext.KnowledgeArticles.Add(article);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(article.Id);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/CreateKnowledgeArticle/CreateKnowledgeArticleCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.CreateKnowledgeArticle;

public sealed class CreateKnowledgeArticleCommandValidator : AbstractValidator<CreateKnowledgeArticleCommand>
{
    public CreateKnowledgeArticleCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Title"])
            .MaximumLength(300).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Title", 300]);

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Content"])
            .MaximumLength(8000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Content", 8000]);

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage(localization[LocalizationKeys.Validation.InvalidValue, "Type"]);

        RuleFor(x => x.Category)
            .MaximumLength(100).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Category", 100]);

        RuleFor(x => x.Tags)
            .MaximumLength(500).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Tags", 500]);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/UpdateKnowledgeArticle/UpdateKnowledgeArticleCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.UpdateKnowledgeArticle;

public sealed record UpdateKnowledgeArticleCommand(
    Guid Id, string Title, string Content, KnowledgeArticleType Type, string? Category, string? Tags)
    : IRequest<Result>;
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/UpdateKnowledgeArticle/UpdateKnowledgeArticleCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.UpdateKnowledgeArticle;

internal sealed class UpdateKnowledgeArticleCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UpdateKnowledgeArticleCommand, Result>
{
    public async Task<Result> Handle(UpdateKnowledgeArticleCommand request, CancellationToken ct)
    {
        var article = await dbContext.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new NotFoundException($"Knowledge article '{request.Id}' was not found.");

        article.Title = request.Title;
        article.Content = request.Content;
        article.Type = request.Type;
        article.Category = request.Category;
        article.Tags = request.Tags;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/UpdateKnowledgeArticle/UpdateKnowledgeArticleCommandValidator.cs`** — same rules as `CreateKnowledgeArticleCommandValidator` plus a leading `RuleFor(x => x.Id).NotEmpty()...`, following [UpdateQuickReplyTemplateCommandValidator.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Commands/UpdateQuickReplyTemplate/UpdateQuickReplyTemplateCommandValidator.cs)'s shape exactly.

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/DeleteKnowledgeArticle/DeleteKnowledgeArticleCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.DeleteKnowledgeArticle;

public sealed record DeleteKnowledgeArticleCommand(Guid Id) : IRequest<Result>;
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/DeleteKnowledgeArticle/DeleteKnowledgeArticleCommandHandler.cs`** — copy [DeleteQuickReplyTemplateCommandHandler.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Commands/DeleteQuickReplyTemplate/DeleteQuickReplyTemplateCommandHandler.cs) exactly, substituting `dbContext.KnowledgeArticles`/`Knowledge article '{request.Id}'` and the `ICurrentUserService`-based `DeletedBy` stamp.

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/DeleteKnowledgeArticle/DeleteKnowledgeArticleCommandValidator.cs`** — copy [DeleteQuickReplyTemplateCommandValidator.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Commands/DeleteQuickReplyTemplate/DeleteQuickReplyTemplateCommandValidator.cs) exactly.

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Queries/GetKnowledgeArticleById/GetKnowledgeArticleByIdQuery.cs`**

```csharp
using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Queries.GetKnowledgeArticleById;

public sealed record GetKnowledgeArticleByIdQuery(Guid Id) : IRequest<Result<KnowledgeArticleDto>>;
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Queries/GetKnowledgeArticleById/GetKnowledgeArticleByIdQueryHandler.cs`**

```csharp
using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.KnowledgeBase.Queries.GetKnowledgeArticleById;

internal sealed class GetKnowledgeArticleByIdQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetKnowledgeArticleByIdQuery, Result<KnowledgeArticleDto>>
{
    public async Task<Result<KnowledgeArticleDto>> Handle(
        GetKnowledgeArticleByIdQuery request, CancellationToken ct)
    {
        var article = await dbContext.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new NotFoundException($"Knowledge article '{request.Id}' was not found.");

        var dto = new KnowledgeArticleDto(
            article.Id, article.Title, article.Content, article.Type, article.Status,
            article.Category, article.Tags, article.PublishedOn, article.PublishedBy,
            article.CreatedOn, article.UpdatedOn);

        return Result<KnowledgeArticleDto>.Success(dto);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Queries/GetKnowledgeArticlesList/GetKnowledgeArticlesListQuery.cs`**

```csharp
using AzmCrm.Application.Features.KnowledgeBase.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Queries.GetKnowledgeArticlesList;

public sealed record GetKnowledgeArticlesListQuery(
    int PageNumber = 1, int PageSize = 20,
    KnowledgeArticleType? Type = null, KnowledgeArticleStatus? Status = null, string? Category = null
) : IRequest<Result<PaginatedResult<KnowledgeArticleListItemDto>>>;
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Queries/GetKnowledgeArticlesList/GetKnowledgeArticlesListQueryHandler.cs`** — same shape as [GetSlaPoliciesListQueryHandler.cs](../../../src/AzmCrm.Application/Features/Sla/Queries/GetSlaPoliciesList/GetSlaPoliciesListQueryHandler.cs): `AsQueryable()` on `dbContext.KnowledgeArticles`, then three sequential `if (request.X is not null) query = query.Where(...)` filters for `Type`, `Status`, and `Category` (`a => a.Category == request.Category`), ordered `.OrderByDescending(a => a.CreatedOn)` (newest first — a management list of freshly authored/edited content, unlike SLA's small fixed-size priority-keyed list), then projected into `KnowledgeArticleListItemDto`.

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Queries/GetKnowledgeArticlesList/GetKnowledgeArticlesListQueryValidator.cs`** — copy [GetQuickReplyTemplatesListQueryValidator.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Queries/GetQuickReplyTemplatesList/GetQuickReplyTemplatesListQueryValidator.cs)'s `PageNumber`/`PageSize` rules exactly.

**Edit file: `src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs`** — add `using AzmCrm.Domain.Features.KnowledgeBase;` to the usings, and add after `DbSet<EscalationRule> EscalationRules { get; }`:

```csharp
DbSet<KnowledgeArticle> KnowledgeArticles { get; }
```

### 3 — Infrastructure layer

**Create file: `src/AzmCrm.Infrastructure/Data/Configurations/KnowledgeArticleConfiguration.cs`**

```csharp
using AzmCrm.Domain.Features.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class KnowledgeArticleConfiguration : IEntityTypeConfiguration<KnowledgeArticle>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticle> builder)
    {
        builder.ToTable("KnowledgeArticles");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .ValueGeneratedNever();

        builder.Property(a => a.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(a => a.Content)
            .IsRequired()
            .HasMaxLength(8000);

        builder.Property(a => a.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Category)
            .HasMaxLength(100);

        builder.Property(a => a.Tags)
            .HasMaxLength(500);

        builder.HasQueryFilter(a => !a.IsDeleted);

        builder.HasIndex(a => a.Type);
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.Category);
    }
}
```

**Edit file: `src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs`** — add `using AzmCrm.Domain.Features.KnowledgeBase;` and, after `public DbSet<EscalationRule> EscalationRules => Set<EscalationRule>();`:

```csharp
public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();
```

**Generate migration:**

```bash
dotnet ef migrations add AddKnowledgeArticles --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API
```

### 4 — API layer

**Create file: `src/AzmCrm.API/Controllers/KnowledgeArticlesController.cs`** — copy [QuickReplyTemplatesController.cs](../../../src/AzmCrm.API/Controllers/QuickReplyTemplatesController.cs)'s exact shape: `[Route("api/knowledge-articles")]` (same kebab-case-override reasoning as that file's comment), `Create`/`GetById`/`GetList`/`Update`/`Delete` actions wired to the commands/query above. `GetList` takes `[FromQuery] KnowledgeArticleType? type`, `[FromQuery] KnowledgeArticleStatus? status`, `[FromQuery] string? category` instead of `search`.

### 5 — Test doubles

**Edit file: `tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs`** — add `using AzmCrm.Domain.Features.KnowledgeBase;`, add `public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();` after the `EscalationRules` line, and add `modelBuilder.Entity<KnowledgeArticle>().HasQueryFilter(a => !a.IsDeleted);` after the `EscalationRule` query filter line.

## Edge Cases & Failure Modes

- **`Type` omitted or an out-of-range enum value** — rejected by `CreateKnowledgeArticleCommandValidator`/`UpdateKnowledgeArticleCommandValidator`'s `IsInEnum()` rule before the command reaches the handler, matching `CreateSlaPolicyCommandValidator`'s `Priority` rule from Story 17.
- **`Category`/`Tags` left `null`** — both are optional (`string?`); `KnowledgeArticleConfiguration` does not mark either `.IsRequired()`, so a `null` value persists as a `NULL` column, and `GetKnowledgeArticlesListQueryHandler`'s `Category` filter is simply skipped when the request omits it (`request.Category is not null` guard).
- **Creating an article with `Type = Guide` but no steps attached** — allowed by this story; `KnowledgeArticleType.Guide` is just an enum value here with no FK/child-table enforcement. Story 23 is what actually lets an agent attach ordered `KnowledgeArticleStep` rows; a `Guide` with zero steps is a valid (if incomplete) row in both stories.
- **Updating `Type` on an article that already has Story 22 publish state or Story 23 steps** — allowed unconditionally by `UpdateKnowledgeArticleCommandHandler` in this story; there is no guard preventing, e.g., flipping a `Published` `Guide` (with steps) to `Faq`. This is a deliberate, documented gap — flagged here as a follow-up for Story 22/23 to consider, mirroring KAN-5 Story 17's identical "Not in scope" note about `Ticket.Priority` changes not re-stamping SLA dates.
- **Deleting (`DeleteKnowledgeArticleCommand`) an article** — performs the same **soft** delete every other aggregate in this codebase uses (`IsDeleted = true`, `DeletedBy`/`DeletedOn` stamped); `KnowledgeArticleConfiguration`'s `HasQueryFilter(a => !a.IsDeleted)` then hides it from every query (`GetKnowledgeArticlesList`, `GetKnowledgeArticleById`) automatically, including a subsequent `GetKnowledgeArticleByIdQuery` on its `Id`, which now throws `NotFoundException` exactly like any other missing row.
- **`Content` longer than 8000 characters** — rejected by the validator's `MaximumLength(8000)` rule before persistence; `KnowledgeArticleConfiguration.Content` mirrors the same cap at the database column level (`HasMaxLength(8000)`), so no oversized value can reach the database even if a caller bypassed the validator.
- **No authenticated user context on create/delete** — `BaseEntity.CreatedBy`/`CreatedOn` are stamped by `ApplicationDbContext.SaveChangesAsync`'s existing `EntityState.Added` branch using `_currentUserService.UserId ?? Guid.Empty`, identical to every other aggregate; `DeleteKnowledgeArticleCommandHandler` explicitly stamps `DeletedBy = currentUserService.UserId ?? Guid.Empty` the same way `DeleteQuickReplyTemplateCommandHandler` does — no new failure mode introduced by this story.

## Test Plan

1. **Create file: `tests/AzmCrm.Application.Tests/Features/KnowledgeBase/CreateKnowledgeArticleCommandHandlerTests.cs`** — `Create_persists_article_as_Draft_and_returns_id` (asserts `Status == KnowledgeArticleStatus.Draft` and `PublishedOn`/`PublishedBy` are both `null`); `Create_with_null_Category_and_Tags_succeeds`.
2. **Create file: `tests/AzmCrm.Application.Tests/Features/KnowledgeBase/UpdateKnowledgeArticleCommandHandlerTests.cs`** — `Update_persists_changes`; `Update_missing_article_throws_NotFoundException`.
3. **Create file: `tests/AzmCrm.Application.Tests/Features/KnowledgeBase/DeleteKnowledgeArticleCommandHandlerTests.cs`** — `Delete_soft_deletes_article`; `Delete_missing_article_throws_NotFoundException`.
4. **Create file: `tests/AzmCrm.Application.Tests/Features/KnowledgeBase/GetKnowledgeArticleByIdQueryHandlerTests.cs`** — `GetById_returns_article`; `GetById_missing_article_throws_NotFoundException`; `GetById_soft_deleted_article_throws_NotFoundException` (confirms the query filter hides it).
5. **Create file: `tests/AzmCrm.Application.Tests/Features/KnowledgeBase/GetKnowledgeArticlesListQueryHandlerTests.cs`** — `List_returns_all_articles_ordered_by_CreatedOn_descending`; `List_filters_by_Type`; `List_filters_by_Status`; `List_filters_by_Category`.
6. **Create file: `tests/AzmCrm.Application.Tests/Features/KnowledgeBase/CreateKnowledgeArticleCommandValidatorTests.cs`** — `Undefined_Type_fails`; `Title_exceeding_300_chars_fails`; `Content_exceeding_8000_chars_fails`; `Valid_command_passes`.
7. All new tests use `TestApplicationDbContext.Create()` and `StubLocalizationService` exactly as established in prior stories — no new test doubles are needed.

## Migration / Rollback

- The migration generated in Task 3 **adds** a new, standalone `KnowledgeArticles` table (no FK to any existing table) plus three indexes — purely additive, safe on top of the latest existing migration (`AddSlaBreachNotifications`).
- **Rollback**: `dotnet ef database update AddSlaBreachNotifications --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` drops the `KnowledgeArticles` table entirely.
- **Half-applied state**: same existing behavior — `DatabaseInitializer` logs and rethrows on migration failure, so the app fails to start rather than running against a partial schema.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Migration applies cleanly:** `dotnet ef database update --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` (or let the API apply it automatically on startup).
4. **Manual smoke test:** `POST /api/knowledge-articles` with `{"title":"How do I reset my password?","content":"Go to Settings > Security > Reset Password.","type":"Faq","category":"Account","tags":"password,reset,login"}`, confirm 201 and the response's `status` is `"Draft"`; `GET /api/knowledge-articles/{id}` returns the same data; `GET /api/knowledge-articles?type=Faq` includes it; `PUT /api/knowledge-articles/{id}` with a changed `title` and confirm the change via a follow-up `GET`; `DELETE /api/knowledge-articles/{id}` then confirm a follow-up `GET /api/knowledge-articles/{id}` returns 404.

## Done Criteria

- [ ] `KnowledgeArticle`/`KnowledgeArticleType`/`KnowledgeArticleStatus`, EF configuration, and migration exist and apply cleanly on top of `AddSlaBreachNotifications`.
- [ ] `POST/PUT/DELETE /api/knowledge-articles` and `GET /api/knowledge-articles`, `GET /api/knowledge-articles/{id}` work end to end.
- [ ] `GET /api/knowledge-articles` filters correctly by `type`, `status`, and `category`.
- [ ] Every new article defaults to `Status = Draft` with `PublishedOn`/`PublishedBy` both `null`.
- [ ] Deleting an article soft-deletes it and hides it from every subsequent query.
- [ ] All new handler and validator unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

This story satisfies KAN-6's "Create and manage FAQ entries" acceptance criterion and creates the single `KnowledgeArticle` aggregate Stories 22-24 extend to satisfy the remaining three.
