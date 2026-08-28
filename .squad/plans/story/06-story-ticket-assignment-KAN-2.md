# Story 06 — Ticket Assignment to Agents (Story: KAN-2)

## Prerequisites

- [05-story-ticket-core-crud-KAN-2.md](05-story-ticket-core-crud-KAN-2.md) completed: requires the `Ticket`/`TicketHistory` entities, `IApplicationDbContext.Tickets`/`TicketHistories`, `TicketsController`, `TicketDto`/`TicketListItemDto`/`GetTicketsListQuery`, and the `TestApplicationDbContext` test double.

## Story Goal

Let support agents (and, later, automated routing) assign a ticket to a specific agent account, or unassign it, satisfying KAN-2's "Assign tickets to specific agents" acceptance criterion. Every assignment change is recorded as a `TicketHistory` entry, feeding "View complete ticket history" (Story 05's `GET /api/tickets/{id}/history`) without any change to that endpoint.

Outcomes:
1. `PUT /api/tickets/{id}/assign` sets (or clears, when `assignedToUserId` is `null`) the ticket's assigned agent, validating the agent account exists via the existing `IIdentityQueryService` abstraction.
2. `GET /api/tickets/{id}` and `GET /api/tickets` responses include `AssignedToUserId`/`AssignedToUserName`.
3. `GET /api/tickets?assignedToUserId=...` filters the list to a single agent's tickets (e.g. "my tickets").

