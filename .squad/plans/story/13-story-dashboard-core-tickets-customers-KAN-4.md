# Story 13 — Dashboard Core: My Assigned Tickets & Customer Summary (Story: KAN-4)

## Prerequisites

- [05-story-ticket-core-crud-KAN-2.md](05-story-ticket-core-crud-KAN-2.md) completed: requires the `Ticket` entity, `IApplicationDbContext.Tickets`, and the `GetTicketsListQueryHandler`/`TicketListItemDto` shape this story's dashboard query mirrors.
- [06-story-ticket-assignment-KAN-2.md](06-story-ticket-assignment-KAN-2.md) completed: requires `Ticket.AssignedToUserId` — the column this story's "my tickets" filter is built on.
- [01-story-customer-core-crud-KAN-1.md](01-story-customer-core-crud-KAN-1.md) completed: requires the `Customer` entity and `IApplicationDbContext.Customers` — this story batch-loads `Customer` rows to embed a summary alongside each dashboard ticket.
- This is the first story in the KAN-4 ("Agent Dashboard") slice. It introduces the `DashboardController` and `Features/Dashboard` folder that [14-story-agent-tasks-reminders-KAN-4.md](14-story-agent-tasks-reminders-KAN-4.md), [15-story-quick-reply-templates-KAN-4.md](15-story-quick-reply-templates-KAN-4.md), and [16-story-ticket-collaboration-comments-KAN-4.md](16-story-ticket-collaboration-comments-KAN-4.md) do **not** depend on or edit — those three add their own independent controllers/entities and can be implemented in any order relative to this one and each other.

## Story Goal

Give a support agent a single "my dashboard" view over tickets already assigned to them, satisfying two of KAN-4's five acceptance criteria in one pass: **"View all assigned tickets in one place"** and **"Access customer information from the dashboard"**. No new entity or migration is introduced — this story is a read-only composition over the existing `Ticket` and `Customer` tables, the same way KAN-2 Story 06 added an `assignedToUserId` filter on top of `Ticket` rather than a new aggregate.

Outcomes:
1. `GET /api/dashboard/tickets` returns a paginated list of tickets where `AssignedToUserId` equals the caller's own user id (resolved from the bearer token via `ICurrentUserService`, not a query parameter — an agent can only ever see their own dashboard), optionally filtered by `status`. Each item embeds a `CustomerSummaryDto` (name, company, email, phone) for the ticket's customer, so the frontend never has to make a second round trip per ticket to show "who is this ticket about."
2. `GET /api/dashboard/summary` returns a per-status count of the caller's assigned tickets plus a total escalated count, giving the dashboard's headline numbers (e.g. "12 open, 3 escalated") without the frontend paging through the full list.

**Not in scope**: any endpoint that lets one agent view another agent's dashboard (no `userId` query parameter — this is deliberately "my dashboard only," unlike `GET /api/tickets?assignedToUserId=...` from Story 06 which remains available for a supervisor view), customer full-profile embedding (only the summary fields used by `CustomerListItemDto` are embedded, not the full `CustomerDto` address fields), and any change to `TicketsController`, `GetTicketsListQuery`, or `TicketListItemDto` — this story adds new files only.

## Context — Read These Files First

