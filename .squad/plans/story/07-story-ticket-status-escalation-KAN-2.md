# Story 07 — Ticket Status Tracking & Escalation (Story: KAN-2)

## Prerequisites

- [05-story-ticket-core-crud-KAN-2.md](05-story-ticket-core-crud-KAN-2.md) completed: requires the `Ticket`/`TicketHistory` entities (`Ticket.Status` already exists, defaulting to `TicketStatus.New`), `IApplicationDbContext.Tickets`/`TicketHistories`, `TicketsController`, `TicketDto`/`TicketListItemDto`/`GetTicketsListQuery`, and the `TestApplicationDbContext` test double.
- Independent of [06-story-ticket-assignment-KAN-2.md](06-story-ticket-assignment-KAN-2.md) — status/escalation and assignment are separate concerns on the same `Ticket` row and can be implemented in either order, but this plan assumes Story 06 landed first only for the shared editing pattern it establishes on `TicketDto.cs`/`TicketListItemDto.cs`/`GetTicketsListQuery.cs`/`TicketConfiguration.cs`/`TicketsController.cs` (append-only DTO parameters, additive query filters, new controller actions in the same file). This story adds no dependency on `AssignedToUserId` itself.

## Story Goal

Let support agents move a ticket through its status lifecycle and escalate a ticket that needs urgent attention, satisfying KAN-2's "Track ticket status and escalation" acceptance criterion. Both actions are logged as `TicketHistory` entries, feeding "View complete ticket history" (Story 05's `GET /api/tickets/{id}/history`) without any change to that endpoint.

Outcomes:
1. `PUT /api/tickets/{id}/status` changes a ticket's `Status` to any other defined `TicketStatus` value.
2. `POST /api/tickets/{id}/escalate` marks a ticket as escalated, recording an escalation timestamp and an optional reason.
3. `GET /api/tickets/{id}` and `GET /api/tickets` responses include `IsEscalated`/`EscalatedOn`.
4. `GET /api/tickets?isEscalated=true` filters the list to only escalated tickets.

**Not in scope**: a formal status state machine (e.g. disallowing `Closed → InProgress` without first `Reopened`) — any status may transition to any other status, since KAN-2's acceptance criteria only ask to "track" status, not to enforce a workflow; de-escalation (no `unescalate` endpoint — escalation is treated as a one-way, timestamped event per this story's minimal scope; a later story can add de-escalation if agents need it); and escalation notifications/alerts.

## Context — Read These Files First

