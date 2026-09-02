# Story 16 — Ticket Collaboration: Internal Comments (Story: KAN-4)

## Prerequisites

- [05-story-ticket-core-crud-KAN-2.md](05-story-ticket-core-crud-KAN-2.md) completed: requires the `Ticket` entity, `IApplicationDbContext.Tickets`, and `TicketsController` this story attaches new actions to.
- Editing `TicketsController.cs` here is additive only — this story does not touch any action added by [06-story-ticket-assignment-KAN-2.md](06-story-ticket-assignment-KAN-2.md) or [07-story-ticket-status-escalation-KAN-2.md](07-story-ticket-status-escalation-KAN-2.md); it can be implemented independently of [13-story-dashboard-core-tickets-customers-KAN-4.md](13-story-dashboard-core-tickets-customers-KAN-4.md), [14-story-agent-tasks-reminders-KAN-4.md](14-story-agent-tasks-reminders-KAN-4.md), and [15-story-quick-reply-templates-KAN-4.md](15-story-quick-reply-templates-KAN-4.md), and in any order relative to them.

## Story Goal

Let any support agent leave an internal comment on a ticket, visible to every other agent who opens that ticket, and see who wrote each comment — satisfying KAN-4's **"Collaborate with team members"** acceptance criterion. This mirrors KAN-1 Story 03's `CustomerNote` almost exactly, applied to `Ticket` instead of `Customer`, with one addition: each comment's author name is resolved for display, since "who on the team said this" is the point of a collaboration thread (a `CustomerNote` never needed this, since customer notes aren't a multi-agent conversation in the same way).

Outcomes:
1. `POST /api/tickets/{id}/comments` appends an internal comment to a ticket. The author is always the authenticated caller (`BaseEntity.CreatedBy`, auto-stamped by `ApplicationDbContext.SaveChangesAsync` — no separate "author" field is needed).
2. `GET /api/tickets/{id}/comments` returns a paginated list of a ticket's comments, **oldest first** (a running collaboration thread reads top-to-bottom, like a chat log — the same deliberate deviation from this codebase's newest-first list convention that KAN-3 Story 08 already established for conversation messages), each with the author's resolved display name.