1. [src/AzmCrm.Domain/Features/Tickets/Ticket.cs](../../../src/AzmCrm.Domain/Features/Tickets/Ticket.cs) — read in full (19 lines). `AssignedToUserId` (line 14, nullable `Guid`) is the filter column; `CustomerId` (line 8) is the FK this story batch-resolves against `Customer`.
2. [src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQueryHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQueryHandler.cs) — read in full (77 lines, current end-state after KAN-2 Story 07). `GetMyTicketsQueryHandler` (Task 2 below) follows the exact same "filter → count → page → batch-resolve related data → project" shape, but resolves `Customer` rows from `dbContext.Customers` directly (same `DbContext`, no `IIdentityQueryService`-style external abstraction needed) instead of resolving assignee names.
3. [src/AzmCrm.Application/Features/Tickets/DTOs/TicketListItemDto.cs](../../../src/AzmCrm.Application/Features/Tickets/DTOs/TicketListItemDto.cs) — read in full (17 lines, current end-state: `Id, CustomerId, Title, Category, Priority, Status, CreatedOn, AssignedToUserId, AssignedToUserName, IsEscalated, EscalatedOn`). `DashboardTicketDto` (Task 1) carries the same ticket fields minus `CustomerId`/`AssignedToUserId`/`AssignedToUserName` (redundant on "my dashboard" — the caller already knows both), plus an embedded `CustomerSummaryDto`.
4. [src/AzmCrm.Domain/Features/Tickets/TicketStatus.cs](../../../src/AzmCrm.Domain/Features/Tickets/TicketStatus.cs) — read in full (12 lines): `New, Open, InProgress, OnHold, Resolved, Closed, Reopened`. `DashboardSummaryDto` (Task 1) has one count field per value, plus `EscalatedCount`.
5. [src/AzmCrm.Domain/Features/Customers/Customer.cs](../../../src/AzmCrm.Domain/Features/Customers/Customer.cs) — read in full (17 lines). `CustomerSummaryDto` carries `Id, FullName, CompanyName, Email, PhoneNumber` — the same four descriptive fields `CustomerListItemDto` already exposes (see item 6), just under a new name scoped to the Dashboard feature.
6. [src/AzmCrm.Application/Features/Customers/DTOs/CustomerListItemDto.cs](../../../src/AzmCrm.Application/Features/Customers/DTOs/CustomerListItemDto.cs) — read in full (10 lines). Exact field set `CustomerSummaryDto` copies (`Id, FullName, CompanyName, Email, PhoneNumber`), minus `CreatedOn` which the dashboard doesn't need.
7. [src/AzmCrm.Infrastructure/Data/Configurations/CustomerConfiguration.cs](../../../src/AzmCrm.Infrastructure/Data/Configurations/CustomerConfiguration.cs) — line 32, `builder.HasQueryFilter(c => !c.IsDeleted);`. Confirms `dbContext.Customers` silently excludes a soft-deleted customer from the batch lookup in Task 2 — see Edge Cases for what that means for a ticket whose customer was later soft-deleted.
8. [src/AzmCrm.Application/Shared/Interfaces/ICurrentUserService.cs](../../../src/AzmCrm.Application/Shared/Interfaces/ICurrentUserService.cs) — read in full (13 lines). `Guid? UserId` (line 5) is what both new handlers use as the "my" filter; already registered as `services.AddScoped<ICurrentUserService, CurrentUserService>();` at [src/AzmCrm.Infrastructure/DependencyInjection.cs:104](../../../src/AzmCrm.Infrastructure/DependencyInjection.cs) — **no DI registration change needed**.
9. [src/AzmCrm.Application/Features/Customers/Commands/DeleteCustomer/DeleteCustomerCommandHandler.cs](../../../src/AzmCrm.Application/Features/Customers/Commands/DeleteCustomer/DeleteCustomerCommandHandler.cs) — lines 19-20 (`customer.DeletedBy = currentUserService.UserId ?? Guid.Empty;`). Existing precedent in this codebase for the `?? Guid.Empty` fallback when `UserId` is unexpectedly null; `GetMyTicketsQueryHandler`/`GetDashboardSummaryQueryHandler` use the same fallback rather than throwing (see Edge Cases).
10. [src/AzmCrm.Application/Shared/Models/PaginatedResult.cs](../../../src/AzmCrm.Application/Shared/Models/PaginatedResult.cs) — read in full (12 lines). Used for `GetMyTicketsQuery`'s response.
11. [src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQueryValidator.cs](../../../src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQueryValidator.cs) — read in full (18 lines). Exact paging-range rule pair (`PageNumber >= 1`, `PageSize` between 1 and 100) `GetMyTicketsQueryValidator` reuses.
12. [src/AzmCrm.API/Controllers/Base/ApiControllerBase.cs](../../../src/AzmCrm.API/Controllers/Base/ApiControllerBase.cs) — read in full (29 lines). `DashboardController` inherits `[Authorize]` + `[Route("api/[controller]")]` (→ `api/dashboard`) from here, same as every other controller — no `[AllowAnonymous]` action is added by this story.
13. [src/AzmCrm.API/Controllers/TicketsController.cs](../../../src/AzmCrm.API/Controllers/TicketsController.cs) — lines 44-58 (`GetList`). Exact controller-action shape `DashboardController.GetMyTickets` mirrors (query-string paging + one enum filter parameter, `mediator.Send` + `ToResult`).
14. [src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs) — read in full (27 lines, current end-state after KAN-3). **Not edited by this story** — both new handlers only read `Tickets` and `Customers`, both already exposed.
15. [src/AzmCrm.Application/AssemblyInfo.cs](../../../src/AzmCrm.Application/AssemblyInfo.cs) (3 lines) — `[assembly: InternalsVisibleTo("AzmCrm.Application.Tests")]` already covers the new `internal sealed class` handlers; no change needed.
16. Grep for `AddMediatR` and `AddValidatorsFromAssembly` in `src/AzmCrm.Application/DependencyInjection.cs` (lines 13-16) — confirms both new handlers and the new validator are discovered by assembly scan; no manual registration needed.
17. [tests/AzmCrm.Application.Tests/TestDoubles/StubCurrentUserService.cs](../../../tests/AzmCrm.Application.Tests/TestDoubles/StubCurrentUserService.cs) — read in full. Reused as-is to set a fixed `UserId` in tests.

