# Story 23 — Step-by-Step Guide Steps (Story: KAN-6)

## Prerequisites

- [21-story-knowledge-base-core-crud-KAN-6.md](21-story-knowledge-base-core-crud-KAN-6.md) completed: requires `KnowledgeArticle`/`KnowledgeArticleType`, `KnowledgeArticleDto`, `KnowledgeArticlesController`, and `IApplicationDbContext.KnowledgeArticles`.
- [22-story-knowledge-article-publishing-KAN-6.md](22-story-knowledge-article-publishing-KAN-6.md) completed: requires `KnowledgeArticlePublicDto` and the `[AllowAnonymous]` `GET /api/knowledge-articles/published/{id}` action — this story appends a trailing `Steps` field to that DTO so a published guide's steps are visible to the same customer-facing endpoint, the same "extend the shared DTO with a trailing field" pattern [17-story-sla-policies-KAN-5.md](17-story-sla-policies-KAN-5.md) used on `TicketDto`/`TicketListItemDto`.
- [04-story-customer-attachments-KAN-1.md](04-story-customer-attachments-KAN-1.md) — read only for its precedent of a child aggregate (`CustomerAttachment`) with a required FK back to a parent (`CustomerId`) and cascade delete; `KnowledgeArticleStep`/`KnowledgeArticleId` follows the identical FK/cascade shape.

## Story Goal

Let an agent attach an ordered list of steps to a `KnowledgeArticle` (typically, but not exclusively, one with `Type = Guide`), satisfying KAN-6's "Provide solutions and step-by-step guides" acceptance criterion.

Outcomes:
1. A new child entity `KnowledgeArticleStep` (`KnowledgeArticleId`, `StepNumber`, `Title`, `Description`) attaches to any `KnowledgeArticle`; `POST /api/knowledge-articles/{id}/steps` appends one, `PUT /api/knowledge-articles/{id}/steps/{stepId}` edits one, `DELETE /api/knowledge-articles/{id}/steps/{stepId}` removes one.
2. `KnowledgeArticleDto` (Story 21, agent-facing) and `KnowledgeArticlePublicDto` (Story 22, customer-facing) both gain a trailing `IReadOnlyList<KnowledgeArticleStepDto> Steps` field, populated by `GetKnowledgeArticleByIdQueryHandler`/`GetPublishedKnowledgeArticleByIdQueryHandler` ordered by `StepNumber` ascending — a guide's steps are only ever read as part of fetching the guide itself; there is no standalone `GET .../steps` list endpoint, since every caller that wants steps already has (or is about to fetch) the parent article.
3. Attaching steps to a non-`Guide` article (`Faq` or `Article`) is allowed, not rejected — see Story 21's Story Goal note that `Type` can already be changed freely after creation with no cross-field enforcement; this story does not introduce the first such enforcement rule either, for consistency.