**Not in scope**: editing or deleting a comment once posted (matching `CustomerNote`'s existing "notes are append-only" convention), @mentions or notifications when a teammate is mentioned, comment threading/replies, and comments visible to the customer (these are strictly internal/agent-only — there is no channel that ever surfaces a `TicketComment` to a `Conversation`/`Message`).

## Context — Read These Files First

1. [src/AzmCrm.Domain/Features/Customers/CustomerNote.cs](../../../src/AzmCrm.Domain/Features/Customers/CustomerNote.cs) — read in full (11 lines). `TicketComment` (Task 1) is the same shape with `TicketId`/`Ticket` in place of `CustomerId`/`Customer`.
2. [src/AzmCrm.Infrastructure/Data/Configurations/CustomerNoteConfiguration.cs](../../../src/AzmCrm.Infrastructure/Data/Configurations/CustomerNoteConfiguration.cs) — read in full (31 lines). Exact EF configuration shape `TicketCommentConfiguration` mirrors: `ToTable`, `HasKey`, `Property(...).ValueGeneratedNever()`, `Property(Content).IsRequired().HasMaxLength(4000)`, `HasOne(...).WithMany().HasForeignKey(...).OnDelete(Cascade)`, `HasQueryFilter`, `HasIndex`.
3. [src/AzmCrm.Application/Features/Customers/Commands/CreateCustomerNote/CreateCustomerNoteCommand.cs](../../../src/AzmCrm.Application/Features/Customers/Commands/CreateCustomerNote/CreateCustomerNoteCommand.cs), [CreateCustomerNoteCommandHandler.cs](../../../src/AzmCrm.Application/Features/Customers/Commands/CreateCustomerNote/CreateCustomerNoteCommandHandler.cs), and [CreateCustomerNoteCommandValidator.cs](../../../src/AzmCrm.Application/Features/Customers/Commands/CreateCustomerNote/CreateCustomerNoteCommandValidator.cs) — read all three in full. `CreateTicketCommentCommand`/`Handler`/`Validator` (Task 2) are these three files with `Customer(s)` replaced by `Ticket(s)` throughout — identical "verify parent exists via `AnyAsync`, else `NotFoundException`, then construct-and-save" shape.
4. [src/AzmCrm.Application/Features/Customers/Queries/GetCustomerNotes/GetCustomerNotesQuery.cs](../../../src/AzmCrm.Application/Features/Customers/Queries/GetCustomerNotes/GetCustomerNotesQuery.cs), [GetCustomerNotesQueryHandler.cs](../../../src/AzmCrm.Application/Features/Customers/Queries/GetCustomerNotes/GetCustomerNotesQueryHandler.cs), and [GetCustomerNotesQueryValidator.cs](../../../src/AzmCrm.Application/Features/Customers/Queries/GetCustomerNotes/GetCustomerNotesQueryValidator.cs) — read all three in full. `GetTicketCommentsQueryHandler` follows the same "verify parent exists, then paginated child list" shape **except** it orders `OrderBy(c => c.CreatedOn)` ascending, not descending (see Story Goal), and additionally batch-resolves each comment's author name.
5. [src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQueryHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQueryHandler.cs) — lines 54-64. The exact batch author-name-resolution pattern (`IIdentityQueryService.GetUsersInfoAsync` over a materialized page's distinct ids, then a dictionary lookup during DTO projection) `GetTicketCommentsQueryHandler` reuses, resolving `CreatedBy` instead of `AssignedToUserId`.
6. [src/AzmCrm.Application/Shared/Interfaces/IIdentityQueryService.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IIdentityQueryService.cs) — read in full (15 lines). Already registered as `services.AddScoped<IIdentityQueryService, IdentityQueryService>();` at [src/AzmCrm.Infrastructure/DependencyInjection.cs:106](../../../src/AzmCrm.Infrastructure/DependencyInjection.cs) — **no DI registration change needed**.
7. [src/AzmCrm.Domain/Common/BaseEntity.cs](../../../src/AzmCrm.Domain/Common/BaseEntity.cs) — read in full (19 lines). `CreatedBy` (auto-stamped by `ApplicationDbContext.SaveChangesAsync`) is the comment's author id — no separate `AuthorId` field on `TicketComment`, matching the reasoning KAN-3 Story 08 already documented for `Message.CreatedBy` (no bespoke "sent by" field needed).
8. [src/AzmCrm.API/Controllers/CustomersController.cs](../../../src/AzmCrm.API/Controllers/CustomersController.cs) — lines 107-128 (`AddNote`/`GetNotes`). Exact controller-action shape the two new `TicketsController` actions mirror.
9. [src/AzmCrm.API/Controllers/TicketsController.cs](../../../src/AzmCrm.API/Controllers/TicketsController.cs) — read in full (111 lines, current end-state after KAN-2 Story 07). This story **edits** this file to append two new actions after `GetHistory` (lines 102-110), rather than creating a new controller.
10. [src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs) — read in full (27 lines, current end-state after KAN-3). Add `DbSet<TicketComment> TicketComments { get; }` next to the existing `Ticket*` members.
11. [src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs](../../../src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs) — read in full (66 lines). Lines 25-33 are the `DbSet<T>` properties to extend; line 35's comment marks where.
12. [src/AzmCrm.Application/Shared/Exceptions/NotFoundException.cs](../../../src/AzmCrm.Application/Shared/Exceptions/NotFoundException.cs) (3 lines) and [src/AzmCrm.API/Middleware/ExceptionHandlingMiddleware.cs](../../../src/AzmCrm.API/Middleware/ExceptionHandlingMiddleware.cs) lines 26-43 (`NotFoundException` → HTTP 404 at lines 33-37).
13. [src/AzmCrm.Application/Localization/LocalizationKeys.cs](../../../src/AzmCrm.Application/Localization/LocalizationKeys.cs) — read in full (46 lines). This story reuses `Validation.Required` and `Validation.MaxLength` only.
14. [src/AzmCrm.Infrastructure/Data/Migrations/20260828165442_AddCommunications.cs](../../../src/AzmCrm.Infrastructure/Data/Migrations/20260828165442_AddCommunications.cs) — the most recent migration; the new migration for this story adds the `TicketComments` table on top of this baseline (or on top of whichever KAN-4 story's migration is latest at implementation time).
15. [tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs](../../../tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs) — read in full (44 lines). Add `TicketComment` `DbSet<T>` property and its query filter.
16. [tests/AzmCrm.Application.Tests/TestDoubles/StubIdentityQueryService.cs](../../../tests/AzmCrm.Application.Tests/TestDoubles/StubIdentityQueryService.cs) *(created by KAN-2 Story 06 — verify it exists before writing tests; if it doesn't, create it exactly as Story 06 specifies)* — reused as-is for `GetTicketCommentsQueryHandlerTests`' author-name resolution.

## Implementation tasks

### 1 — Domain layer

**Create file: `src/AzmCrm.Domain/Features/Tickets/TicketComment.cs`**

```csharp
using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.Tickets;

public sealed class TicketComment : BaseEntity
{
    public required Guid TicketId { get; init; }
    public required string Content { get; set; }

    public Ticket Ticket { get; init; } = null!;
}
```

Placed alongside `Ticket.cs`/`TicketHistory.cs` in the same `AzmCrm.Domain.Features.Tickets` namespace, since it's a child aggregate of `Ticket` exactly like `TicketHistory` is.

### 2 — Application layer

**Create file: `src/AzmCrm.Application/Features/Tickets/DTOs/TicketCommentDto.cs`**

```csharp
namespace AzmCrm.Application.Features.Tickets.DTOs;

public sealed record TicketCommentDto(
    Guid Id,
    Guid TicketId,
    string Content,
    Guid CreatedBy,
    string? CreatedByName,
    DateTime CreatedOn
);
```

**Create file: `src/AzmCrm.Application/Features/Tickets/DTOs/CreateTicketCommentRequest.cs`**

```csharp
namespace AzmCrm.Application.Features.Tickets.DTOs;

public sealed record CreateTicketCommentRequest(string Content);
```

**Create file: `src/AzmCrm.Application/Features/Tickets/Commands/CreateTicketComment/CreateTicketCommentCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Tickets.Commands.CreateTicketComment;

public sealed record CreateTicketCommentCommand(Guid TicketId, string Content) : IRequest<Result<Guid>>;
```

**Create file: `src/AzmCrm.Application/Features/Tickets/Commands/CreateTicketComment/CreateTicketCommentCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Tickets.Commands.CreateTicketComment;

internal sealed class CreateTicketCommentCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreateTicketCommentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateTicketCommentCommand request, CancellationToken ct)
    {
        var ticketExists = await dbContext.Tickets.AnyAsync(t => t.Id == request.TicketId, ct);
        if (!ticketExists)
            throw new NotFoundException($"Ticket '{request.TicketId}' was not found.");

        var comment = new TicketComment
        {
            TicketId = request.TicketId,
            Content = request.Content
        };

        dbContext.TicketComments.Add(comment);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(comment.Id);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Tickets/Commands/CreateTicketComment/CreateTicketCommentCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Tickets.Commands.CreateTicketComment;

public sealed class CreateTicketCommentCommandValidator : AbstractValidator<CreateTicketCommentCommand>
{
    public CreateTicketCommentCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Ticket Id"]);

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Content"])
            .MaximumLength(4000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Content", 4000]);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Tickets/Queries/GetTicketComments/GetTicketCommentsQuery.cs`**

```csharp
using AzmCrm.Application.Features.Tickets.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Tickets.Queries.GetTicketComments;

public sealed record GetTicketCommentsQuery(
    Guid TicketId,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<Result<PaginatedResult<TicketCommentDto>>>;
```

**Create file: `src/AzmCrm.Application/Features/Tickets/Queries/GetTicketComments/GetTicketCommentsQueryHandler.cs`**

```csharp
using AzmCrm.Application.Features.Tickets.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Tickets.Queries.GetTicketComments;

internal sealed class GetTicketCommentsQueryHandler(
    IApplicationDbContext dbContext,
    IIdentityQueryService identityQueryService)
    : IRequestHandler<GetTicketCommentsQuery, Result<PaginatedResult<TicketCommentDto>>>
{
    public async Task<Result<PaginatedResult<TicketCommentDto>>> Handle(
        GetTicketCommentsQuery request, CancellationToken ct)
    {
        var ticketExists = await dbContext.Tickets.AnyAsync(t => t.Id == request.TicketId, ct);
        if (!ticketExists)
            throw new NotFoundException($"Ticket '{request.TicketId}' was not found.");

        var query = dbContext.TicketComments.Where(c => c.TicketId == request.TicketId);

        var totalCount = await query.CountAsync(ct);

        var comments = await query
            .OrderBy(c => c.CreatedOn) // oldest first — collaboration thread reading order, see Story Goal
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var authorIds = comments.Select(c => c.CreatedBy).Distinct();
        var authorNames = await identityQueryService.GetUsersInfoAsync(authorIds, ct);

        var items = comments.Select(c => new TicketCommentDto(
            c.Id, c.TicketId, c.Content, c.CreatedBy,
            authorNames.TryGetValue(c.CreatedBy, out var info) ? info.FullName : null,
            c.CreatedOn));

        var result = new PaginatedResult<TicketCommentDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<TicketCommentDto>>.Success(result);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Tickets/Queries/GetTicketComments/GetTicketCommentsQueryValidator.cs`** — same shape as `GetCustomerNotesQueryValidator` (Context item 4): `RuleFor(x => x.TicketId).NotEmpty()...`, plus the standard paging-range rules (`PageNumber >= 1`, `PageSize` between 1 and 100).

**Edit file: `src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs`** — after `DbSet<TicketHistory> TicketHistories { get; }`:

```csharp
DbSet<TicketComment> TicketComments { get; }
```

### 3 — Infrastructure layer

**Create file: `src/AzmCrm.Infrastructure/Data/Configurations/TicketCommentConfiguration.cs`**

```csharp
using AzmCrm.Domain.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class TicketCommentConfiguration : IEntityTypeConfiguration<TicketComment>
{
    public void Configure(EntityTypeBuilder<TicketComment> builder)
    {
        builder.ToTable("TicketComments");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.Content)
            .IsRequired()
            .HasMaxLength(4000);

        builder.HasOne(c => c.Ticket)
            .WithMany()
            .HasForeignKey(c => c.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(c => !c.IsDeleted);

        builder.HasIndex(c => c.TicketId);
    }
}
```

**Edit file: `src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs`** — after `public DbSet<TicketHistory> TicketHistories => Set<TicketHistory>();`:

```csharp
public DbSet<TicketComment> TicketComments => Set<TicketComment>();
```

**Generate migration:**

```bash
dotnet ef migrations add AddTicketComments --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API
```

### 4 — API layer

**Edit file: `src/AzmCrm.API/Controllers/TicketsController.cs`** — add `using AzmCrm.Application.Features.Tickets.Commands.CreateTicketComment;` and `using AzmCrm.Application.Features.Tickets.Queries.GetTicketComments;`, then append two new actions after `GetHistory` (after line 110, before the closing `}` of the class):

```csharp
[HttpPost("{id:guid}/comments")]
[ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> AddComment(Guid id, [FromBody] CreateTicketCommentRequest request, CancellationToken ct)
{
    var command = new CreateTicketCommentCommand(id, request.Content);

    var result = await mediator.Send(command, ct);

    return ToCreatedResult(result, commentId => $"/api/tickets/{id}/comments/{commentId}");
}

[HttpGet("{id:guid}/comments")]
[ProducesResponseType(typeof(Result<PaginatedResult<TicketCommentDto>>), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetComments(
    Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
{
    var result = await mediator.Send(new GetTicketCommentsQuery(id, pageNumber, pageSize), ct);
    return ToResult(result);
}
```

`TicketCommentDto` needs a `using AzmCrm.Application.Features.Tickets.DTOs;` — already present in this file (Context item 9 confirms it's imported for `TicketDto`/`TicketListItemDto`/`TicketHistoryDto`).

## Edge Cases & Failure Modes

- **`TicketId` on `POST /api/tickets/{id}/comments` or `GET /api/tickets/{id}/comments` does not resolve to an existing, non-deleted ticket** — both handlers check `dbContext.Tickets.AnyAsync(...)` (query filter excludes soft-deleted rows) and throw `NotFoundException` → 404, identical to `GetTicketHistoryQueryHandler`'s existing guard (Context item 4).
- **A comment's author (`CreatedBy`) no longer resolves to a known `ApplicationUser`** (e.g. a hard-deleted account, never performed by this codebase's own endpoints) — `IIdentityQueryService.GetUsersInfoAsync` simply omits that id from its returned dictionary; `GetTicketCommentsQueryHandler`'s `TryGetValue` then falls through to `CreatedByName = null`, and the frontend should render a "former teammate" or similar placeholder rather than assuming a name is always present — identical fallback behavior to `GetTicketsListQueryHandler`'s assignee-name resolution (Context item 5).
- **`GetTicketCommentsQuery` orders oldest-first, unlike `GetTicketHistoryQueryHandler`'s newest-first** (`OrderByDescending(h => h.CreatedOn)`) — this is intentional, matching KAN-3 Story 08's identical reasoning for message ordering: a collaboration thread among teammates reads top-to-bottom like a chat log, whereas ticket history is an audit trail read newest-first. Document this explicitly for frontend consumers so they don't assume every ticket-scoped list shares one ordering convention.
- **Comments are never editable or deletable** — matches `CustomerNote`'s existing append-only convention in this codebase; if an agent posts a mistaken comment, there is no correction mechanism beyond posting a follow-up comment. Flag as a follow-up if edit/delete becomes a real requirement.
- **`Content` containing only whitespace** — rejected by `CreateTicketCommentCommandValidator`'s `NotEmpty()` rule (FluentValidation's `NotEmpty()` already treats a whitespace-only string as empty) before the handler ever runs.
- **`PageNumber`/`PageSize` out of range** — enforced by `GetTicketCommentsQueryValidator` via the existing `ValidationBehavior` pipeline, turned into a 400 before the handler runs.

## Test Plan

All new tests live in `tests/AzmCrm.Application.Tests/`, following the existing `TestApplicationDbContext`/`StubLocalizationService`/`StubIdentityQueryService` infrastructure.

1. **Edit `tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs`** — add `public DbSet<TicketComment> TicketComments => Set<TicketComment>();` and `modelBuilder.Entity<TicketComment>().HasQueryFilter(c => !c.IsDeleted);` to `OnModelCreating`.
2. **Create file: `tests/AzmCrm.Application.Tests/Features/Tickets/CreateTicketCommentCommandHandlerTests.cs`** — `Create_persists_comment_for_ticket`; `Create_for_missing_ticket_throws_NotFoundException`.
3. **Create file: `tests/AzmCrm.Application.Tests/Features/Tickets/GetTicketCommentsQueryHandlerTests.cs`** — `Comments_return_ordered_oldest_first` (seed three comments with distinct `CreatedOn` values out of chronological insertion order, assert the returned order is ascending by `CreatedOn`, mirroring `GetConversationMessagesQueryHandlerTests`' `Messages_return_ordered_oldest_first` from KAN-3 Story 08); `Comments_for_missing_ticket_throws_NotFoundException`; `Comment_author_name_is_resolved_via_identity_service` (populate `StubIdentityQueryService.Users` with the seeded comment's `CreatedBy` id, assert `CreatedByName` matches); `Comment_author_name_is_null_when_identity_lookup_misses`.
4. **Create file: `tests/AzmCrm.Application.Tests/Features/Tickets/CreateTicketCommentCommandValidatorTests.cs`** — `Empty_TicketId_fails`; `Empty_Content_fails`; `Content_over_4000_chars_fails`; `Valid_command_passes` — construct the validator with `StubLocalizationService`.

## Migration / Rollback

- The EF Core migration generated in Task 3 only **adds** the `TicketComments` table — additive, safe to apply on top of whichever migration is latest at implementation time (`20260828165442_AddCommunications`, or a later KAN-4 migration if Stories 13/14/15 land first).
- **Rollback**: `dotnet ef database update <previous-migration-name> --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` drops the `TicketComments` table. No other table has a foreign key into it, so this is a clean rollback with no orphaned data.
- **Half-applied state**: same existing behavior as every prior migration — `DatabaseInitializer.InitializeAsync` logs and rethrows on failure, so the app fails to start rather than running against a partially-migrated schema.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Migration applies cleanly:** `dotnet ef database update --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` (or let the API apply it automatically on startup).
4. **Manual smoke test:** create a customer and a ticket (KAN-1/KAN-2), obtain a bearer token, `POST /api/tickets/{id}/comments` with `{"content":"Escalating to billing team"}`, confirm 201; log in as a second registered user and `POST /api/tickets/{id}/comments` with `{"content":"On it, will follow up today"}`; `GET /api/tickets/{id}/comments` as either user and confirm both comments appear in the order they were posted (oldest first), each showing the correct `createdByName`.

## Done Criteria

- [ ] `TicketComment` entity, EF configuration, and migration exist and `dotnet ef database update` applies cleanly.
- [ ] `POST /api/tickets/{id}/comments` and `GET /api/tickets/{id}/comments` work end-to-end, requiring authentication, 404-ing for a missing or soft-deleted ticket.
- [ ] `GetTicketCommentsQueryHandler` returns comments oldest-first with each comment's author name resolved.
- [ ] All new handler and validator unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.
