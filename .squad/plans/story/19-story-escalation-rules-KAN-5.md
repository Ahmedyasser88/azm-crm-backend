# Story 19 — Escalation Rules for Overdue Tickets (Story: KAN-5)

## Prerequisites

- [17-story-sla-policies-KAN-5.md](17-story-sla-policies-KAN-5.md) completed: requires `Ticket.ResolutionDueOn`/`IsEscalated`/`EscalatedOn` and the `SlaPolicy`-derived team-shared CRUD shape this story's `EscalationRule` CRUD follows.
- [07-story-ticket-status-escalation-KAN-2.md](07-story-ticket-status-escalation-KAN-2.md) completed: requires `EscalateTicketCommandHandler`'s escalation shape (`IsEscalated = true`, `EscalatedOn` stamped, a `TicketHistoryEventType.Escalated` row logged, **not** a no-op on repeat) — this story's automatic scan reproduces that exact effect for a system-triggered escalation rather than calling the command a second time.
- Independent of [18-story-auto-assignment-rules-KAN-5.md](18-story-auto-assignment-rules-KAN-5.md) — auto-assignment and automatic escalation are separate concerns that both happen to add a feature folder under `src/AzmCrm.Domain/Features/Automation/`. This story is written assuming Story 18 landed first only for the shared-folder/shared-file editing pattern it establishes (`IApplicationDbContext.cs` gets one more `DbSet<T>` line, `TestApplicationDbContext.cs` one more query filter); it adds no dependency on `AssignmentRule` itself.

## Story Goal

Let a support manager configure how many minutes past a ticket's resolution due date it takes before the ticket is automatically escalated, satisfying KAN-5's "Configure escalation rules for overdue tickets" acceptance criterion. A recurring background scan finds overdue, not-yet-escalated tickets and escalates them exactly as a manual `POST /api/tickets/{id}/escalate` would (Story 07), except the reason is system-generated.

Outcomes:
1. `POST/PUT/DELETE /api/escalation-rules` and `GET /api/escalation-rules`, `GET /api/escalation-rules/{id}` let a manager manage `EscalationRule` rows, each optionally scoped to a `TicketPriority` (`null` = applies to every priority as a catch-all) with an `OverdueMinutes` grace period.
2. A recurring background scan (interval configurable via `SlaMonitoring:IntervalMinutes` in `appsettings.json`, default 5) finds every open ticket (`Status` not `Resolved`/`Closed`) with a `ResolutionDueOn` in the past by at least the matching active `EscalationRule.OverdueMinutes` and not yet `IsEscalated`, and escalates it: `IsEscalated = true`, `EscalatedOn` stamped, a `TicketHistoryEventType.Escalated` row logged with a description naming the rule.
3. The scan logic itself is exposed as `ScanSlaBreachesCommand`/`ScanSlaBreachesCommandHandler` (`IRequest<Result<int>>`, returning the count of tickets escalated) so it is unit-testable via `TestApplicationDbContext` exactly like every other handler in this codebase, independent of the timer that drives it.