## Implementation tasks

### 1 — Application layer: DTOs

**Create file: `src/AzmCrm.Application/Features/Dashboard/DTOs/CustomerSummaryDto.cs`**

```csharp
namespace AzmCrm.Application.Features.Dashboard.DTOs;

public sealed record CustomerSummaryDto(
    Guid Id,
    string FullName,
    string? CompanyName,
    string? Email,
    string? PhoneNumber
);
```

**Create file: `src/AzmCrm.Application/Features/Dashboard/DTOs/DashboardTicketDto.cs`**

```csharp
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Dashboard.DTOs;

public sealed record DashboardTicketDto(
    Guid Id,
    string Title,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    DateTime CreatedOn,
    bool IsEscalated,
    DateTime? EscalatedOn,
    CustomerSummaryDto? Customer
);
```

`Customer` is nullable — see Edge Cases for the soft-deleted-customer case.

**Create file: `src/AzmCrm.Application/Features/Dashboard/DTOs/DashboardSummaryDto.cs`**

```csharp
namespace AzmCrm.Application.Features.Dashboard.DTOs;

public sealed record DashboardSummaryDto(
    int TotalAssigned,
    int New,
    int Open,
    int InProgress,
    int OnHold,
    int Resolved,
    int Closed,
    int Reopened,
    int EscalatedCount
);
```

### 2 — Application layer: GetMyTickets query

**Create file: `src/AzmCrm.Application/Features/Dashboard/Queries/GetMyTickets/GetMyTicketsQuery.cs`**

```csharp
using AzmCrm.Application.Features.Dashboard.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Dashboard.Queries.GetMyTickets;

public sealed record GetMyTicketsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    TicketStatus? Status = null
) : IRequest<Result<PaginatedResult<DashboardTicketDto>>>;
```

**Create file: `src/AzmCrm.Application/Features/Dashboard/Queries/GetMyTickets/GetMyTicketsQueryHandler.cs`**

```csharp
using AzmCrm.Application.Features.Dashboard.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Dashboard.Queries.GetMyTickets;

internal sealed class GetMyTicketsQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetMyTicketsQuery, Result<PaginatedResult<DashboardTicketDto>>>
{
    public async Task<Result<PaginatedResult<DashboardTicketDto>>> Handle(
        GetMyTicketsQuery request, CancellationToken ct)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;

        var query = dbContext.Tickets.Where(t => t.AssignedToUserId == userId);

        if (request.Status is not null)
            query = query.Where(t => t.Status == request.Status);

        var totalCount = await query.CountAsync(ct);

        var tickets = await query
            .OrderByDescending(t => t.CreatedOn)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var customerIds = tickets.Select(t => t.CustomerId).Distinct();
        var customers = await dbContext.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        var items = tickets.Select(t => new DashboardTicketDto(
            t.Id, t.Title, t.Category, t.Priority, t.Status, t.CreatedOn, t.IsEscalated, t.EscalatedOn,
            customers.TryGetValue(t.CustomerId, out var customer)
                ? new CustomerSummaryDto(customer.Id, customer.FullName, customer.CompanyName, customer.Email, customer.PhoneNumber)
                : null));

        var result = new PaginatedResult<DashboardTicketDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<DashboardTicketDto>>.Success(result);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Dashboard/Queries/GetMyTickets/GetMyTicketsQueryValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Dashboard.Queries.GetMyTickets;

public sealed class GetMyTicketsQueryValidator : AbstractValidator<GetMyTicketsQuery>
{
    public GetMyTicketsQueryValidator(ILocalizationService localization)
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage(localization[LocalizationKeys.Validation.MustBeGreaterThan, "Page Number", 0]);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page Size must be between 1 and 100.");
    }
}
```