**Not in scope**: enforcing that the assigned account is an active agent (`ApplicationUser.IsActive` is not checked — `IIdentityQueryService.GetUserInfoAsync` doesn't expose it, and extending that interface is out of scope for this story), role-based restriction of who counts as an "agent" (any valid `ApplicationUser` id is accepted), and assignment notifications/emails.

## Context — Read These Files First

1. [05-story-ticket-core-crud-KAN-2.md](05-story-ticket-core-crud-KAN-2.md) — read in full. This story edits several files that story created (`Ticket.cs`, `TicketDto.cs`, `TicketListItemDto.cs`, `GetTicketsListQuery.cs`, `GetTicketsListQueryHandler.cs`, `TicketConfiguration.cs`, `TicketsController.cs`) rather than creating a new command/query triad from scratch for the DTO/list changes — read it fully before touching those files.
2. [src/AzmCrm.Domain/Features/Tickets/Ticket.cs](../../../src/AzmCrm.Domain/Features/Tickets/Ticket.cs) — created by Story 05. This story adds one new nullable property (`AssignedToUserId`) to it; no navigation property to `ApplicationUser` is added here (see item 5 below for why).
3. [src/AzmCrm.Application/Shared/Interfaces/IIdentityQueryService.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IIdentityQueryService.cs) — read in full (15 lines). `Task<(string? FullName, string? Email)> GetUserInfoAsync(Guid userId, CancellationToken ct = default)` (line 10) returns `(null, null)` for an unknown user — this is the existence check `AssignTicketCommandHandler` uses instead of querying `ApplicationUser` directly, keeping the Application layer decoupled from ASP.NET Identity types (per the interface's own doc-comment, lines 4-5). `Task<Dictionary<Guid, (string? FullName, string? Email)>> GetUsersInfoAsync(IEnumerable<Guid> userIds, ...)` (lines 13-14) is the batch form `GetTicketsListQueryHandler` uses to resolve names for a whole page without an N+1 query pattern.
4. [src/AzmCrm.Infrastructure/Identity/IdentityQueryService.cs](../../../src/AzmCrm.Infrastructure/Identity/IdentityQueryService.cs) — read in full (35 lines). The concrete implementation, already registered as `services.AddScoped<IIdentityQueryService, IdentityQueryService>();` at [src/AzmCrm.Infrastructure/DependencyInjection.cs:86](../../../src/AzmCrm.Infrastructure/DependencyInjection.cs) — **no DI registration change is needed in this story**.
5. [src/AzmCrm.Infrastructure/Data/Configurations/ApplicationUserConfiguration.cs](../../../src/AzmCrm.Infrastructure/Data/Configurations/ApplicationUserConfiguration.cs) — lines 22-25. Precedent for a `HasForeignKey` relationship targeting `ApplicationUser`. Unlike that example, `TicketConfiguration`'s new FK targets `ApplicationUser` **without** a corresponding navigation property on `Ticket` (via `builder.HasOne<ApplicationUser>()` with a type argument instead of `builder.HasOne(t => t.AssignedToUser)`) — this keeps `ApplicationUser` out of the `Ticket` entity's public surface (Application-layer code must go through `IIdentityQueryService`, not `Ticket.AssignedToUser`), while Infrastructure still gets a real DB-level FK constraint because `Ticket` and `ApplicationUser` live in the same `ApplicationDbContext`/database.
6. [src/AzmCrm.Domain/Features/Identity/ApplicationUser.cs](../../../src/AzmCrm.Domain/Features/Identity/ApplicationUser.cs) — read in full (14 lines). `Id` is a `Guid` (via `IdentityUser<Guid>`) — the exact type `Ticket.AssignedToUserId` uses.
7. [src/AzmCrm.Application/Features/Tickets/DTOs/TicketDto.cs](../../../src/AzmCrm.Application/Features/Tickets/DTOs/TicketDto.cs) and [TicketListItemDto.cs](../../../src/AzmCrm.Application/Features/Tickets/DTOs/TicketListItemDto.cs) — created by Story 05, which explicitly left room for later stories to **append** trailing parameters — add `Guid? AssignedToUserId, string? AssignedToUserName` at the end of both.
8. [src/AzmCrm.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs) and [GetTicketsList/GetTicketsListQueryHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQueryHandler.cs) — created by Story 05. Both gain an `IIdentityQueryService` constructor dependency and a resolution step before constructing their DTOs.
9. [src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandHandler.cs](../../../src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandHandler.cs) (Story 01) and [src/AzmCrm.Application/Features/Tickets/Commands/UpdateTicket/UpdateTicketCommandHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Commands/UpdateTicket/UpdateTicketCommandHandler.cs) (Story 05) — the "load-or-404, mutate, log history, save" handler shape `AssignTicketCommandHandler` follows.
10. [src/AzmCrm.API/Controllers/TicketsController.cs](../../../src/AzmCrm.API/Controllers/TicketsController.cs) — created by Story 05. This story **edits** this file to add one new action and to pass the new `assignedToUserId` filter through `GetList`, rather than creating a new controller.
11. [src/AzmCrm.Application/Shared/Exceptions/NotFoundException.cs](../../../src/AzmCrm.Application/Shared/Exceptions/NotFoundException.cs) and [src/AzmCrm.API/Middleware/ExceptionHandlingMiddleware.cs](../../../src/AzmCrm.API/Middleware/ExceptionHandlingMiddleware.cs) lines 26-43 — thrown both when `id` doesn't resolve to an existing ticket and when a non-null `AssignedToUserId` doesn't resolve to a known user (see Edge Cases for why this uses 404 rather than a validation 400).

## Implementation tasks

### 1 — Domain layer

**Edit file: `src/AzmCrm.Domain/Features/Tickets/Ticket.cs`** — add one property (no navigation property to `ApplicationUser`; see Context item 5):

```csharp
public Guid? AssignedToUserId { get; set; }
```

### 2 — Application layer

**Edit file: `src/AzmCrm.Application/Features/Tickets/DTOs/TicketDto.cs`** — append two trailing parameters:

```csharp
public sealed record TicketDto(
    Guid Id,
    Guid CustomerId,
    string Title,
    string? Description,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    DateTime CreatedOn,
    DateTime? UpdatedOn,
    Guid? AssignedToUserId,
    string? AssignedToUserName
);
```

**Edit file: `src/AzmCrm.Application/Features/Tickets/DTOs/TicketListItemDto.cs`** — append the same two parameters:

```csharp
public sealed record TicketListItemDto(
    Guid Id,
    Guid CustomerId,
    string Title,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    DateTime CreatedOn,
    Guid? AssignedToUserId,
    string? AssignedToUserName
);
```

**Create file: `src/AzmCrm.Application/Features/Tickets/DTOs/AssignTicketRequest.cs`**

```csharp
namespace AzmCrm.Application.Features.Tickets.DTOs;

public sealed record AssignTicketRequest(Guid? AssignedToUserId);
```

**Create file: `src/AzmCrm.Application/Features/Tickets/Commands/AssignTicket/AssignTicketCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Tickets.Commands.AssignTicket;

public sealed record AssignTicketCommand(Guid TicketId, Guid? AssignedToUserId) : IRequest<Result>;
```

**Create file: `src/AzmCrm.Application/Features/Tickets/Commands/AssignTicket/AssignTicketCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Tickets.Commands.AssignTicket;

internal sealed class AssignTicketCommandHandler(
    IApplicationDbContext dbContext,
    IIdentityQueryService identityQueryService)
    : IRequestHandler<AssignTicketCommand, Result>
{
    public async Task<Result> Handle(AssignTicketCommand request, CancellationToken ct)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, ct)
            ?? throw new NotFoundException($"Ticket '{request.TicketId}' was not found.");

        var previousAssignee = ticket.AssignedToUserId;

        if (request.AssignedToUserId is null)
        {
            if (previousAssignee is not null)
                dbContext.TicketHistories.Add(new TicketHistory
                {
                    TicketId = ticket.Id,
                    EventType = TicketHistoryEventType.Unassigned,
                    Description = "Ticket unassigned.",
                    OldValue = previousAssignee.ToString(),
                    NewValue = null
                });

            ticket.AssignedToUserId = null;
        }
        else
        {
            var (fullName, _) = await identityQueryService.GetUserInfoAsync(request.AssignedToUserId.Value, ct);
            if (fullName is null)
                throw new NotFoundException($"Agent '{request.AssignedToUserId}' was not found.");

            if (previousAssignee != request.AssignedToUserId)
                dbContext.TicketHistories.Add(new TicketHistory
                {
                    TicketId = ticket.Id,
                    EventType = TicketHistoryEventType.Assigned,
                    Description = $"Ticket assigned to {fullName}.",
                    OldValue = previousAssignee?.ToString(),
                    NewValue = request.AssignedToUserId.ToString()
                });

            ticket.AssignedToUserId = request.AssignedToUserId;
        }

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Tickets/Commands/AssignTicket/AssignTicketCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Tickets.Commands.AssignTicket;

public sealed class AssignTicketCommandValidator : AbstractValidator<AssignTicketCommand>
{
    public AssignTicketCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Ticket Id"]);
    }
}
```

No rule on `AssignedToUserId` itself — `null` is a valid, meaningful value (unassign), and any non-null `Guid` is checked for existence in the handler, not the validator (existence isn't a validator's concern in this codebase's convention — see `CreateCustomerInteractionCommandHandler`'s `AnyAsync` guard for the same split between "shape" validation and "does it exist" handler checks).

**Edit file: `src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQuery.cs`** — append one trailing optional parameter:

```csharp
public sealed record GetTicketsListQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? CustomerId = null,
    TicketStatus? Status = null,
    TicketCategory? Category = null,
    TicketPriority? Priority = null,
    string? Search = null,
    Guid? AssignedToUserId = null
) : IRequest<Result<PaginatedResult<TicketListItemDto>>>;
```

**Edit file: `src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQueryHandler.cs`** — add the `AssignedToUserId` filter, inject `IIdentityQueryService`, and resolve display names for the page:

```csharp
internal sealed class GetTicketsListQueryHandler(
    IApplicationDbContext dbContext,
    IIdentityQueryService identityQueryService)
    : IRequestHandler<GetTicketsListQuery, Result<PaginatedResult<TicketListItemDto>>>
{
    public async Task<Result<PaginatedResult<TicketListItemDto>>> Handle(
        GetTicketsListQuery request, CancellationToken ct)
    {
        var query = dbContext.Tickets.AsQueryable();

        // ... existing CustomerId/Status/Category/Priority/Search filters unchanged ...

        if (request.AssignedToUserId is not null)
            query = query.Where(t => t.AssignedToUserId == request.AssignedToUserId);

        var totalCount = await query.CountAsync(ct);

        var tickets = await query
            .OrderByDescending(t => t.CreatedOn)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var assigneeIds = tickets.Where(t => t.AssignedToUserId is not null)
            .Select(t => t.AssignedToUserId!.Value);
        var assigneeNames = await identityQueryService.GetUsersInfoAsync(assigneeIds, ct);

        var items = tickets.Select(t => new TicketListItemDto(
            t.Id, t.CustomerId, t.Title, t.Category, t.Priority, t.Status, t.CreatedOn,
            t.AssignedToUserId,
            t.AssignedToUserId is not null && assigneeNames.TryGetValue(t.AssignedToUserId.Value, out var info)
                ? info.FullName
                : null));

        var result = new PaginatedResult<TicketListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<TicketListItemDto>>.Success(result);
    }
}
```

Note the `.Select(...)` projection into `TicketListItemDto` moves from the EF `IQueryable` (Story 05) to an in-memory `List<Ticket>.Select(...)` here, because the name lookup requires an already-materialized page of `AssignedToUserId` values before it can call `GetUsersInfoAsync`. `OrderBy`/`Skip`/`Take`/`CountAsync` still run in the database via `ToListAsync(ct)`.

**Edit file: `src/AzmCrm.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs`** — inject `IIdentityQueryService` and resolve the single assignee's name before constructing the DTO:

```csharp
internal sealed class GetTicketByIdQueryHandler(
    IApplicationDbContext dbContext,
    IIdentityQueryService identityQueryService)
    : IRequestHandler<GetTicketByIdQuery, Result<TicketDto>>
{
    public async Task<Result<TicketDto>> Handle(GetTicketByIdQuery request, CancellationToken ct)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException($"Ticket '{request.Id}' was not found.");

        string? assignedToUserName = null;
        if (ticket.AssignedToUserId is not null)
        {
            var (fullName, _) = await identityQueryService.GetUserInfoAsync(ticket.AssignedToUserId.Value, ct);
            assignedToUserName = fullName;
        }

        var dto = new TicketDto(
            ticket.Id, ticket.CustomerId, ticket.Title, ticket.Description, ticket.Category,
            ticket.Priority, ticket.Status, ticket.CreatedOn, ticket.UpdatedOn,
            ticket.AssignedToUserId, assignedToUserName);

        return Result<TicketDto>.Success(dto);
    }
}
```

### 3 — Infrastructure layer

**Edit file: `src/AzmCrm.Infrastructure/Data/Configurations/TicketConfiguration.cs`** — add `using AzmCrm.Domain.Features.Identity;` and, inside `Configure`, add:

```csharp
builder.HasOne<ApplicationUser>()
    .WithMany()
    .HasForeignKey(t => t.AssignedToUserId)
    .OnDelete(DeleteBehavior.SetNull);

builder.HasIndex(t => t.AssignedToUserId);
```

`DeleteBehavior.SetNull` (rather than `Cascade`) because `AssignedToUserId` is nullable and a hard-deleted agent account should clear the assignment, not delete the ticket. This is a database-level FK for referential integrity only — Application-layer code never navigates `Ticket` → `ApplicationUser` directly; it always goes through `IIdentityQueryService` (see Context item 5).

**Generate migration:**

```bash
dotnet ef migrations add AddTicketAssignment --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API
```

### 4 — API layer

**Edit file: `src/AzmCrm.API/Controllers/TicketsController.cs`** — add `using AzmCrm.Application.Features.Tickets.Commands.AssignTicket;`, add the `assignedToUserId` parameter to `GetList`, and add one new action:

```csharp
[HttpGet]
[ProducesResponseType(typeof(Result<PaginatedResult<TicketListItemDto>>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetList(
    [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
    [FromQuery] Guid? customerId = null, [FromQuery] TicketStatus? status = null,
    [FromQuery] TicketCategory? category = null, [FromQuery] TicketPriority? priority = null,
    [FromQuery] string? search = null, [FromQuery] Guid? assignedToUserId = null,
    CancellationToken ct = default)
{
    var result = await mediator.Send(
        new GetTicketsListQuery(
            pageNumber, pageSize, customerId, status, category, priority, search, assignedToUserId), ct);
    return ToResult(result);
}

[HttpPut("{id:guid}/assign")]
[ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> Assign(Guid id, [FromBody] AssignTicketRequest request, CancellationToken ct)
{
    var result = await mediator.Send(new AssignTicketCommand(id, request.AssignedToUserId), ct);
    return ToResult(result);
}
```

## Edge Cases & Failure Modes

- **`AssignedToUserId` in the request body does not resolve to any `ApplicationUser`** — `AssignTicketCommandHandler` checks `identityQueryService.GetUserInfoAsync(...)`; a `null` `FullName` means "not found" per that method's own contract (`IIdentityQueryService.cs:9`), and the handler throws `NotFoundException` → HTTP 404. This is a deliberate consistency choice with every other "referenced entity doesn't exist" case in this codebase (e.g. `CustomerId` on ticket create), even though the missing id arrives in the request body rather than the URL route — document this for API consumers, since a 404 on a `PUT` body field is slightly unusual REST style but matches this codebase's existing convention rather than introducing a new one.
- **Re-assigning a ticket to the agent it's already assigned to** — `AssignTicketCommandHandler`'s `if (previousAssignee != request.AssignedToUserId)` guard means no new `TicketHistory` row is written; `SaveChangesAsync` still runs a harmless no-op, and the command still returns `Result.Success()`.
- **Unassigning an already-unassigned ticket** (`AssignedToUserId` already `null`, request also `null`) — the `if (previousAssignee is not null)` guard means no `Unassigned` history row is written either; same no-op semantics as above.
- **An inactive agent (`ApplicationUser.IsActive == false`) is assigned a ticket** — deliberately not checked; `IIdentityQueryService.GetUserInfoAsync` only reports existence, not `IsActive`. Flag as a follow-up if inactive-agent assignment becomes a real workflow problem; extending the interface is out of scope here to avoid touching `IIdentityQueryService`'s existing contract for a case this story's acceptance criteria doesn't call out.
- **`GetTicketsListQueryHandler`'s name-resolution step runs one `GetUsersInfoAsync` batch call per page, not per row** — avoids the classic N+1 pattern; a ticket with no assignee never appears in the `assigneeIds` set passed to that call.
- **Deleting (soft-deleting) a `Customer` does not affect ticket assignment** — orthogonal concerns; assignment is by `Ticket`, not by `Customer`.
- **A hard-deleted `ApplicationUser`** (never performed by this codebase's own endpoints, but possible via direct DB access or a future admin feature) **clears `AssignedToUserId` via `DeleteBehavior.SetNull`** without writing a `TicketHistory` row, since that happens entirely inside the database and no Application-layer handler runs — call this out to reviewers as a known gap if user hard-deletion is ever implemented.

## Test Plan

1. **Create file: `tests/AzmCrm.Application.Tests/TestDoubles/StubIdentityQueryService.cs`**:
   ```csharp
   using AzmCrm.Application.Shared.Interfaces;

   namespace AzmCrm.Application.Tests.TestDoubles;

   /// <summary>Hand-written <see cref="IIdentityQueryService"/> stub for handler tests.</summary>
   public sealed class StubIdentityQueryService : IIdentityQueryService
   {
       public Dictionary<Guid, (string? FullName, string? Email)> Users { get; } = [];

       public Task<(string? FullName, string? Email)> GetUserInfoAsync(Guid userId, CancellationToken ct = default) =>
           Task.FromResult(Users.TryGetValue(userId, out var info) ? info : (null, null));

       public Task<Dictionary<Guid, (string? FullName, string? Email)>> GetUsersInfoAsync(
           IEnumerable<Guid> userIds, CancellationToken ct = default) =>
           Task.FromResult(userIds.Where(Users.ContainsKey).ToDictionary(id => id, id => Users[id]));
   }
   ```
2. **Create file: `tests/AzmCrm.Application.Tests/Features/Tickets/AssignTicketCommandHandlerTests.cs`** — `Assign_to_known_agent_sets_AssignedToUserId_and_logs_Assigned_history`; `Assign_to_unknown_agent_throws_NotFoundException`; `Unassign_clears_AssignedToUserId_and_logs_Unassigned_history`; `Reassign_to_same_agent_persists_without_extra_history_row`; `Assign_missing_ticket_throws_NotFoundException`. Populate `StubIdentityQueryService.Users` with a fake agent id/name in the fixture setup for each "known agent" case.
3. **Edit `tests/AzmCrm.Application.Tests/Features/Tickets/GetTicketsListQueryHandlerTests.cs`** (Story 05) — add `List_filters_by_assignedToUserId` and update every existing test in this file to pass a `StubIdentityQueryService` into `GetTicketsListQueryHandler`'s new constructor parameter.
4. **Edit `tests/AzmCrm.Application.Tests/Features/Tickets/*` tests that construct `GetTicketByIdQueryHandler`** (Story 05) — update them to pass a `StubIdentityQueryService`.
5. **Create file: `tests/AzmCrm.Application.Tests/Features/Tickets/AssignTicketCommandValidatorTests.cs`** — `Empty_TicketId_fails`; `Null_AssignedToUserId_passes` (unassign is valid); `Valid_command_passes` — use `StubLocalizationService`.

## Migration / Rollback

- The migration generated in Task 3 only **adds** the `AssignedToUserId` column (nullable) and its FK/index to the existing `Tickets` table — additive, safe on top of Story 05's `AddTickets` migration.
- **Rollback**: `dotnet ef database update AddTickets --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` drops the column, FK, and index. No data loss beyond the assignment values themselves.
- **Half-applied state**: same existing behavior — `DatabaseInitializer` logs and rethrows on migration failure, so the app fails to start rather than running against a partial schema.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Migration applies cleanly:** `dotnet ef database update --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` (or let the API apply it automatically on startup).
4. **Manual smoke test:** create a ticket (Story 05) and register a second user to act as an agent (`POST /api/identity/register`), then `PUT /api/tickets/{id}/assign` with `{"assignedToUserId":"<agent-id>"}`, confirm 200; `GET /api/tickets/{id}` shows `assignedToUserId`/`assignedToUserName`; `GET /api/tickets?assignedToUserId=<agent-id>` returns it; `PUT /api/tickets/{id}/assign` with `{"assignedToUserId":null}` unassigns it; `GET /api/tickets/{id}/history` shows both the `Assigned` and `Unassigned` entries; repeat assign against a random, non-existent agent id and confirm 404.

## Done Criteria

- [ ] `Ticket.AssignedToUserId`, its EF configuration (FK + index), and migration exist and apply cleanly on top of Story 05's schema.
- [ ] `PUT /api/tickets/{id}/assign` assigns and unassigns correctly, 404s for a missing ticket or unknown agent id.
- [ ] `GET /api/tickets/{id}` and `GET /api/tickets` responses include `assignedToUserId`/`assignedToUserName`, and `GET /api/tickets?assignedToUserId=...` filters correctly.
- [ ] Assignment and unassignment each log one `TicketHistory` entry (no-ops when nothing actually changes).
- [ ] All new and updated handler and validator unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 07.**