1. [05-story-ticket-core-crud-KAN-2.md](05-story-ticket-core-crud-KAN-2.md) — read in full. This story edits several files that story created, following the exact same append-only DTO / additive-filter / new-controller-action pattern [06-story-ticket-assignment-KAN-2.md](06-story-ticket-assignment-KAN-2.md) already used for `AssignedToUserId` — read that story too for a second worked example of the same editing pattern before touching `TicketDto.cs`/`TicketListItemDto.cs`/`GetTicketsListQuery.cs`/`TicketsController.cs`.
2. [src/AzmCrm.Domain/Features/Tickets/Ticket.cs](../../../src/AzmCrm.Domain/Features/Tickets/Ticket.cs) — created by Story 05 (`Status` property, defaulting to `TicketStatus.New`), edited by Story 06 (`AssignedToUserId`). This story adds two more properties (`IsEscalated`, `EscalatedOn`).
3. [src/AzmCrm.Domain/Features/Tickets/TicketStatus.cs](../../../src/AzmCrm.Domain/Features/Tickets/TicketStatus.cs) — read in full (11 lines: `New`, `Open`, `InProgress`, `OnHold`, `Resolved`, `Closed`, `Reopened`). `ChangeTicketStatusCommandValidator`'s `IsInEnum()` rule validates against exactly these seven values.
4. [src/AzmCrm.Domain/Features/Tickets/TicketHistoryEventType.cs](../../../src/AzmCrm.Domain/Features/Tickets/TicketHistoryEventType.cs) — read in full (9 lines). This story uses the existing `StatusChanged` and `Escalated` members (both already defined by Story 05) — no change to this enum is needed.
5. [src/AzmCrm.Application/Features/Tickets/Commands/UpdateTicket/UpdateTicketCommandHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Commands/UpdateTicket/UpdateTicketCommandHandler.cs) (Story 05) and [Commands/AssignTicket/AssignTicketCommandHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Commands/AssignTicket/AssignTicketCommandHandler.cs) (Story 06) — the "load-or-404, diff old vs. new, log one `TicketHistory` row per real change, save" handler shape `ChangeTicketStatusCommandHandler` and `EscalateTicketCommandHandler` both follow.
6. [src/AzmCrm.Application/Features/Tickets/DTOs/TicketDto.cs](../../../src/AzmCrm.Application/Features/Tickets/DTOs/TicketDto.cs) and [TicketListItemDto.cs](../../../src/AzmCrm.Application/Features/Tickets/DTOs/TicketListItemDto.cs) — created by Story 05, already extended once by Story 06 (`AssignedToUserId`/`AssignedToUserName` appended). This story appends `IsEscalated`/`EscalatedOn` after those.
7. [src/AzmCrm.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs) and [GetTicketsList/GetTicketsListQueryHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQueryHandler.cs) — already edited once by Story 06 to resolve `AssignedToUserName`. This story only needs to append `ticket.IsEscalated, ticket.EscalatedOn` (or `t.IsEscalated, t.EscalatedOn`) to the existing `new TicketDto(...)`/`new TicketListItemDto(...)` calls — no `IIdentityQueryService`-style external lookup is needed since both new fields live directly on `Ticket`.
8. [src/AzmCrm.API/Controllers/TicketsController.cs](../../../src/AzmCrm.API/Controllers/TicketsController.cs) — created by Story 05, edited by Story 06 (`assignedToUserId` query param, `Assign` action). This story adds two more actions (`ChangeStatus`, `Escalate`) and one more `GetList` query param (`isEscalated`).
9. [src/AzmCrm.Application/Localization/LocalizationKeys.cs](../../../src/AzmCrm.Application/Localization/LocalizationKeys.cs) line 18 (`Validation.InvalidValue`) — reused for the `IsInEnum()` rule on the new status value; no new keys or `Messages.*.json` edits are needed by this story.
10. [src/AzmCrm.Application/Shared/Exceptions/NotFoundException.cs](../../../src/AzmCrm.Application/Shared/Exceptions/NotFoundException.cs) and [src/AzmCrm.API/Middleware/ExceptionHandlingMiddleware.cs](../../../src/AzmCrm.API/Middleware/ExceptionHandlingMiddleware.cs) lines 26-43 — thrown when `id` doesn't resolve to an existing ticket in either new handler.

## Implementation tasks

### 1 — Domain layer

**Edit file: `src/AzmCrm.Domain/Features/Tickets/Ticket.cs`** — add two properties (after the `AssignedToUserId` property added by Story 06):

```csharp
public bool IsEscalated { get; set; }
public DateTime? EscalatedOn { get; set; }
```

### 2 — Application layer

**Edit file: `src/AzmCrm.Application/Features/Tickets/DTOs/TicketDto.cs`** — append two trailing parameters (after `AssignedToUserId`/`AssignedToUserName` from Story 06):

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
    string? AssignedToUserName,
    bool IsEscalated,
    DateTime? EscalatedOn
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
    string? AssignedToUserName,
    bool IsEscalated,
    DateTime? EscalatedOn
);
```

**Create file: `src/AzmCrm.Application/Features/Tickets/DTOs/ChangeTicketStatusRequest.cs`**

```csharp
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Tickets.DTOs;

public sealed record ChangeTicketStatusRequest(TicketStatus Status);
```

**Create file: `src/AzmCrm.Application/Features/Tickets/DTOs/EscalateTicketRequest.cs`**

```csharp
namespace AzmCrm.Application.Features.Tickets.DTOs;

public sealed record EscalateTicketRequest(string? Reason);
```

**Create file: `src/AzmCrm.Application/Features/Tickets/Commands/ChangeTicketStatus/ChangeTicketStatusCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Tickets.Commands.ChangeTicketStatus;

public sealed record ChangeTicketStatusCommand(Guid TicketId, TicketStatus Status) : IRequest<Result>;
```

**Create file: `src/AzmCrm.Application/Features/Tickets/Commands/ChangeTicketStatus/ChangeTicketStatusCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Tickets.Commands.ChangeTicketStatus;