### 3 — Application layer: GetDashboardSummary query

**Create file: `src/AzmCrm.Application/Features/Dashboard/Queries/GetDashboardSummary/GetDashboardSummaryQuery.cs`**

```csharp
using AzmCrm.Application.Features.Dashboard.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Dashboard.Queries.GetDashboardSummary;

public sealed record GetDashboardSummaryQuery : IRequest<Result<DashboardSummaryDto>>;
```

**Create file: `src/AzmCrm.Application/Features/Dashboard/Queries/GetDashboardSummary/GetDashboardSummaryQueryHandler.cs`**

```csharp
using AzmCrm.Application.Features.Dashboard.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Dashboard.Queries.GetDashboardSummary;

internal sealed class GetDashboardSummaryQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetDashboardSummaryQuery, Result<DashboardSummaryDto>>
{
    public async Task<Result<DashboardSummaryDto>> Handle(GetDashboardSummaryQuery request, CancellationToken ct)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;

        var myTickets = dbContext.Tickets.Where(t => t.AssignedToUserId == userId);

        var statusCounts = await myTickets
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, ct);

        var escalatedCount = await myTickets.CountAsync(t => t.IsEscalated, ct);

        int CountFor(TicketStatus status) => statusCounts.GetValueOrDefault(status);

        var dto = new DashboardSummaryDto(
            TotalAssigned: statusCounts.Values.Sum(),
            New: CountFor(TicketStatus.New),
            Open: CountFor(TicketStatus.Open),
            InProgress: CountFor(TicketStatus.InProgress),
            OnHold: CountFor(TicketStatus.OnHold),
            Resolved: CountFor(TicketStatus.Resolved),
            Closed: CountFor(TicketStatus.Closed),
            Reopened: CountFor(TicketStatus.Reopened),
            EscalatedCount: escalatedCount);

        return Result<DashboardSummaryDto>.Success(dto);
    }
}
```

No validator is needed — `GetDashboardSummaryQuery` has no parameters to validate.

### 4 — API layer

**Create file: `src/AzmCrm.API/Controllers/DashboardController.cs`**

```csharp
using AzmCrm.API.Controllers.Base;
using AzmCrm.Application.Features.Dashboard.DTOs;
using AzmCrm.Application.Features.Dashboard.Queries.GetDashboardSummary;
using AzmCrm.Application.Features.Dashboard.Queries.GetMyTickets;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AzmCrm.API.Controllers;

public sealed class DashboardController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("tickets")]
    [ProducesResponseType(typeof(Result<PaginatedResult<DashboardTicketDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTickets(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] TicketStatus? status = null, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetMyTicketsQuery(pageNumber, pageSize, status), ct);
        return ToResult(result);
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(Result<DashboardSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var result = await mediator.Send(new GetDashboardSummaryQuery(), ct);
        return ToResult(result);
    }
}
```

Both actions rely on the base class's default `[Authorize]` — there is no anonymous access to any dashboard data.

## Edge Cases & Failure Modes

