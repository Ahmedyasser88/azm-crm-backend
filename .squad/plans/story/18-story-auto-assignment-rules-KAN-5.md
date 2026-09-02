# Story 18 — Auto-Assign Tickets Based on Rules (Story: KAN-5)

## Prerequisites

- [17-story-sla-policies-KAN-5.md](17-story-sla-policies-KAN-5.md) completed: this story edits `CreateTicketCommandHandler` a second time (Story 17 added the SLA-policy lookup; this story appends the rule-matching/auto-assign block right after it), and follows the exact `Sla` feature folder's CRUD pattern for its own `Automation` feature folder. Independent of Story 17's actual SLA fields — this story adds no dependency on `SlaPolicyId`/`ResponseDueOn`/etc.
- [06-story-ticket-assignment-KAN-2.md](06-story-ticket-assignment-KAN-2.md) completed: requires `Ticket.AssignedToUserId`, `IIdentityQueryService` (used here to validate an `AssignmentRule`'s target agent exists), and the `TicketHistoryEventType.Assigned` history-logging shape this story's auto-assign path reuses.

## Story Goal

Let a support manager configure ordered rules that automatically assign a newly created ticket to a specific agent based on its category and/or priority, satisfying KAN-5's "Auto-assign tickets based on rules" acceptance criterion.

Outcomes:
1. `POST/PUT/DELETE /api/assignment-rules` and `GET /api/assignment-rules`, `GET /api/assignment-rules/{id}` let a manager manage `AssignmentRule` rows, each optionally scoped to a `TicketCategory` and/or `TicketPriority` (`null` on either means "any"), pointing at one target agent, ordered by an explicit `EvaluationOrder`.
2. When `CreateTicketCommand` runs, the active `AssignmentRule` with the lowest `EvaluationOrder` whose `Category`/`Priority` both match (or are `null`) the new ticket is applied: the ticket is assigned to that rule's `AssignedToUserId`, and a `TicketHistory` row is logged exactly as a manual `AssignTicketCommand` would, with a description noting it was rule-driven.
3. A ticket matching no active rule is created unassigned — identical to today's behavior before this story.

**Not in scope**: round-robin or load-balanced assignment across a pool of agents (every rule targets exactly one fixed agent); re-evaluating rules against already-existing tickets when a rule is created/updated/deleted (rules only ever apply at ticket-creation time going forward); validating that the target agent is an active/available agent beyond existing via `IIdentityQueryService` (mirrors Story 06's identical scope note); and any UI/manual "re-run auto-assignment" action.

## Context — Read These Files First

1. [17-story-sla-policies-KAN-5.md](17-story-sla-policies-KAN-5.md) — read in full, especially its "Context" list entries 1-3 (the `QuickReplyTemplate`-derived team-shared CRUD shape) and its edit to `CreateTicketCommandHandler` — this story's own edit to that same handler lands immediately after Story 17's SLA-policy block.
2. [src/AzmCrm.Application/Features/Tickets/Commands/AssignTicket/AssignTicketCommandHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Commands/AssignTicket/AssignTicketCommandHandler.cs) (60 lines, read in full) — the `IIdentityQueryService.GetUserInfoAsync` validate-agent-exists-and-get-name pattern this story's `CreateAssignmentRuleCommandHandler` reuses to validate `AssignedToUserId`, and the `TicketHistoryEventType.Assigned` history-row shape (`OldValue`/`NewValue` as stringified user ids) `CreateTicketCommandHandler`'s new auto-assign block reuses.
3. [src/AzmCrm.Application/Shared/Interfaces/IIdentityQueryService.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IIdentityQueryService.cs) — read in full (18 lines). `GetUserInfoAsync(userId, ct)` returns `(string? FullName, string? Email)`; `FullName is null` means the user id doesn't exist.
4. [src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs) — as edited by Story 17 (SLA-policy lookup block already inserted). This story appends an `AssignmentRule` lookup after that block, before `dbContext.Tickets.Add(ticket)`.
5. [src/AzmCrm.Domain/Features/Tickets/TicketCategory.cs](../../../src/AzmCrm.Domain/Features/Tickets/TicketCategory.cs) and [TicketPriority.cs](../../../src/AzmCrm.Domain/Features/Tickets/TicketPriority.cs) — read in full. `AssignmentRule.Category`/`Priority` are nullable versions of these two enums.
6. [src/AzmCrm.Domain/Features/Tickets/TicketHistoryEventType.cs](../../../src/AzmCrm.Domain/Features/Tickets/TicketHistoryEventType.cs) — read in full (9 lines). This story reuses the existing `Assigned` member; no enum change needed.
7. [src/AzmCrm.Infrastructure/Data/Configurations/TicketConfiguration.cs](../../../src/AzmCrm.Infrastructure/Data/Configurations/TicketConfiguration.cs) as edited by Story 17 — the `HasOne<ApplicationUser>().WithMany().HasForeignKey(...)` block (for `AssignedToUserId`) is the exact shape `AssignmentRuleConfiguration`'s own FK to `ApplicationUser` follows.
8. [src/AzmCrm.API/Controllers/AgentTasksController.cs](../../../src/AzmCrm.API/Controllers/AgentTasksController.cs) — a second worked example (alongside Story 17's `SlaPoliciesController`) of the kebab-case `[Route("api/agent-tasks")]` override `AssignmentRulesController`'s `[Route("api/assignment-rules")]` follows.

## Implementation tasks

### 1 — Domain layer

**Create file: `src/AzmCrm.Domain/Features/Automation/AssignmentRule.cs`**

```csharp
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Domain.Features.Automation;

public sealed class AssignmentRule : BaseEntity
{
    public required string Name { get; set; }
    public TicketCategory? Category { get; set; }
    public TicketPriority? Priority { get; set; }
    public required Guid AssignedToUserId { get; set; }
    public int EvaluationOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
```

### 2 — Application layer

**Create file: `src/AzmCrm.Application/Features/Automation/DTOs/AssignmentRuleDto.cs`**

```csharp
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Automation.DTOs;

public sealed record AssignmentRuleDto(
    Guid Id, string Name, TicketCategory? Category, TicketPriority? Priority,
    Guid AssignedToUserId, string? AssignedToUserName, int EvaluationOrder, bool IsActive,
    DateTime CreatedOn, DateTime? UpdatedOn);
```

**Create file: `src/AzmCrm.Application/Features/Automation/DTOs/AssignmentRuleListItemDto.cs`**

```csharp
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Automation.DTOs;

public sealed record AssignmentRuleListItemDto(
    Guid Id, string Name, TicketCategory? Category, TicketPriority? Priority,
    Guid AssignedToUserId, string? AssignedToUserName, int EvaluationOrder, bool IsActive);
```

**Create file: `src/AzmCrm.Application/Features/Automation/DTOs/CreateAssignmentRuleRequest.cs`**

```csharp
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Automation.DTOs;

public sealed record CreateAssignmentRuleRequest(
    string Name, TicketCategory? Category, TicketPriority? Priority,
    Guid AssignedToUserId, int EvaluationOrder);
```

**Create file: `src/AzmCrm.Application/Features/Automation/DTOs/UpdateAssignmentRuleRequest.cs`**

```csharp
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Automation.DTOs;

public sealed record UpdateAssignmentRuleRequest(
    string Name, TicketCategory? Category, TicketPriority? Priority,
    Guid AssignedToUserId, int EvaluationOrder, bool IsActive);
```

**Create file: `src/AzmCrm.Application/Features/Automation/Commands/CreateAssignmentRule/CreateAssignmentRuleCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Automation.Commands.CreateAssignmentRule;

public sealed record CreateAssignmentRuleCommand(
    string Name, TicketCategory? Category, TicketPriority? Priority,
    Guid AssignedToUserId, int EvaluationOrder) : IRequest<Result<Guid>>;
```

**Create file: `src/AzmCrm.Application/Features/Automation/Commands/CreateAssignmentRule/CreateAssignmentRuleCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Automation;
using MediatR;

namespace AzmCrm.Application.Features.Automation.Commands.CreateAssignmentRule;

internal sealed class CreateAssignmentRuleCommandHandler(
    IApplicationDbContext dbContext,
    IIdentityQueryService identityQueryService)
    : IRequestHandler<CreateAssignmentRuleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateAssignmentRuleCommand request, CancellationToken ct)
    {
        var (fullName, _) = await identityQueryService.GetUserInfoAsync(request.AssignedToUserId, ct);
        if (fullName is null)
            throw new NotFoundException($"Agent '{request.AssignedToUserId}' was not found.");

        var rule = new AssignmentRule
        {
            Name = request.Name,
            Category = request.Category,
            Priority = request.Priority,
            AssignedToUserId = request.AssignedToUserId,
            EvaluationOrder = request.EvaluationOrder
        };

        dbContext.AssignmentRules.Add(rule);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(rule.Id);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Automation/Commands/CreateAssignmentRule/CreateAssignmentRuleCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Automation.Commands.CreateAssignmentRule;

public sealed class CreateAssignmentRuleCommandValidator : AbstractValidator<CreateAssignmentRuleCommand>
{
    public CreateAssignmentRuleCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Name"])
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Name", 200]);

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage(localization[LocalizationKeys.Validation.InvalidValue, "Category"])
            .When(x => x.Category is not null);

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage(localization[LocalizationKeys.Validation.InvalidValue, "Priority"])
            .When(x => x.Priority is not null);

        RuleFor(x => x.AssignedToUserId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Assigned To User Id"]);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Automation/Commands/UpdateAssignmentRule/UpdateAssignmentRuleCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Automation.Commands.UpdateAssignmentRule;

public sealed record UpdateAssignmentRuleCommand(
    Guid Id, string Name, TicketCategory? Category, TicketPriority? Priority,
    Guid AssignedToUserId, int EvaluationOrder, bool IsActive) : IRequest<Result>;
```

**Create file: `src/AzmCrm.Application/Features/Automation/Commands/UpdateAssignmentRule/UpdateAssignmentRuleCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Automation.Commands.UpdateAssignmentRule;

internal sealed class UpdateAssignmentRuleCommandHandler(
    IApplicationDbContext dbContext,
    IIdentityQueryService identityQueryService)
    : IRequestHandler<UpdateAssignmentRuleCommand, Result>
{
    public async Task<Result> Handle(UpdateAssignmentRuleCommand request, CancellationToken ct)
    {
        var rule = await dbContext.AssignmentRules.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new NotFoundException($"Assignment rule '{request.Id}' was not found.");

        var (fullName, _) = await identityQueryService.GetUserInfoAsync(request.AssignedToUserId, ct);
        if (fullName is null)
            throw new NotFoundException($"Agent '{request.AssignedToUserId}' was not found.");

        rule.Name = request.Name;
        rule.Category = request.Category;
        rule.Priority = request.Priority;
        rule.AssignedToUserId = request.AssignedToUserId;
        rule.EvaluationOrder = request.EvaluationOrder;
        rule.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Automation/Commands/UpdateAssignmentRule/UpdateAssignmentRuleCommandValidator.cs`** — same rules as `CreateAssignmentRuleCommandValidator` plus `RuleFor(x => x.Id).NotEmpty()...`.

**Create file: `src/AzmCrm.Application/Features/Automation/Commands/DeleteAssignmentRule/DeleteAssignmentRuleCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Automation.Commands.DeleteAssignmentRule;

public sealed record DeleteAssignmentRuleCommand(Guid Id) : IRequest<Result>;
```

**Create file: `src/AzmCrm.Application/Features/Automation/Commands/DeleteAssignmentRule/DeleteAssignmentRuleCommandHandler.cs`** — copy [DeleteQuickReplyTemplateCommandHandler.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Commands/DeleteQuickReplyTemplate/DeleteQuickReplyTemplateCommandHandler.cs) exactly, substituting `dbContext.AssignmentRules`/`Assignment rule '{request.Id}'`.

**Create file: `src/AzmCrm.Application/Features/Automation/Commands/DeleteAssignmentRule/DeleteAssignmentRuleCommandValidator.cs`** — copy [DeleteQuickReplyTemplateCommandValidator.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Commands/DeleteQuickReplyTemplate/DeleteQuickReplyTemplateCommandValidator.cs) exactly.

**Create file: `src/AzmCrm.Application/Features/Automation/Queries/GetAssignmentRuleById/GetAssignmentRuleByIdQuery.cs`** and **`GetAssignmentRuleByIdQueryHandler.cs`** — same shape as Story 17's `GetSlaPolicyByIdQuery`/Handler, resolving `AssignedToUserName` via `IIdentityQueryService.GetUserInfoAsync` (the handler therefore takes both `IApplicationDbContext` and `IIdentityQueryService`, following [GetTicketByIdQueryHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs)'s exact "resolve id to a display name" pattern).

**Create file: `src/AzmCrm.Application/Features/Automation/Queries/GetAssignmentRulesList/GetAssignmentRulesListQuery.cs`**

```csharp
using AzmCrm.Application.Features.Automation.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Automation.Queries.GetAssignmentRulesList;

public sealed record GetAssignmentRulesListQuery(
    int PageNumber = 1, int PageSize = 20,
    TicketCategory? Category = null, TicketPriority? Priority = null, bool? IsActive = null
) : IRequest<Result<PaginatedResult<AssignmentRuleListItemDto>>>;
```

**Create file: `src/AzmCrm.Application/Features/Automation/Queries/GetAssignmentRulesList/GetAssignmentRulesListQueryHandler.cs`** — same shape as [GetTicketsListQueryHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQueryHandler.cs)'s batch name-resolution pattern: filter by `Category`/`Priority`/`IsActive` when non-null, `.OrderBy(r => r.EvaluationOrder)` (evaluation order is the natural read order for a manager reviewing rule precedence), batch-resolve `AssignedToUserName` via `identityQueryService.GetUsersInfoAsync(...)`, project into `AssignmentRuleListItemDto`.

**Create file: `src/AzmCrm.Application/Features/Automation/Queries/GetAssignmentRulesList/GetAssignmentRulesListQueryValidator.cs`** — copy [GetAgentTasksListQueryValidator.cs](../../../src/AzmCrm.Application/Features/AgentTasks/Queries/GetAgentTasksList/GetAgentTasksListQueryValidator.cs)'s `PageNumber`/`PageSize` rules exactly.

**Edit file: `src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs`** — after Story 17's SLA-policy block and before `dbContext.Tickets.Add(ticket)`, add:

```csharp
var assignmentRule = await dbContext.AssignmentRules
    .Where(r => r.IsActive)
    .Where(r => r.Category == null || r.Category == request.Category)
    .Where(r => r.Priority == null || r.Priority == request.Priority)
    .OrderBy(r => r.EvaluationOrder)
    .FirstOrDefaultAsync(ct);

if (assignmentRule is not null)
    ticket.AssignedToUserId = assignmentRule.AssignedToUserId;
```

and, after `dbContext.TicketHistories.Add(new TicketHistory { ... Description = "Ticket created." ... });`, add a second history entry conditionally:

```csharp
if (assignmentRule is not null)
    dbContext.TicketHistories.Add(new TicketHistory
    {
        TicketId = ticket.Id,
        EventType = TicketHistoryEventType.Assigned,
        Description = $"Ticket auto-assigned by rule '{assignmentRule.Name}'.",
        OldValue = null,
        NewValue = assignmentRule.AssignedToUserId.ToString()
    });
```

**Edit file: `src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs`** — add `using AzmCrm.Domain.Features.Automation;` and, after `DbSet<SlaPolicy> SlaPolicies { get; }`:

```csharp
DbSet<AssignmentRule> AssignmentRules { get; }
```

### 3 — Infrastructure layer

**Create file: `src/AzmCrm.Infrastructure/Data/Configurations/AssignmentRuleConfiguration.cs`**

```csharp
using AzmCrm.Domain.Features.Automation;
using AzmCrm.Domain.Features.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class AssignmentRuleConfiguration : IEntityTypeConfiguration<AssignmentRule>
{
    public void Configure(EntityTypeBuilder<AssignmentRule> builder)
    {
        builder.ToTable("AssignmentRules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Category)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(r => r.Priority)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(r => !r.IsDeleted);

        builder.HasIndex(r => new { r.IsActive, r.EvaluationOrder });
    }
}
```

Note `OnDelete(DeleteBehavior.Restrict)` (not `SetNull`, unlike `Ticket.AssignedToUserId`) because `AssignmentRule.AssignedToUserId` is `required Guid` (non-nullable) — a rule cannot exist with no target agent, so deleting the referenced `ApplicationUser` row must be blocked at the database level rather than silently nulling a non-nullable column.

**Edit file: `src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs`** — add `using AzmCrm.Domain.Features.Automation;` and, after the `SlaPolicies` line:

```csharp
public DbSet<AssignmentRule> AssignmentRules => Set<AssignmentRule>();
```

**Generate migration:**

```bash
dotnet ef migrations add AddAssignmentRules --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API
```

### 4 — API layer

**Create file: `src/AzmCrm.API/Controllers/AssignmentRulesController.cs`** — `[Route("api/assignment-rules")]`, same `Create`/`GetById`/`GetList`/`Update`/`Delete` shape as [SlaPoliciesController.cs](../../../src/AzmCrm.API/Controllers/SlaPoliciesController.cs) (Story 17), `GetList` taking `[FromQuery] TicketCategory? category`, `[FromQuery] TicketPriority? priority`, `[FromQuery] bool? isActive`.

### 5 — Test doubles

**Edit file: `tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs`** — add `using AzmCrm.Domain.Features.Automation;`, add `public DbSet<AssignmentRule> AssignmentRules => Set<AssignmentRule>();` after the `SlaPolicies` line, and add `modelBuilder.Entity<AssignmentRule>().HasQueryFilter(r => !r.IsDeleted);` after the `SlaPolicy` query filter line.

## Edge Cases & Failure Modes

- **Multiple active rules match a ticket** (e.g. one rule for `Category=Technical` with no `Priority`, another for `Priority=Urgent` with no `Category`, and a new ticket is both `Technical` and `Urgent`) — `CreateTicketCommandHandler`'s `OrderBy(r => r.EvaluationOrder).FirstOrDefaultAsync(ct)` always picks exactly one: the lowest `EvaluationOrder` among all matches. Two active rules sharing the same `EvaluationOrder` both matching a ticket resolve deterministically only insofar as the database's tie-break is stable for a given dataset — not guaranteed globally; document this as a configuration hazard for the manager (avoid duplicate `EvaluationOrder` values), not a bug to fix here.
- **No active rule matches** — `assignmentRule is null`; `ticket.AssignedToUserId` stays `null` and no second `TicketHistory` row is added, identical to pre-Story-18 behavior.
- **A rule's `Category` and `Priority` are both `null`** — a valid "catch-all" rule matching every ticket; useful as a low-priority (high `EvaluationOrder`) default-assignee fallback.
- **`AssignedToUserId` on create/update does not resolve to an existing user** — `CreateAssignmentRuleCommandHandler`/`UpdateAssignmentRuleCommandHandler` both throw `NotFoundException` via the same `IIdentityQueryService.GetUserInfoAsync` check `AssignTicketCommandHandler` (Story 06) uses, before any row is written.
- **Deleting or deactivating a rule after tickets have already been auto-assigned by it** — no retroactive effect; already-assigned tickets keep their `AssignedToUserId`. Only future `CreateTicketCommand` calls stop matching the removed/deactivated rule.
- **The matched rule's target agent is later deactivated (`ApplicationUser.IsActive = false`) or deleted** — not checked by `CreateTicketCommandHandler`'s auto-assign block (mirrors Story 06's identical, already-documented gap: "enforcing that an assigned `ApplicationUser` is an active agent" is out of scope for the whole KAN-2/KAN-5 ticket-assignment surface). A rule pointing at a *deleted* `ApplicationUser` row is prevented outright by `AssignmentRuleConfiguration`'s `OnDelete(DeleteBehavior.Restrict)` FK — the delete itself fails at the database level while any `AssignmentRule` still references that user.
- **`Category`/`Priority` sent as an unrecognized string** — same framework-level 400 as Story 07 documented for `ChangeTicketStatusCommand.Status` (`JsonStringEnumConverter` rejects the request body before MediatR sees it); the validators' `IsInEnum()` `.When(...)` rules are defense-in-depth for an out-of-range integer reaching the command directly.

## Test Plan

1. **Create file: `tests/AzmCrm.Application.Tests/Features/Automation/CreateAssignmentRuleCommandHandlerTests.cs`** — `Create_persists_rule_and_returns_id`; `Create_with_unknown_agent_throws_NotFoundException`.
2. **Create file: `tests/AzmCrm.Application.Tests/Features/Automation/UpdateAssignmentRuleCommandHandlerTests.cs`** — `Update_persists_changes`; `Update_missing_rule_throws_NotFoundException`; `Update_with_unknown_agent_throws_NotFoundException`.
3. **Create file: `tests/AzmCrm.Application.Tests/Features/Automation/DeleteAssignmentRuleCommandHandlerTests.cs`** — `Delete_soft_deletes_rule`; `Delete_missing_rule_throws_NotFoundException`.
4. **Create file: `tests/AzmCrm.Application.Tests/Features/Automation/GetAssignmentRulesListQueryHandlerTests.cs`** — `List_returns_rules_ordered_by_EvaluationOrder`; `List_filters_by_category`; `List_filters_by_priority`; `List_filters_by_isActive`.
5. **Edit `tests/AzmCrm.Application.Tests/Features/Tickets/CreateTicketCommandHandlerTests.cs`** — add `Create_matching_active_rule_auto_assigns_and_logs_history` (seed an `AssignmentRule` for the ticket's category, assert `AssignedToUserId` set and one `Assigned` `TicketHistory` row logged in addition to the `Created` row); add `Create_with_no_matching_rule_leaves_ticket_unassigned`; add `Create_picks_lowest_EvaluationOrder_among_multiple_matches` (seed two matching active rules with different `EvaluationOrder`, assert the lower one wins).
6. Use a stub `IIdentityQueryService` returning a fixed `(FullName, Email)` for any requested id (mirrors `AssignTicketCommandHandlerTests`' existing stub from Story 06) across all new handler tests that need one.

## Migration / Rollback

- The migration generated in Task 3 only **adds** the new `AssignmentRules` table (with its FK to `AspNetUsers` and one composite index) — additive, safe on top of Story 17's `AddSlaPolicies` migration. No existing table is altered.
- **Rollback**: `dotnet ef database update AddSlaPolicies --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` drops the `AssignmentRules` table.
- **Half-applied state**: same existing behavior — `DatabaseInitializer` logs and rethrows on migration failure, so the app fails to start rather than running against a partial schema.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Migration applies cleanly:** `dotnet ef database update --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` (or let the API apply it automatically on startup).
4. **Manual smoke test:** `POST /api/assignment-rules` with `{"name":"Billing to Alice","category":"Billing","priority":null,"assignedToUserId":"<existing agent id>","evaluationOrder":1}`, confirm 201; create a ticket (Story 05) with `"category":"Billing"`, confirm `GET /api/tickets/{id}` shows `assignedToUserId` set to that agent and `GET /api/tickets/{id}/history` shows both a `Created` and an `Assigned` entry; create a ticket with a different category, confirm it remains unassigned.

## Done Criteria

- [ ] `AssignmentRule` entity, EF configuration, and migration exist and apply cleanly on top of Story 17's schema.
- [ ] `POST/PUT/DELETE /api/assignment-rules` and `GET /api/assignment-rules`, `GET /api/assignment-rules/{id}` work, validating the target agent exists on create/update.
- [ ] Creating a ticket matching an active rule auto-assigns it to that rule's agent and logs an `Assigned` `TicketHistory` row; no match leaves the ticket unassigned.
- [ ] Among multiple matching active rules, the one with the lowest `EvaluationOrder` wins.
- [ ] All new and updated handler and validator unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

This story satisfies KAN-5's "Auto-assign tickets based on rules" acceptance criterion.