internal sealed class ChangeTicketStatusCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<ChangeTicketStatusCommand, Result>
{
    public async Task<Result> Handle(ChangeTicketStatusCommand request, CancellationToken ct)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, ct)
            ?? throw new NotFoundException($"Ticket '{request.TicketId}' was not found.");

        if (ticket.Status != request.Status)
        {
            dbContext.TicketHistories.Add(new TicketHistory
            {
                TicketId = ticket.Id,
                EventType = TicketHistoryEventType.StatusChanged,
                Description = $"Status changed from {ticket.Status} to {request.Status}.",
                OldValue = ticket.Status.ToString(),
                NewValue = request.Status.ToString()
            });

            ticket.Status = request.Status;
        }

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Tickets/Commands/ChangeTicketStatus/ChangeTicketStatusCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Tickets.Commands.ChangeTicketStatus;

public sealed class ChangeTicketStatusCommandValidator : AbstractValidator<ChangeTicketStatusCommand>
{
    public ChangeTicketStatusCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Ticket Id"]);

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage(localization[LocalizationKeys.Validation.InvalidValue, "Status"]);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Tickets/Commands/EscalateTicket/EscalateTicketCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Tickets.Commands.EscalateTicket;

public sealed record EscalateTicketCommand(Guid TicketId, string? Reason) : IRequest<Result>;
```

**Create file: `src/AzmCrm.Application/Features/Tickets/Commands/EscalateTicket/EscalateTicketCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Tickets.Commands.EscalateTicket;

internal sealed class EscalateTicketCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<EscalateTicketCommand, Result>
{
    public async Task<Result> Handle(EscalateTicketCommand request, CancellationToken ct)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, ct)
            ?? throw new NotFoundException($"Ticket '{request.TicketId}' was not found.");

        ticket.IsEscalated = true;
        ticket.EscalatedOn = DateTime.UtcNow;