**Not in scope**: reordering steps via a dedicated "reorder" endpoint (an agent reorders by deleting and re-adding, or by editing each step's own content while `StepNumber` stays whatever was supplied — no automatic renumbering, no uniqueness constraint on `StepNumber` within an article, and no gap-filling); step attachments/images; marking a step as optional/conditional; and step completion tracking for the reading customer.

## Context — Read These Files First

1. [21-story-knowledge-base-core-crud-KAN-6.md](21-story-knowledge-base-core-crud-KAN-6.md) — read in full for `KnowledgeArticle`'s exact shape, `KnowledgeArticleDto`'s current field list, and `KnowledgeArticlesController`'s current action set this story appends to.
2. [22-story-knowledge-article-publishing-KAN-6.md](22-story-knowledge-article-publishing-KAN-6.md) — read in full for `KnowledgeArticlePublicDto`'s current field list and `GetPublishedKnowledgeArticleByIdQueryHandler`'s current body this story edits.
3. [src/AzmCrm.Domain/Features/Customers/CustomerNote.cs](../../../src/AzmCrm.Domain/Features/Customers/CustomerNote.cs) (9 lines, read in full) — the exact required-FK-plus-navigation-property shape `KnowledgeArticleStep` follows (`CustomerId`/`Customer` there maps to `KnowledgeArticleId`/`KnowledgeArticle` here), substituting a `StepNumber`/`Title` pair for the single `Content` field.
4. [src/AzmCrm.Infrastructure/Data/Configurations/CustomerNoteConfiguration.cs](../../../src/AzmCrm.Infrastructure/Data/Configurations/CustomerNoteConfiguration.cs) (26 lines, read in full) — the exact `HasOne(...).WithMany().HasForeignKey(...).OnDelete(DeleteBehavior.Cascade)` shape `KnowledgeArticleStepConfiguration` follows; cascade delete here means deleting (soft-deleting, per this codebase's convention) the parent article does not orphan its steps at the database level, though in practice this codebase never hard-deletes (see Edge Cases).
5. [src/AzmCrm.Application/Features/Tickets/Commands/CreateTicketComment/CreateTicketCommentCommandHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Commands/CreateTicketComment/CreateTicketCommentCommandHandler.cs) — read in full; the `var parentExists = await dbContext.X.AnyAsync(...); if (!parentExists) throw new NotFoundException(...)` shape `AddKnowledgeArticleStepCommandHandler` follows to validate `KnowledgeArticleId` before inserting a step.
6. [src/AzmCrm.Application/Features/Tickets/DTOs/TicketDto.cs](../../../src/AzmCrm.Application/Features/Tickets/DTOs/TicketDto.cs) — read as the precedent for appending trailing fields to an already-shipped `record` DTO (KAN-5 Story 17 appended four trailing `DateTime?`/`Guid?` params here); this story appends one trailing `IReadOnlyList<KnowledgeArticleStepDto> Steps` param to `KnowledgeArticleDto` and `KnowledgeArticlePublicDto` the same way.

## Implementation tasks

### 1 — Domain layer

**Create file: `src/AzmCrm.Domain/Features/KnowledgeBase/KnowledgeArticleStep.cs`**

```csharp
using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.KnowledgeBase;

public sealed class KnowledgeArticleStep : BaseEntity
{
    public required Guid KnowledgeArticleId { get; init; }
    public required int StepNumber { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }

    public KnowledgeArticle KnowledgeArticle { get; init; } = null!;
}
```

### 2 — Application layer

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/DTOs/KnowledgeArticleStepDto.cs`**

```csharp
namespace AzmCrm.Application.Features.KnowledgeBase.DTOs;

public sealed record KnowledgeArticleStepDto(Guid Id, int StepNumber, string Title, string Description);
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/DTOs/AddKnowledgeArticleStepRequest.cs`**

```csharp
namespace AzmCrm.Application.Features.KnowledgeBase.DTOs;

public sealed record AddKnowledgeArticleStepRequest(int StepNumber, string Title, string Description);
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/DTOs/UpdateKnowledgeArticleStepRequest.cs`**

```csharp
namespace AzmCrm.Application.Features.KnowledgeBase.DTOs;

public sealed record UpdateKnowledgeArticleStepRequest(int StepNumber, string Title, string Description);
```

**Edit file: `src/AzmCrm.Application/Features/KnowledgeBase/DTOs/KnowledgeArticleDto.cs`** (Story 21) — add `using System.Collections.Generic;` if not already implied, and append a trailing parameter:

```csharp
public sealed record KnowledgeArticleDto(
    Guid Id, string Title, string Content, KnowledgeArticleType Type, KnowledgeArticleStatus Status,
    string? Category, string? Tags, DateTime? PublishedOn, Guid? PublishedBy,
    DateTime CreatedOn, DateTime? UpdatedOn, IReadOnlyList<KnowledgeArticleStepDto> Steps);
```

**Edit file: `src/AzmCrm.Application/Features/KnowledgeBase/DTOs/KnowledgeArticlePublicDto.cs`** (Story 22) — append the same trailing parameter:

```csharp
public sealed record KnowledgeArticlePublicDto(
    Guid Id, string Title, string Content, KnowledgeArticleType Type,
    string? Category, string? Tags, DateTime? PublishedOn, IReadOnlyList<KnowledgeArticleStepDto> Steps);
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/AddKnowledgeArticleStep/AddKnowledgeArticleStepCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.AddKnowledgeArticleStep;

public sealed record AddKnowledgeArticleStepCommand(
    Guid KnowledgeArticleId, int StepNumber, string Title, string Description) : IRequest<Result<Guid>>;
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/AddKnowledgeArticleStep/AddKnowledgeArticleStepCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.AddKnowledgeArticleStep;

internal sealed class AddKnowledgeArticleStepCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<AddKnowledgeArticleStepCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(AddKnowledgeArticleStepCommand request, CancellationToken ct)
    {
        var articleExists = await dbContext.KnowledgeArticles.AnyAsync(a => a.Id == request.KnowledgeArticleId, ct);
        if (!articleExists)
            throw new NotFoundException($"Knowledge article '{request.KnowledgeArticleId}' was not found.");

        var step = new KnowledgeArticleStep
        {
            KnowledgeArticleId = request.KnowledgeArticleId,
            StepNumber = request.StepNumber,
            Title = request.Title,
            Description = request.Description
        };

        dbContext.KnowledgeArticleSteps.Add(step);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(step.Id);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/AddKnowledgeArticleStep/AddKnowledgeArticleStepCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.AddKnowledgeArticleStep;

public sealed class AddKnowledgeArticleStepCommandValidator : AbstractValidator<AddKnowledgeArticleStepCommand>
{
    public AddKnowledgeArticleStepCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.KnowledgeArticleId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Knowledge Article Id"]);

        RuleFor(x => x.StepNumber)
            .GreaterThan(0)
            .WithMessage(localization[LocalizationKeys.Validation.MustBeGreaterThan, "Step Number", 0]);

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Title"])
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Title", 200]);

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Description"])
            .MaximumLength(4000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Description", 4000]);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/UpdateKnowledgeArticleStep/UpdateKnowledgeArticleStepCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.UpdateKnowledgeArticleStep;

public sealed record UpdateKnowledgeArticleStepCommand(
    Guid KnowledgeArticleId, Guid StepId, int StepNumber, string Title, string Description) : IRequest<Result>;
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/UpdateKnowledgeArticleStep/UpdateKnowledgeArticleStepCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.UpdateKnowledgeArticleStep;

internal sealed class UpdateKnowledgeArticleStepCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UpdateKnowledgeArticleStepCommand, Result>
{
    public async Task<Result> Handle(UpdateKnowledgeArticleStepCommand request, CancellationToken ct)
    {
        var step = await dbContext.KnowledgeArticleSteps
            .FirstOrDefaultAsync(s => s.Id == request.StepId && s.KnowledgeArticleId == request.KnowledgeArticleId, ct)
            ?? throw new NotFoundException($"Step '{request.StepId}' was not found.");

        step.StepNumber = request.StepNumber;
        step.Title = request.Title;
        step.Description = request.Description;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/UpdateKnowledgeArticleStep/UpdateKnowledgeArticleStepCommandValidator.cs`** — same rules as `AddKnowledgeArticleStepCommandValidator` plus `RuleFor(x => x.StepId).NotEmpty()...`.

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/DeleteKnowledgeArticleStep/DeleteKnowledgeArticleStepCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.DeleteKnowledgeArticleStep;

public sealed record DeleteKnowledgeArticleStepCommand(Guid KnowledgeArticleId, Guid StepId) : IRequest<Result>;
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/DeleteKnowledgeArticleStep/DeleteKnowledgeArticleStepCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.DeleteKnowledgeArticleStep;

internal sealed class DeleteKnowledgeArticleStepCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<DeleteKnowledgeArticleStepCommand, Result>
{
    public async Task<Result> Handle(DeleteKnowledgeArticleStepCommand request, CancellationToken ct)
    {
        var step = await dbContext.KnowledgeArticleSteps
            .FirstOrDefaultAsync(s => s.Id == request.StepId && s.KnowledgeArticleId == request.KnowledgeArticleId, ct)
            ?? throw new NotFoundException($"Step '{request.StepId}' was not found.");

        step.IsDeleted = true;
        step.DeletedBy = currentUserService.UserId ?? Guid.Empty;
        step.DeletedOn = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

**Create file: `src/AzmCrm.Application/Features/KnowledgeBase/Commands/DeleteKnowledgeArticleStep/DeleteKnowledgeArticleStepCommandValidator.cs`** — `RuleFor(x => x.KnowledgeArticleId).NotEmpty()...` and `RuleFor(x => x.StepId).NotEmpty()...`.

**Edit file: `src/AzmCrm.Application/Features/KnowledgeBase/Queries/GetKnowledgeArticleById/GetKnowledgeArticleByIdQueryHandler.cs`** (Story 21) — load and project the article's steps before constructing the `KnowledgeArticleDto`:

```csharp
var steps = await dbContext.KnowledgeArticleSteps
    .Where(s => s.KnowledgeArticleId == article.Id)
    .OrderBy(s => s.StepNumber)
    .Select(s => new KnowledgeArticleStepDto(s.Id, s.StepNumber, s.Title, s.Description))
    .ToListAsync(ct);

var dto = new KnowledgeArticleDto(
    article.Id, article.Title, article.Content, article.Type, article.Status,
    article.Category, article.Tags, article.PublishedOn, article.PublishedBy,
    article.CreatedOn, article.UpdatedOn, steps);
```

**Edit file: `src/AzmCrm.Application/Features/KnowledgeBase/Queries/GetPublishedKnowledgeArticleById/GetPublishedKnowledgeArticleByIdQueryHandler.cs`** (Story 22) — same `steps` query as above, appended as the trailing argument to `KnowledgeArticlePublicDto`'s constructor call.

**Edit file: `src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs`** — add, after `DbSet<KnowledgeArticle> KnowledgeArticles { get; }`:

```csharp
DbSet<KnowledgeArticleStep> KnowledgeArticleSteps { get; }
```

### 3 — Infrastructure layer

**Create file: `src/AzmCrm.Infrastructure/Data/Configurations/KnowledgeArticleStepConfiguration.cs`**

```csharp
using AzmCrm.Domain.Features.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class KnowledgeArticleStepConfiguration : IEntityTypeConfiguration<KnowledgeArticleStep>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticleStep> builder)
    {
        builder.ToTable("KnowledgeArticleSteps");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedNever();

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Description)
            .IsRequired()
            .HasMaxLength(4000);

        builder.HasOne(s => s.KnowledgeArticle)
            .WithMany()
            .HasForeignKey(s => s.KnowledgeArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(s => !s.IsDeleted);

        builder.HasIndex(s => s.KnowledgeArticleId);
    }
}
```

**Edit file: `src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs`** — add, after `public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();`:

```csharp
public DbSet<KnowledgeArticleStep> KnowledgeArticleSteps => Set<KnowledgeArticleStep>();
```

**Generate migration:**

```bash
dotnet ef migrations add AddKnowledgeArticleSteps --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API
```

### 4 — API layer

**Edit file: `src/AzmCrm.API/Controllers/KnowledgeArticlesController.cs`** — add three nested actions:

```csharp
[HttpPost("{id:guid}/steps")]
[ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> AddStep(Guid id, [FromBody] AddKnowledgeArticleStepRequest request, CancellationToken ct)
{
    var result = await mediator.Send(
        new AddKnowledgeArticleStepCommand(id, request.StepNumber, request.Title, request.Description), ct);
    return ToCreatedResult(result, stepId => $"/api/knowledge-articles/{id}/steps/{stepId}");
}

[HttpPut("{id:guid}/steps/{stepId:guid}")]
[ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> UpdateStep(
    Guid id, Guid stepId, [FromBody] UpdateKnowledgeArticleStepRequest request, CancellationToken ct)
{
    var result = await mediator.Send(
        new UpdateKnowledgeArticleStepCommand(id, stepId, request.StepNumber, request.Title, request.Description), ct);
    return ToResult(result);
}

[HttpDelete("{id:guid}/steps/{stepId:guid}")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> DeleteStep(Guid id, Guid stepId, CancellationToken ct)
{
    var result = await mediator.Send(new DeleteKnowledgeArticleStepCommand(id, stepId), ct);
    return ToNoContentResult(result);
}
```

Add the corresponding `using AzmCrm.Application.Features.KnowledgeBase.Commands.AddKnowledgeArticleStep;`, `...Commands.UpdateKnowledgeArticleStep;`, and `...Commands.DeleteKnowledgeArticleStep;` lines.

### 5 — Test doubles

**Edit file: `tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs`** — add `public DbSet<KnowledgeArticleStep> KnowledgeArticleSteps => Set<KnowledgeArticleStep>();` after the `KnowledgeArticles` line, and `modelBuilder.Entity<KnowledgeArticleStep>().HasQueryFilter(s => !s.IsDeleted);` after the `KnowledgeArticle` query filter line.

## Edge Cases & Failure Modes

- **Two steps with the same `StepNumber` on the same article** — allowed; no uniqueness constraint exists at either the validator or database level. `GetKnowledgeArticleByIdQueryHandler`'s `.OrderBy(s => s.StepNumber)` produces a stable-but-arbitrary relative order between ties (EF Core/PostgreSQL do not guarantee a secondary sort key here). Documented as an accepted gap — an agent authoring steps is expected to number them uniquely and sequentially, but nothing enforces it.
- **A gap in `StepNumber` (e.g. steps numbered 1, 2, 5)** — allowed; steps are stored and returned in whatever `StepNumber` order they end up in, gaps included. No renumbering/compaction logic exists.
- **Adding a step to a nonexistent `KnowledgeArticleId`** — `AddKnowledgeArticleStepCommandHandler`'s `articleExists` guard throws `NotFoundException` before any `KnowledgeArticleStep` row is constructed, mirroring `CreateTicketCommentCommandHandler`'s identical parent-existence check.
- **Updating/deleting a step with a `stepId` that exists but belongs to a *different* article than the `id` in the route** — `UpdateKnowledgeArticleStepCommandHandler`/`DeleteKnowledgeArticleStepCommandHandler`'s combined `s.Id == request.StepId && s.KnowledgeArticleId == request.KnowledgeArticleId` predicate returns no row in that case, so it 404s rather than silently operating on (or leaking the existence of) another article's step.
- **Adding a step to a `Faq`/`Article`-typed (non-`Guide`) article** — allowed without restriction; see Story Goal outcome 3. This is a deliberate, documented non-enforcement, matching Story 21's Story Goal note that `Type` itself is freely mutable post-creation with no cross-field validation anywhere in this feature.
- **Fetching a `KnowledgeArticle`/published article with zero steps** — `Steps` in both `KnowledgeArticleDto` and `KnowledgeArticlePublicDto` is simply an empty list (`IReadOnlyList<KnowledgeArticleStepDto>` with `Count == 0`), never `null` — the `.Select(...).ToListAsync(ct)` projection always returns a (possibly empty) `List<T>`, matching how every other paginated/list projection in this codebase behaves.
- **Deleting (`DeleteKnowledgeArticleStepCommand`) a step** — soft-deletes it (`IsDeleted = true`) exactly like every other aggregate in this codebase; `KnowledgeArticleStepConfiguration`'s `HasQueryFilter(s => !s.IsDeleted)` then hides it from the `Steps` projection on both `GetKnowledgeArticleById` and `GetPublishedKnowledgeArticleById`. The database-level `OnDelete(DeleteBehavior.Cascade)` FK never actually fires in normal operation, since Story 21's `DeleteKnowledgeArticleCommandHandler` also only soft-deletes the parent — a hard delete of `KnowledgeArticle` (never exposed by any command in this feature) is the only path that would trigger it.

## Test Plan

1. **Create file: `tests/AzmCrm.Application.Tests/Features/KnowledgeBase/AddKnowledgeArticleStepCommandHandlerTests.cs`** — `Add_persists_step_and_returns_id`; `Add_to_missing_article_throws_NotFoundException`.
2. **Create file: `tests/AzmCrm.Application.Tests/Features/KnowledgeBase/UpdateKnowledgeArticleStepCommandHandlerTests.cs`** — `Update_persists_changes`; `Update_missing_step_throws_NotFoundException`; `Update_step_belonging_to_different_article_throws_NotFoundException`.
3. **Create file: `tests/AzmCrm.Application.Tests/Features/KnowledgeBase/DeleteKnowledgeArticleStepCommandHandlerTests.cs`** — `Delete_soft_deletes_step`; `Delete_missing_step_throws_NotFoundException`.
4. **Edit `tests/AzmCrm.Application.Tests/Features/KnowledgeBase/GetKnowledgeArticleByIdQueryHandlerTests.cs`** (Story 21) — add `GetById_returns_steps_ordered_by_StepNumber_ascending`; add `GetById_with_no_steps_returns_empty_Steps_list`.
5. **Edit `tests/AzmCrm.Application.Tests/Features/KnowledgeBase/GetPublishedKnowledgeArticleByIdQueryHandlerTests.cs`** (Story 22) — add `GetPublishedById_returns_steps_ordered_by_StepNumber_ascending`.
6. All new tests use `TestApplicationDbContext.Create()` and `StubLocalizationService` exactly as established in prior stories — no new test doubles are needed.

## Migration / Rollback

- The migration generated in Task 3 **adds** a new `KnowledgeArticleSteps` table with an FK to `KnowledgeArticles` plus one index — additive, safe on top of `AddKnowledgeArticles` (Story 21).
- **Rollback**: `dotnet ef database update AddKnowledgeArticles --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` drops the `KnowledgeArticleSteps` table and its FK; `KnowledgeArticles` itself is untouched.
- **Half-applied state**: same existing behavior — `DatabaseInitializer` logs and rethrows on migration failure, so the app fails to start rather than running against a partial schema.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Migration applies cleanly:** `dotnet ef database update --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` (or let the API apply it automatically on startup).
4. **Manual smoke test:** Create a `Guide`-typed article (Story 21), `POST /api/knowledge-articles/{id}/steps` twice with `stepNumber` 1 and 2, confirm both 201; `GET /api/knowledge-articles/{id}` shows both steps in `Steps`, ordered 1 then 2; publish the article (Story 22), then `GET /api/knowledge-articles/published/{id}` (no auth token) also shows both steps; `DELETE /api/knowledge-articles/{id}/steps/{stepId}` for one of them, confirm a follow-up `GET` shows only the remaining step.

## Done Criteria

- [ ] `KnowledgeArticleStep` entity, EF configuration, and migration exist and apply cleanly on top of `AddKnowledgeArticles`.
- [ ] `POST/PUT/DELETE /api/knowledge-articles/{id}/steps[/...]` work end to end and validate the parent article's existence.
- [ ] `GET /api/knowledge-articles/{id}` (agent) and `GET /api/knowledge-articles/published/{id}` (customer) both return the article's steps, ordered by `StepNumber` ascending, defaulting to an empty list when none exist.
- [ ] Updating/deleting a step scoped to the wrong parent article 404s rather than operating cross-article.
- [ ] All new handler and validator unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

This story satisfies KAN-6's "Provide solutions and step-by-step guides" acceptance criterion.