- **A ticket's customer was soft-deleted after the ticket was created** — `CustomerConfiguration.HasQueryFilter(c => !c.IsDeleted)` (`CustomerConfiguration.cs:29`) means the batch `dbContext.Customers.Where(c => customerIds.Contains(c.Id))` lookup in `GetMyTicketsQueryHandler` silently excludes that customer. The handler's `customers.TryGetValue(...)` returns `false` in that case, so `DashboardTicketDto.Customer` is `null` for that ticket rather than throwing or omitting the ticket itself — document this for frontend consumers so they render a "customer unavailable" state instead of assuming `Customer` is always present.
- **`ICurrentUserService.UserId` is null** despite the controller's `[Authorize]` attribute (should not happen with a valid JWT, since `CurrentUserService` always resolves an id from the token's claims, but guarded defensively) — both handlers fall back to `Guid.Empty` (mirroring `DeleteCustomerCommandHandler`'s existing `?? Guid.Empty` convention, Context item 9) rather than throwing; since no real ticket is ever assigned to `Guid.Empty`, this degrades to "zero tickets, all-zero summary" rather than a 500.
- **An agent has zero assigned tickets** — `GetMyTicketsQuery` returns a `PaginatedResult` with `TotalCount = 0` and an empty `Items`; `GetDashboardSummaryQuery` returns a `DashboardSummaryDto` with every count at `0`, not an error.
- **`PageNumber`/`PageSize` out of range** — enforced by `GetMyTicketsQueryValidator` via the existing `ValidationBehavior` pipeline, turned into a 400 before the handler runs, identical to every other paginated query in this codebase.
- **A ticket is escalated but not in a "resolved-family" status** — `EscalatedCount` in `GetDashboardSummaryQueryHandler` is a separate `CountAsync(t => t.IsEscalated, ct)`, not a status bucket, so an escalated `InProgress` ticket is counted in both `InProgress` and `EscalatedCount` — this is intentional (escalation is orthogonal to status per KAN-2 Story 07's `Ticket.IsEscalated` design) and should be documented for the frontend as "escalated" being a cross-cutting flag, not a status value.
- **No `userId` query parameter exists on either endpoint** — an agent cannot view a teammate's dashboard through this controller even by guessing another id; that capability, if ever needed, is already served by the pre-existing `GET /api/tickets?assignedToUserId=...` (Story 06), which this story does not restrict or change.

## Test Plan

All new tests live in `tests/AzmCrm.Application.Tests/`, following the existing `TestApplicationDbContext`/`StubCurrentUserService` infrastructure — no `IApplicationDbContext`/`TestApplicationDbContext` schema change is needed since this story adds no new entity.

1. **Create file: `tests/AzmCrm.Application.Tests/Features/Dashboard/GetMyTicketsQueryHandlerTests.cs`** — `Returns_only_tickets_assigned_to_current_user` (seed one ticket assigned to the `StubCurrentUserService`'s `UserId` and one assigned to a different random `Guid`, assert only the first is returned); `Filters_by_status`; `Embeds_customer_summary_for_each_ticket` (seed a `Customer` and a `Ticket` referencing it, assert `DashboardTicketDto.Customer` matches); `Customer_is_null_when_customer_was_soft_deleted` (seed a ticket, then set the referenced `Customer.IsDeleted = true` directly and save, assert the returned ticket's `Customer` is `null` but the ticket itself still appears); `Returns_empty_page_when_no_tickets_assigned`.
2. **Create file: `tests/AzmCrm.Application.Tests/Features/Dashboard/GetDashboardSummaryQueryHandlerTests.cs`** — `Counts_tickets_by_status_for_current_user_only` (seed tickets in several statuses for the current user and one ticket for a different user, assert per-status counts and `TotalAssigned` exclude the other user's ticket); `EscalatedCount_counts_escalated_tickets_regardless_of_status`; `Returns_all_zero_summary_when_no_tickets_assigned`.
3. **Create file: `tests/AzmCrm.Application.Tests/Features/Dashboard/GetMyTicketsQueryValidatorTests.cs`** — `PageNumber_less_than_1_fails`; `PageSize_out_of_range_fails`; `Valid_query_passes` — construct the validator with `StubLocalizationService`.

## Migration / Rollback

No migration in this story — no new entity or column is introduced; both new queries read existing `Tickets`/`Customers` tables as-is.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Manual smoke test:** create a customer and a ticket for that customer (KAN-1/KAN-2), assign the ticket to your own logged-in user via `PUT /api/tickets/{id}/assign` with your own user id, then `GET /api/dashboard/tickets` and confirm the ticket appears with an embedded `customer` object; `GET /api/dashboard/tickets?status=Open` and confirm filtering; `GET /api/dashboard/summary` and confirm the counts match (one ticket in whatever status it was created with, `escalatedCount` 0 unless escalated).

## Done Criteria

- [ ] `GET /api/dashboard/tickets` returns only the caller's assigned tickets, each with an embedded customer summary (or `null` if the customer was soft-deleted), and supports `status` filtering and paging.
- [ ] `GET /api/dashboard/summary` returns accurate per-status counts and an escalated count scoped to the caller only.
- [ ] Neither endpoint accepts a way to view another agent's dashboard.
- [ ] All new handler and validator unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 14.**