        dbContext.TicketHistories.Add(new TicketHistory
        {
            TicketId = ticket.Id,
            EventType = TicketHistoryEventType.Escalated,
            Description = string.IsNullOrWhiteSpace(request.Reason)
                ? "Ticket escalated."
                : $"Ticket escalated: {request.Reason}"
        });

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

Note this handler is intentionally **not** idempotent-with-no-op like `ChangeTicketStatusCommandHandler`: escalating an already-escalated ticket still updates `EscalatedOn` to the current time and still logs a new `Escalated` history row, because a second escalation call is meaningful (e.g. "still not resolved, escalating again") rather than a no-op — document this for reviewers (see Edge Cases).

**Create file: `src/AzmCrm.Application/Features/Tickets/Commands/EscalateTicket/EscalateTicketCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketCommandValidator : AbstractValidator<EscalateTicketCommand>
{
    public EscalateTicketCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Ticket Id"]);

        RuleFor(x => x.Reason)
            .MaximumLength(1000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Reason", 1000]);
    }
}
```

**Edit file: `src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQuery.cs`** — append one trailing optional parameter (after `AssignedToUserId` from Story 06):

```csharp
public sealed record GetTicketsListQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? CustomerId = null,
    TicketStatus? Status = null,
    TicketCategory? Category = null,
    TicketPriority? Priority = null,
    string? Search = null,
    Guid? AssignedToUserId = null,
    bool? IsEscalated = null
) : IRequest<Result<PaginatedResult<TicketListItemDto>>>;
```

**Edit file: `src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQueryHandler.cs`** — add one more filter next to the `AssignedToUserId` filter Story 06 added:

```csharp
if (request.IsEscalated is not null)
    query = query.Where(t => t.IsEscalated == request.IsEscalated);
```

and append `t.IsEscalated, t.EscalatedOn` as trailing arguments to the existing `new TicketListItemDto(...)` construction.

**Edit file: `src/AzmCrm.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs`** — append `ticket.IsEscalated, ticket.EscalatedOn` as trailing arguments to the existing `new TicketDto(...)` construction.

### 3 — Infrastructure layer

**Edit file: `src/AzmCrm.Infrastructure/Data/Configurations/TicketConfiguration.cs`** — add, inside `Configure` (after the `AssignedToUserId` block added by Story 06):

```csharp
builder.Property(t => t.IsEscalated)
    .IsRequired()
    .HasDefaultValue(false);

builder.HasIndex(t => t.IsEscalated);
```

`EscalatedOn` needs no explicit configuration — it's a plain nullable `DateTime` column, following the same convention as `Ticket.UpdatedOn`/`DeletedOn` on `BaseEntity`, which also have no bespoke `Property(...)` calls anywhere in this codebase.

**Generate migration:**

```bash
dotnet ef migrations add AddTicketEscalation --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API
```

### 4 — API layer

**Edit file: `src/AzmCrm.API/Controllers/TicketsController.cs`** — add `using AzmCrm.Application.Features.Tickets.Commands.ChangeTicketStatus;` and `using AzmCrm.Application.Features.Tickets.Commands.EscalateTicket;`, add the `isEscalated` parameter to `GetList`, and add two new actions:

```csharp
[HttpGet]
[ProducesResponseType(typeof(Result<PaginatedResult<TicketListItemDto>>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetList(
    [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
    [FromQuery] Guid? customerId = null, [FromQuery] TicketStatus? status = null,
    [FromQuery] TicketCategory? category = null, [FromQuery] TicketPriority? priority = null,
    [FromQuery] string? search = null, [FromQuery] Guid? assignedToUserId = null,
    [FromQuery] bool? isEscalated = null, CancellationToken ct = default)
{
    var result = await mediator.Send(
        new GetTicketsListQuery(
            pageNumber, pageSize, customerId, status, category, priority, search,
            assignedToUserId, isEscalated), ct);
    return ToResult(result);
}

[HttpPut("{id:guid}/status")]
[ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeTicketStatusRequest request, CancellationToken ct)
{
    var result = await mediator.Send(new ChangeTicketStatusCommand(id, request.Status), ct);
    return ToResult(result);
}

[HttpPost("{id:guid}/escalate")]
[ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> Escalate(Guid id, [FromBody] EscalateTicketRequest request, CancellationToken ct)
{
    var result = await mediator.Send(new EscalateTicketCommand(id, request.Reason), ct);
    return ToResult(result);
}
```

## Edge Cases & Failure Modes

- **No status state machine is enforced** — `ChangeTicketStatusCommandHandler` accepts any `TicketStatus` value as the new status regardless of the current one (e.g. `Closed → Open` is allowed), because KAN-2's acceptance criteria only require *tracking* status, not enforcing a workflow. Flag as a follow-up if the business later needs to restrict transitions (e.g. require `Reopened` before re-entering `InProgress` from `Closed`).
- **Changing status to the value it already has** — `ChangeTicketStatusCommandHandler`'s `if (ticket.Status != request.Status)` guard means no new `TicketHistory` row is written; the command still returns `Result.Success()` (idempotent, matching `UpdateTicketCommandHandler`'s no-op pattern from Story 05).
- **Escalating an already-escalated ticket is *not* a no-op** — unlike every other command in this feature, `EscalateTicketCommandHandler` always updates `EscalatedOn` and always logs a new `TicketHistory` row, even if `IsEscalated` was already `true`, because a repeated escalation call is meaningful (re-escalation, or an updated reason) rather than a duplicate. This is a deliberate asymmetry with `ChangeTicketStatusCommandHandler`/`AssignTicketCommandHandler`'s no-op-on-unchanged-value pattern — call this out to reviewers so it isn't "fixed" into a no-op by mistake.
- **No de-escalation endpoint** — once `IsEscalated` is `true`, only creating a new ticket resets it (there's no way to clear it via the API). If agents need to mark a ticket as "no longer escalated" once resolved, that's a follow-up not covered by KAN-2's acceptance criteria as written.
- **`Status` sent as an unrecognized string** (e.g. `"Cancelled"`) — since `JsonStringEnumConverter` is already registered globally (KAN-1 Story 02, confirmed still active in `ApplicationExtensions.cs:15-17`), ASP.NET Core's model binder rejects the request body before it reaches MediatR, producing a framework-level 400 — not from `ChangeTicketStatusCommandValidator`. The validator's `IsInEnum()` rule is defense-in-depth for the case where an out-of-range **integer** reaches the command directly (e.g. a future non-JSON caller), since a malformed enum name never reaches `Handle`. This mirrors the exact caveat KAN-1 Story 02 documented for `InteractionType`.
- **`Reason` on escalate longer than 1000 characters** — rejected by `EscalateTicketCommandValidator` before the command reaches the handler.
- **Combining `isEscalated` with the other five list filters** (`customerId`, `status`, `category`, `priority`, `search`, `assignedToUserId`) — applied as one more independent, AND-combined `Where` clause in `GetTicketsListQueryHandler`, consistent with every other filter added across Stories 05-07.

## Test Plan

1. **Create file: `tests/AzmCrm.Application.Tests/Features/Tickets/ChangeTicketStatusCommandHandlerTests.cs`** — `Change_to_new_status_persists_and_logs_StatusChanged_history`; `Change_to_same_status_persists_without_extra_history_row`; `Change_missing_ticket_throws_NotFoundException`.
2. **Create file: `tests/AzmCrm.Application.Tests/Features/Tickets/EscalateTicketCommandHandlerTests.cs`** — `Escalate_sets_IsEscalated_and_EscalatedOn_and_logs_history`; `Escalate_already_escalated_ticket_updates_EscalatedOn_and_logs_another_history_row` (asserts two `Escalated` rows after calling the handler twice); `Escalate_missing_ticket_throws_NotFoundException`; `Escalate_without_reason_uses_default_description`.
3. **Edit `tests/AzmCrm.Application.Tests/Features/Tickets/GetTicketsListQueryHandlerTests.cs`** (Stories 05-06) — add `List_filters_by_isEscalated`.
4. **Create file: `tests/AzmCrm.Application.Tests/Features/Tickets/ChangeTicketStatusCommandValidatorTests.cs`** — `Undefined_Status_fails` (cast an out-of-range `int` to `TicketStatus`); `Valid_command_passes`.
5. **Create file: `tests/AzmCrm.Application.Tests/Features/Tickets/EscalateTicketCommandValidatorTests.cs`** — `Reason_over_1000_chars_fails`; `Null_Reason_passes`; `Valid_command_passes`.
6. All new tests use `TestApplicationDbContext.Create()`, `StubCurrentUserService`, and `StubLocalizationService` exactly as established in Story 05 — no additional test doubles are needed for this story (`ChangeTicketStatusCommandHandler`/`EscalateTicketCommandHandler` depend only on `IApplicationDbContext`, not `IIdentityQueryService`).

## Migration / Rollback

- The migration generated in Task 3 only **adds** the `IsEscalated` (non-nullable, default `false`) and `EscalatedOn` (nullable) columns plus one index to the existing `Tickets` table — additive, safe on top of Story 06's `AddTicketAssignment` migration.
- **Rollback**: `dotnet ef database update AddTicketAssignment --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` drops both columns and the index. No data loss beyond the escalation flags themselves.
- **Half-applied state**: same existing behavior — `DatabaseInitializer` logs and rethrows on migration failure, so the app fails to start rather than running against a partial schema.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Migration applies cleanly:** `dotnet ef database update --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` (or let the API apply it automatically on startup).
4. **Manual smoke test:** create a ticket (Story 05), then `PUT /api/tickets/{id}/status` with `{"status":"InProgress"}`, confirm 200 and `GET /api/tickets/{id}` reflects the new status; `POST /api/tickets/{id}/escalate` with `{"reason":"SLA breach imminent"}`, confirm 200 and `GET /api/tickets/{id}` shows `isEscalated:true` and a populated `escalatedOn`; `GET /api/tickets?isEscalated=true` returns it; `GET /api/tickets/{id}/history` shows both the `StatusChanged` and `Escalated` entries.

## Done Criteria

- [ ] `Ticket.IsEscalated`/`EscalatedOn`, EF configuration, and migration exist and apply cleanly on top of Story 06's schema.
- [ ] `PUT /api/tickets/{id}/status` changes status and logs one `TicketHistory` row per actual change (no-op on an unchanged value).
- [ ] `POST /api/tickets/{id}/escalate` sets `IsEscalated`/`EscalatedOn` and always logs a `TicketHistory` row, even on repeated calls.
- [ ] `GET /api/tickets/{id}` and `GET /api/tickets` responses include `isEscalated`/`escalatedOn`, and `GET /api/tickets?isEscalated=true` filters correctly.
- [ ] All new and updated handler and validator unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

This completes KAN-2's five acceptance criteria across Stories 05-07: create/track (05), categories/priorities (05), assign to agents (06), status/escalation (07), and complete history (05's `GET /api/tickets/{id}/history`, populated by every mutating command across all three stories).