**Not in scope**: escalating tickets based on `ResponseDueOn` breaches (that's a plain alert, not an escalation — see [20-story-sla-breach-alerts-KAN-5.md](20-story-sla-breach-alerts-KAN-5.md)); a manual "trigger scan now" endpoint; de-escalation; and any change to `EscalateTicketCommand`/`EscalateTicketCommandHandler` themselves (they remain exactly as Story 07 left them — this story adds a second, independent code path that produces the same ticket-level effect for the automatic case).

## Context — Read These Files First

1. [07-story-ticket-status-escalation-KAN-2.md](07-story-ticket-status-escalation-KAN-2.md) — read in full, especially `EscalateTicketCommandHandler`'s body and its "Escalating an already-escalated ticket is *not* a no-op" Edge Case — this story's scan **skips** already-escalated tickets entirely (`!t.IsEscalated` in the query), so it never re-triggers on a ticket a human already escalated, avoiding that no-op/not-no-op asymmetry altogether rather than having to replicate it.
2. [17-story-sla-policies-KAN-5.md](17-story-sla-policies-KAN-5.md) — read in full, especially its `SlaPolicy` CRUD (Context entries 1-3) — `EscalationRule`'s CRUD follows the exact same team-shared shape, and `Ticket.ResolutionDueOn` (added by that story) is what this story's scan compares against.
3. [src/AzmCrm.Application/DependencyInjection.cs](../../../src/AzmCrm.Application/DependencyInjection.cs) — read in full (18 lines). Confirms `AddMediatR` scans `Assembly.GetExecutingAssembly()` (the Application assembly) for handlers — `ScanSlaBreachesCommandHandler` needs no separate registration.
4. [src/AzmCrm.Infrastructure/DependencyInjection.cs](../../../src/AzmCrm.Infrastructure/DependencyInjection.cs) — read in full. This story adds one `services.Configure<SlaMonitoringSettings>(...)` line and one `services.AddHostedService<SlaMonitoringBackgroundService>();` line at the end, following the exact pattern already used there for `SmtpSettings`/`IEmailSender` and `WhatsAppSettings`/`IWhatsAppProvider`.
5. [src/AzmCrm.Infrastructure/AzmCrm.Infrastructure.csproj](../../../src/AzmCrm.Infrastructure/AzmCrm.Infrastructure.csproj) — read in full (29 lines). `<FrameworkReference Include="Microsoft.AspNetCore.App" />` already pulls in `Microsoft.Extensions.Hosting.Abstractions` (for `BackgroundService`, `IServiceScopeFactory`) and `Microsoft.Extensions.Options` (for `IOptions<T>`) — no new package reference is needed for this story's hosted service.
6. [src/AzmCrm.API/appsettings.json](../../../src/AzmCrm.API/appsettings.json) — read in full. This story adds one top-level `"SlaMonitoring": { "IntervalMinutes": 5 }` section, following the exact placement/shape of the existing `"Smtp"`/`"WhatsApp"`/`"Sms"` sections.
7. [src/AzmCrm.Domain/Features/Tickets/TicketStatus.cs](../../../src/AzmCrm.Domain/Features/Tickets/TicketStatus.cs) — read in full (7 values). The scan's "open" filter is `t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed`.
8. [src/AzmCrm.Application/Features/Tickets/Commands/EscalateTicket/EscalateTicketCommandHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Commands/EscalateTicket/EscalateTicketCommandHandler.cs) — read in full (36 lines). `ScanSlaBreachesCommandHandler`'s per-ticket escalation block (`IsEscalated = true; EscalatedOn = ...; TicketHistories.Add(new TicketHistory { EventType = TicketHistoryEventType.Escalated, ... })`) mirrors this handler's shape exactly, differing only in the `Description` text and in batching all `SaveChangesAsync` calls into one at the end of the scan instead of one per ticket.
9. [src/AzmCrm.Infrastructure/Identity/CurrentUserService.cs](../../../src/AzmCrm.Infrastructure/Identity/CurrentUserService.cs) lines 16-27 — confirms `IHttpContextAccessor.HttpContext` is `null` outside a request (e.g. inside this story's background service's own DI scope), so `ICurrentUserService.UserId` safely returns `null` there; `ApplicationDbContext.SaveChangesAsync` (lines 49-65) already falls back to `Guid.Empty` for `CreatedBy`/`UpdatedBy` in that case — no change needed to either file, but see Edge Cases.

## Implementation tasks

### 1 — Domain layer

**Create file: `src/AzmCrm.Domain/Features/Automation/EscalationRule.cs`**

```csharp
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Domain.Features.Automation;

public sealed class EscalationRule : BaseEntity
{
    public required string Name { get; set; }
    public TicketPriority? Priority { get; set; }
    public required int OverdueMinutes { get; set; }
    public bool IsActive { get; set; } = true;
}
```

### 2 — Application layer: Escalation rule CRUD

**Create file: `src/AzmCrm.Application/Features/Automation/DTOs/EscalationRuleDto.cs`**

```csharp
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Automation.DTOs;

public sealed record EscalationRuleDto(
    Guid Id, string Name, TicketPriority? Priority, int OverdueMinutes, bool IsActive,
    DateTime CreatedOn, DateTime? UpdatedOn);
```

**Create file: `src/AzmCrm.Application/Features/Automation/DTOs/EscalationRuleListItemDto.cs`**

```csharp
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Automation.DTOs;

public sealed record EscalationRuleListItemDto(
    Guid Id, string Name, TicketPriority? Priority, int OverdueMinutes, bool IsActive);
```

**Create file: `src/AzmCrm.Application/Features/Automation/DTOs/CreateEscalationRuleRequest.cs`**

```csharp
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Automation.DTOs;

public sealed record CreateEscalationRuleRequest(string Name, TicketPriority? Priority, int OverdueMinutes);
```

**Create file: `src/AzmCrm.Application/Features/Automation/DTOs/UpdateEscalationRuleRequest.cs`**

```csharp
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Automation.DTOs;

public sealed record UpdateEscalationRuleRequest(
    string Name, TicketPriority? Priority, int OverdueMinutes, bool IsActive);
```

**Create file: `src/AzmCrm.Application/Features/Automation/Commands/CreateEscalationRule/CreateEscalationRuleCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Automation.Commands.CreateEscalationRule;

public sealed record CreateEscalationRuleCommand(string Name, TicketPriority? Priority, int OverdueMinutes)
    : IRequest<Result<Guid>>;
```

**Create file: `src/AzmCrm.Application/Features/Automation/Commands/CreateEscalationRule/CreateEscalationRuleCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Automation;
using MediatR;

namespace AzmCrm.Application.Features.Automation.Commands.CreateEscalationRule;

internal sealed class CreateEscalationRuleCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreateEscalationRuleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateEscalationRuleCommand request, CancellationToken ct)
    {
        var rule = new EscalationRule
        {
            Name = request.Name,
            Priority = request.Priority,
            OverdueMinutes = request.OverdueMinutes
        };

        dbContext.EscalationRules.Add(rule);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(rule.Id);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Automation/Commands/CreateEscalationRule/CreateEscalationRuleCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Automation.Commands.CreateEscalationRule;

public sealed class CreateEscalationRuleCommandValidator : AbstractValidator<CreateEscalationRuleCommand>
{
    public CreateEscalationRuleCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Name"])
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Name", 200]);

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage(localization[LocalizationKeys.Validation.InvalidValue, "Priority"])
            .When(x => x.Priority is not null);

        RuleFor(x => x.OverdueMinutes)
            .GreaterThanOrEqualTo(0)
            .WithMessage(localization[LocalizationKeys.Validation.MustBeGreaterThan, "Overdue Minutes", -1]);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Automation/Commands/UpdateEscalationRule/UpdateEscalationRuleCommand.cs`, `UpdateEscalationRuleCommandHandler.cs`, `UpdateEscalationRuleCommandValidator.cs`** — same shape as Story 17's `UpdateSlaPolicyCommand`/Handler/Validator (load-or-404, overwrite fields, save; no uniqueness constraint on `Priority` — unlike `SlaPolicy`, multiple active `EscalationRule`s may target the same `Priority` and the scan always applies the tightest one, see Task 3).

**Create file: `src/AzmCrm.Application/Features/Automation/Commands/DeleteEscalationRule/DeleteEscalationRuleCommand.cs`, `DeleteEscalationRuleCommandHandler.cs`, `DeleteEscalationRuleCommandValidator.cs`** — copy [DeleteQuickReplyTemplateCommand.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Commands/DeleteQuickReplyTemplate/DeleteQuickReplyTemplateCommand.cs)/`Handler.cs`/`Validator.cs` exactly, substituting `dbContext.EscalationRules`/`Escalation rule '{request.Id}'`.

**Create file: `src/AzmCrm.Application/Features/Automation/Queries/GetEscalationRuleById/GetEscalationRuleByIdQuery.cs`, `GetEscalationRuleByIdQueryHandler.cs`** — same shape as Story 17's `GetSlaPolicyByIdQuery`/Handler (no `IIdentityQueryService` needed — `EscalationRule` has no user reference).

**Create file: `src/AzmCrm.Application/Features/Automation/Queries/GetEscalationRulesList/GetEscalationRulesListQuery.cs`, `GetEscalationRulesListQueryHandler.cs`, `GetEscalationRulesListQueryValidator.cs`** — same shape as Story 17's `GetSlaPoliciesListQuery`/Handler/Validator: filter by `Priority`/`IsActive`, order `.OrderBy(r => r.Priority)` (nulls — catch-all rules — sort first under EF Core's default Postgres `ORDER BY` semantics, which is an acceptable, undocumented-but-harmless tie-break since this is a small manager-facing list, not a matching-order-sensitive path — the scan in Task 3 does its own explicit tightest-match selection independent of this list's sort order).

### 3 — Application layer: the scan itself

**Create file: `src/AzmCrm.Application/Features/Automation/Commands/ScanSlaBreaches/ScanSlaBreachesCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Automation.Commands.ScanSlaBreaches;

/// <summary>
/// Finds overdue, not-yet-escalated tickets and escalates each one whose matching active
/// <see cref="AzmCrm.Domain.Features.Automation.EscalationRule"/> grace period has elapsed.
/// Returns the number of tickets escalated. Invoked on a timer by
/// <c>SlaMonitoringBackgroundService</c> (Infrastructure), and directly by tests.
/// </summary>
public sealed record ScanSlaBreachesCommand : IRequest<Result<int>>;
```

**Create file: `src/AzmCrm.Application/Features/Automation/Commands/ScanSlaBreaches/ScanSlaBreachesCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Automation.Commands.ScanSlaBreaches;

internal sealed class ScanSlaBreachesCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<ScanSlaBreachesCommand, Result<int>>
{
    public async Task<Result<int>> Handle(ScanSlaBreachesCommand request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var overdueTickets = await dbContext.Tickets
            .Where(t => !t.IsEscalated)
            .Where(t => t.ResolutionDueOn != null)
            .Where(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed)
            .Where(t => t.ResolutionDueOn! <= now)
            .ToListAsync(ct);

        if (overdueTickets.Count == 0)
            return Result<int>.Success(0);

        var activeRules = await dbContext.EscalationRules
            .Where(r => r.IsActive)
            .ToListAsync(ct);

        var escalatedCount = 0;

        foreach (var ticket in overdueTickets)
        {
            // Tightest match wins: a rule scoped to this ticket's exact Priority is preferred
            // over a null-Priority catch-all rule, mirroring AssignmentRule's "most specific
            // first" intent from Story 18 without sharing its EvaluationOrder mechanism (an
            // EscalationRule has no explicit ordering field — Priority-specificity alone
            // resolves ties, since at most one rule of each specificity level is expected to
            // ever matter for a given ticket).
            var rule = activeRules.FirstOrDefault(r => r.Priority == ticket.Priority)
                       ?? activeRules.FirstOrDefault(r => r.Priority == null);

            if (rule is null)
                continue;

            if (now < ticket.ResolutionDueOn!.Value.AddMinutes(rule.OverdueMinutes))
                continue;

            ticket.IsEscalated = true;
            ticket.EscalatedOn = now;

            dbContext.TicketHistories.Add(new TicketHistory
            {
                TicketId = ticket.Id,
                EventType = TicketHistoryEventType.Escalated,
                Description = $"Automatically escalated: resolution SLA breached (rule '{rule.Name}')."
            });

            escalatedCount++;
        }

        if (escalatedCount > 0)
            await dbContext.SaveChangesAsync(ct);

        return Result<int>.Success(escalatedCount);
    }
}
```

**Edit file: `src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs`** — add, after `DbSet<AssignmentRule> AssignmentRules { get; }`:

```csharp
DbSet<EscalationRule> EscalationRules { get; }
```

### 4 — Infrastructure layer

**Create file: `src/AzmCrm.Infrastructure/Data/Configurations/EscalationRuleConfiguration.cs`**

```csharp
using AzmCrm.Domain.Features.Automation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class EscalationRuleConfiguration : IEntityTypeConfiguration<EscalationRule>
{
    public void Configure(EntityTypeBuilder<EscalationRule> builder)
    {
        builder.ToTable("EscalationRules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Priority)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasQueryFilter(r => !r.IsDeleted);

        builder.HasIndex(r => new { r.IsActive, r.Priority });
    }
}
```

**Edit file: `src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs`** — add, after the `AssignmentRules` line:

```csharp
public DbSet<EscalationRule> EscalationRules => Set<EscalationRule>();
```

**Create file: `src/AzmCrm.Infrastructure/Sla/SlaMonitoringSettings.cs`**

```csharp
namespace AzmCrm.Infrastructure.Sla;

public sealed class SlaMonitoringSettings
{
    public const string SectionName = "SlaMonitoring";

    public int IntervalMinutes { get; set; } = 5;
}
```

**Create file: `src/AzmCrm.Infrastructure/Sla/SlaMonitoringBackgroundService.cs`**

```csharp
using AzmCrm.Application.Features.Automation.Commands.ScanSlaBreaches;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AzmCrm.Infrastructure.Sla;

/// <summary>
/// Polls for overdue tickets on a fixed interval and escalates them via
/// <see cref="ScanSlaBreachesCommand"/>. Runs in its own DI scope per tick, since
/// <c>IApplicationDbContext</c>/<c>IMediator</c> are scoped services and this service itself
/// is a long-lived singleton (ASP.NET Core's <see cref="BackgroundService"/> contract).
/// </summary>
internal sealed class SlaMonitoringBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<SlaMonitoringSettings> settings,
    ILogger<SlaMonitoringBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, settings.Value.IntervalMinutes));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var result = await mediator.Send(new ScanSlaBreachesCommand(), stoppingToken);

                if (result.IsSuccess && result.Data > 0)
                    logger.LogInformation("SLA monitoring scan escalated {Count} ticket(s).", result.Data);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failed scan must not crash the host or stop future ticks — the next
                // PeriodicTimer tick retries automatically.
                logger.LogError(ex, "SLA monitoring scan failed.");
            }
        }
    }
}
```

**Edit file: `src/AzmCrm.Infrastructure/DependencyInjection.cs`** — add `using AzmCrm.Infrastructure.Sla;` and, at the end of `AddInfrastructure` before `return services;`:

```csharp
services.Configure<SlaMonitoringSettings>(configuration.GetSection(SlaMonitoringSettings.SectionName));
services.AddHostedService<SlaMonitoringBackgroundService>();
```

**Edit file: `src/AzmCrm.API/appsettings.json`** — add a new top-level section (after `"Sms"`, before `"AllowedHosts"`):

```json
"SlaMonitoring": {
  "IntervalMinutes": 5
},
```

**Generate migration:**

```bash
dotnet ef migrations add AddEscalationRules --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API
```

### 5 — API layer

**Create file: `src/AzmCrm.API/Controllers/EscalationRulesController.cs`** — `[Route("api/escalation-rules")]`, same `Create`/`GetById`/`GetList`/`Update`/`Delete` shape as [SlaPoliciesController.cs](../../../src/AzmCrm.API/Controllers/SlaPoliciesController.cs), `GetList` taking `[FromQuery] TicketPriority? priority`, `[FromQuery] bool? isActive`.

### 6 — Test doubles

**Edit file: `tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs`** — add `public DbSet<EscalationRule> EscalationRules => Set<EscalationRule>();` after the `AssignmentRules` line, and `modelBuilder.Entity<EscalationRule>().HasQueryFilter(r => !r.IsDeleted);` after the `AssignmentRule` query filter line (both under the `using AzmCrm.Domain.Features.Automation;` already added by Story 18).

## Edge Cases & Failure Modes

- **No `EscalationRule` matches an overdue ticket's priority and no catch-all (`Priority == null`) rule is active** — `ScanSlaBreachesCommandHandler`'s `rule is null` guard `continue`s past that ticket; it stays un-escalated until a matching rule is created, even though it's overdue. This is intentional — escalation is opt-in per the acceptance criterion's "configure escalation rules," not automatic for every SLA-tracked ticket.
- **A ticket has no `SlaPolicyId`/`ResolutionDueOn`** (created before Story 17 shipped, or its priority had no active `SlaPolicy` at creation time) — excluded by the `t.ResolutionDueOn != null` filter; never considered for automatic escalation, regardless of how old the ticket is.
- **A ticket is already `IsEscalated`** (manually via Story 07's endpoint, or by a previous scan tick) — excluded by `!t.IsEscalated`; the scan never re-escalates or re-stamps `EscalatedOn` for it, sidestepping `EscalateTicketCommandHandler`'s documented "repeated escalation is not a no-op" behavior entirely, since the automatic path only ever escalates a ticket once.
- **A ticket becomes `Resolved`/`Closed` exactly at scan time, in a race with the scan reading it as still open** — EF Core's `SaveChangesAsync` inside `ChangeTicketStatusCommandHandler` and this handler's own `SaveChangesAsync` are independent transactions against the same row; the last write wins per normal EF Core/Postgres last-writer-wins semantics (no concurrency token is defined on `Ticket`, consistent with every other mutating `Ticket` command in this codebase). Worst case: a ticket resolved a moment before a scan tick reads it is escalated one tick late if the scan's read happened first — treated as an acceptable, self-correcting race (the next scan tick, or a human noticing, isn't blocked), not a bug to fix here.
- **The scan runs with zero overdue tickets** — `ScanSlaBreachesCommandHandler` returns `Result<int>.Success(0)` immediately after the first query, without querying `EscalationRules` at all or calling `SaveChangesAsync`.
- **`SlaMonitoringSettings.IntervalMinutes` configured as `0` or negative** — `SlaMonitoringBackgroundService`'s `Math.Max(1, ...)` floors the effective interval at one minute; `PeriodicTimer`'s constructor throws `ArgumentOutOfRangeException` for a non-positive `TimeSpan`, so this floor is load-bearing, not cosmetic.
- **A scan tick throws** (e.g. a transient database error) — caught by `SlaMonitoringBackgroundService`'s `catch (Exception ex) when (ex is not OperationCanceledException)`, logged, and the loop continues to the next `PeriodicTimer` tick; a cancellation during host shutdown (`OperationCanceledException`) is deliberately **not** caught, so it propagates and lets `ExecuteAsync` exit cleanly within the 15-second `ShutdownTimeout` already configured in `Program.cs`.
- **`ScanSlaBreachesCommandHandler` runs outside any HTTP request** (as it always does, via the background service's own DI scope) — `ICurrentUserService.UserId` resolves to `null` (see Context entry 9), so every entity this handler touches (`Ticket` via `Modified`, `TicketHistory` via `Added`) is stamped with `CreatedBy`/`UpdatedBy` = `Guid.Empty` by `ApplicationDbContext.SaveChangesAsync`'s existing fallback — the same fallback already relied on by any other non-HTTP-triggered write path in this codebase (there are none before this story; this is the first). No `Guid.Empty`-special-casing is added by this story — flag as a follow-up only if a real "system" `ApplicationUser` row is later wanted for audit-trail readability.

## Test Plan

1. **Create file: `tests/AzmCrm.Application.Tests/Features/Automation/CreateEscalationRuleCommandHandlerTests.cs`** — `Create_persists_rule_and_returns_id`.
2. **Create file: `tests/AzmCrm.Application.Tests/Features/Automation/UpdateEscalationRuleCommandHandlerTests.cs`** — `Update_persists_changes`; `Update_missing_rule_throws_NotFoundException`.
3. **Create file: `tests/AzmCrm.Application.Tests/Features/Automation/DeleteEscalationRuleCommandHandlerTests.cs`** — `Delete_soft_deletes_rule`; `Delete_missing_rule_throws_NotFoundException`.
4. **Create file: `tests/AzmCrm.Application.Tests/Features/Automation/GetEscalationRulesListQueryHandlerTests.cs`** — `List_filters_by_priority`; `List_filters_by_isActive`.
5. **Create file: `tests/AzmCrm.Application.Tests/Features/Automation/ScanSlaBreachesCommandHandlerTests.cs`** — the core test file for this story:
   - `Scan_escalates_ticket_past_its_grace_period` (seed a ticket with `ResolutionDueOn` in the past beyond an active priority-matched rule's `OverdueMinutes`; assert `IsEscalated`/`EscalatedOn` set and one `Escalated` `TicketHistory` row logged, and the handler returns `Result<int>.Success(1)`).
   - `Scan_does_not_escalate_ticket_still_within_grace_period` (`ResolutionDueOn` in the past, but not yet past `OverdueMinutes` beyond it).
   - `Scan_does_not_escalate_already_escalated_ticket`.
   - `Scan_does_not_escalate_ticket_with_null_ResolutionDueOn`.
   - `Scan_does_not_escalate_Resolved_or_Closed_ticket` (two cases, or a `[Theory]` over both statuses).
   - `Scan_prefers_priority_specific_rule_over_catchall_rule` (seed both an active `Priority = ticket.Priority` rule with a short `OverdueMinutes` and an active `Priority = null` rule with a long one on the same overdue ticket; assert the priority-specific rule's grace period is the one applied).
   - `Scan_skips_ticket_with_no_matching_rule_and_no_catchall`.
   - `Scan_with_no_overdue_tickets_returns_zero_without_touching_EscalationRules` (can be asserted indirectly by seeding zero `EscalationRule` rows and confirming success with count 0, rather than mocking the query).
6. All new tests use `TestApplicationDbContext.Create()` exactly as established in prior stories — `ScanSlaBreachesCommandHandler` depends only on `IApplicationDbContext`, no new test doubles needed. Seed `Ticket.ResolutionDueOn`/`Status`/`IsEscalated` directly on the entity (bypassing `CreateTicketCommand`) the same way `EscalateTicketCommandHandlerTests`' `SeedTicketAsync` helper does.

## Migration / Rollback

- The migration generated in Task 4 only **adds** the new `EscalationRules` table (with one composite index) — additive, safe on top of Story 18's `AddAssignmentRules` migration. No existing table is altered.
- **Rollback**: `dotnet ef database update AddAssignmentRules --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` drops the `EscalationRules` table.
- **Half-applied state**: same existing behavior — `DatabaseInitializer` logs and rethrows on migration failure, so the app fails to start rather than running against a partial schema.
- **Rolling back the background service itself** (if it needs to be disabled without a redeploy): setting `SlaMonitoring:IntervalMinutes` doesn't stop it — there's no "disabled" toggle in this story's scope. Removing the `services.AddHostedService<SlaMonitoringBackgroundService>();` line and redeploying is the only way to stop the scan entirely; flagged as a follow-up if an operational kill-switch is needed.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Migration applies cleanly:** `dotnet ef database update --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` (or let the API apply it automatically on startup).
4. **Manual smoke test:** `POST /api/escalation-rules` with `{"name":"Default overdue","priority":null,"overdueMinutes":0}`, confirm 201; create an SLA-tracked ticket (Story 17) whose `resolutionDueOn` is in the very near future, wait past both that due date and one `SlaMonitoring:IntervalMinutes` interval (temporarily set to `1` in `appsettings.Development.json` for this smoke test), confirm `GET /api/tickets/{id}` now shows `isEscalated:true` and `GET /api/tickets/{id}/history` shows an `Escalated` entry mentioning "Automatically escalated" and the rule's name — without ever calling `POST /api/tickets/{id}/escalate` manually.
5. **Log check:** confirm `logs/azm-crm-*.log` contains an `SLA monitoring scan escalated 1 ticket(s).` line at the expected interval.

## Done Criteria

- [ ] `EscalationRule` entity, EF configuration, and migration exist and apply cleanly on top of Story 18's schema.
- [ ] `POST/PUT/DELETE /api/escalation-rules` and `GET /api/escalation-rules`, `GET /api/escalation-rules/{id}` work.
- [ ] `ScanSlaBreachesCommandHandler` correctly identifies overdue, non-escalated, open tickets, picks the tightest-matching active rule, and escalates only those past their grace period — verified entirely through unit tests, independent of the timer.
- [ ] `SlaMonitoringBackgroundService` is registered, configurable via `SlaMonitoring:IntervalMinutes`, and survives a failed scan without crashing the host.
- [ ] All new and updated handler and validator unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

This story satisfies KAN-5's "Configure escalation rules for overdue tickets" acceptance criterion and gives [20-story-sla-breach-alerts-KAN-5.md](20-story-sla-breach-alerts-KAN-5.md) the exact scan tick it extends to also raise response-time breach alerts and persist/email notifications.
