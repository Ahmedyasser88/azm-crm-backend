# Story 20 — Trigger Alerts & Notifications for SLA Breaches (Story: KAN-5)

## Prerequisites

- [19-story-escalation-rules-KAN-5.md](19-story-escalation-rules-KAN-5.md) completed: requires `ScanSlaBreachesCommandHandler` and `SlaMonitoringBackgroundService` — this story edits the handler a second time (its own scan tick now also raises response-time breach alerts and persists+emails a notification for every escalation it performs) rather than adding a second timer.
- [17-story-sla-policies-KAN-5.md](17-story-sla-policies-KAN-5.md) completed: requires `Ticket.ResponseDueOn`/`RespondedOn` (the response-breach half of this story's detection) and the `SlaPolicy`-derived team-shared CRUD folder shape.
- Reuses `IEmailSender` from [09-story-email-channel-KAN-3.md](../story/09-story-email-channel-KAN-3.md) (`src/AzmCrm.Application/Shared/Interfaces/IEmailSender.cs`, implemented by `SmtpEmailSender`) — already registered in DI by `AddInfrastructure`, no changes needed to either.

## Story Goal

Automatically alert the right agent when a ticket breaches its SLA — either because nobody responded before `ResponseDueOn`, or because the ticket was just auto-escalated for missing `ResolutionDueOn` — satisfying KAN-5's "Trigger alerts and notifications for SLA breaches" acceptance criterion. Every breach is both emailed (best-effort, via the existing `IEmailSender`) and durably recorded as an `SlaBreachNotification` row so it's visible even if the email fails or the agent was offline.

Outcomes:
1. Every `ScanSlaBreaches` tick (Story 19's existing timer, no new schedule) now also finds open tickets whose `ResponseDueOn` has passed with `RespondedOn` still `null`, and — once per ticket, not once per tick — creates an `SlaBreachNotification` (`BreachType = ResponseOverdue`).
2. Every ticket the same tick escalates for a resolution breach (Story 19's existing logic, unchanged) also gets an `SlaBreachNotification` (`BreachType = ResolutionOverdue`) created alongside it.
3. For each new notification whose ticket has an `AssignedToUserId`, the handler resolves that agent's email via `IIdentityQueryService` and sends it via `IEmailSender.SendAsync`, recording `EmailSent = true`/`false` on the notification row; email failures are caught and logged, never fail the scan.
4. `GET /api/sla-breach-notifications` and `GET /api/sla-breach-notifications/{id}` let an agent or manager see the breach history, filterable by `ticketId`/`notifiedUserId`/`breachType`. Notifications are system-generated only — no create/update/delete endpoints.

**Not in scope**: in-app/real-time push notifications (e.g. over the existing `ChatHub` SignalR hub) — email plus a queryable REST list is this story's full notification surface; notifying anyone when a ticket has **no** `AssignedToUserId` (the notification row is still created for dashboard visibility, but no email is attempted — see Edge Cases); a "mark as read/acknowledged" action on a notification; and re-sending a failed email.

## Context — Read These Files First

1. [19-story-escalation-rules-KAN-5.md](19-story-escalation-rules-KAN-5.md) — read in full, especially `ScanSlaBreachesCommandHandler`'s current body (Task 3) — this story edits that exact method, adding a response-breach detection pass and notification creation calls around the existing resolution-escalation loop.
2. [src/AzmCrm.Application/Shared/Interfaces/IEmailSender.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IEmailSender.cs) — read in full (11 lines). `SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)` — this story's only outbound-email touchpoint; no new abstraction is introduced.
3. [src/AzmCrm.Infrastructure/Communications/SmtpEmailSender.cs](../../../src/AzmCrm.Infrastructure/Communications/SmtpEmailSender.cs) — read in full (30 lines). Confirms `SendAsync` can throw (`SmtpClient.SendMailAsync` — network/auth failures), which is exactly why Task 2 below wraps every call in a `try`/`catch`.
4. [src/AzmCrm.Application/Shared/Interfaces/IIdentityQueryService.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IIdentityQueryService.cs) — read in full. `GetUsersInfoAsync(IEnumerable<Guid> userIds, ct)` batch-resolves `(FullName, Email)` — used here exactly as [GetTicketsListQueryHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQueryHandler.cs) already uses it, to avoid one query per ticket.
5. [src/AzmCrm.Application/Features/Automation/Commands/ScanSlaBreaches/ScanSlaBreachesCommandHandler.cs](../../../src/AzmCrm.Application/Features/Automation/Commands/ScanSlaBreaches/ScanSlaBreachesCommandHandler.cs) (Story 19, ~55 lines) — read in full before editing; Task 2 below shows the full post-edit body.
6. [15-story-quick-reply-templates-KAN-4.md](15-story-quick-reply-templates-KAN-4.md) / [src/AzmCrm.API/Controllers/QuickReplyTemplatesController.cs](../../../src/AzmCrm.API/Controllers/QuickReplyTemplatesController.cs) — the `GetById`/`GetList` half of this controller shape (this story's `SlaBreachNotificationsController` has no `Create`/`Update`/`Delete` actions, since notifications are system-generated only).
7. [src/AzmCrm.API/appsettings.json](../../../src/AzmCrm.API/appsettings.json) "Smtp" section — `FromAddress`/`FromName` are already configured; this story's email subject/body composition needs no new settings.

## Implementation tasks

### 1 — Domain layer

**Create file: `src/AzmCrm.Domain/Features/Sla/SlaBreachType.cs`**

```csharp
namespace AzmCrm.Domain.Features.Sla;

public enum SlaBreachType
{
    ResponseOverdue,
    ResolutionOverdue
}
```

**Create file: `src/AzmCrm.Domain/Features/Sla/SlaBreachNotification.cs`**

```csharp
using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.Sla;

public sealed class SlaBreachNotification : BaseEntity
{
    public required Guid TicketId { get; init; }
    public required SlaBreachType BreachType { get; init; }
    public Guid? NotifiedUserId { get; init; }
    public required string Message { get; init; }
    public bool EmailSent { get; set; }
}
```

### 2 — Application layer

**Create file: `src/AzmCrm.Application/Features/Sla/DTOs/SlaBreachNotificationDto.cs`**

```csharp
using AzmCrm.Domain.Features.Sla;

namespace AzmCrm.Application.Features.Sla.DTOs;

public sealed record SlaBreachNotificationDto(
    Guid Id, Guid TicketId, SlaBreachType BreachType, Guid? NotifiedUserId,
    string? NotifiedUserName, string Message, bool EmailSent, DateTime CreatedOn);
```

**Create file: `src/AzmCrm.Application/Features/Sla/Queries/GetSlaBreachNotificationById/GetSlaBreachNotificationByIdQuery.cs`, `GetSlaBreachNotificationByIdQueryHandler.cs`** — same shape as Story 17's `GetSlaPolicyByIdQuery`/Handler, resolving `NotifiedUserName` via `IIdentityQueryService.GetUserInfoAsync` when `NotifiedUserId is not null` (following [GetTicketByIdQueryHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs)'s exact "resolve nullable id to nullable name" `if` block).

**Create file: `src/AzmCrm.Application/Features/Sla/Queries/GetSlaBreachNotificationsList/GetSlaBreachNotificationsListQuery.cs`**

```csharp
using AzmCrm.Application.Features.Sla.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Sla;
using MediatR;

namespace AzmCrm.Application.Features.Sla.Queries.GetSlaBreachNotificationsList;

public sealed record GetSlaBreachNotificationsListQuery(
    int PageNumber = 1, int PageSize = 20,
    Guid? TicketId = null, Guid? NotifiedUserId = null, SlaBreachType? BreachType = null
) : IRequest<Result<PaginatedResult<SlaBreachNotificationDto>>>;
```

**Create file: `src/AzmCrm.Application/Features/Sla/Queries/GetSlaBreachNotificationsList/GetSlaBreachNotificationsListQueryHandler.cs`** — same batch-name-resolution shape as [GetTicketsListQueryHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQueryHandler.cs): filter by `TicketId`/`NotifiedUserId`/`BreachType` when non-null, `.OrderByDescending(n => n.CreatedOn)` (a breach feed is read newest-first, same reasoning as [GetTicketsListQueryHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQueryHandler.cs)'s own `OrderByDescending(t => t.CreatedOn)`, unlike Story 17's alphabetical `SlaPolicy` list), batch-resolve `NotifiedUserName` for every non-null `NotifiedUserId` in the page via `identityQueryService.GetUsersInfoAsync(...)`, project into `SlaBreachNotificationDto`.

**Create file: `src/AzmCrm.Application/Features/Sla/Queries/GetSlaBreachNotificationsList/GetSlaBreachNotificationsListQueryValidator.cs`** — copy [GetAgentTasksListQueryValidator.cs](../../../src/AzmCrm.Application/Features/AgentTasks/Queries/GetAgentTasksList/GetAgentTasksListQueryValidator.cs)'s `PageNumber`/`PageSize` rules exactly.

**Edit file: `src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs`** — add `using AzmCrm.Domain.Features.Sla;` is already present (Story 17); add, after `DbSet<SlaPolicy> SlaPolicies { get; }`:

```csharp
DbSet<SlaBreachNotification> SlaBreachNotifications { get; }
```

**Edit file: `src/AzmCrm.Application/Features/Automation/Commands/ScanSlaBreaches/ScanSlaBreachesCommandHandler.cs`** — replace the whole handler body with:

```csharp
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Sla;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AzmCrm.Application.Features.Automation.Commands.ScanSlaBreaches;

internal sealed class ScanSlaBreachesCommandHandler(
    IApplicationDbContext dbContext,
    IIdentityQueryService identityQueryService,
    IEmailSender emailSender,
    ILogger<ScanSlaBreachesCommandHandler> logger)
    : IRequestHandler<ScanSlaBreachesCommand, Result<int>>
{
    public async Task<Result<int>> Handle(ScanSlaBreachesCommand request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var openTickets = await dbContext.Tickets
            .Where(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed)
            .Where(t => t.ResponseDueOn != null || t.ResolutionDueOn != null)
            .ToListAsync(ct);

        if (openTickets.Count == 0)
            return Result<int>.Success(0);

        var activeEscalationRules = await dbContext.EscalationRules
            .Where(r => r.IsActive)
            .ToListAsync(ct);

        var newNotifications = new List<SlaBreachNotification>();
        var escalatedCount = 0;

        foreach (var ticket in openTickets)
        {
            // Response breach: RespondedOn still null past ResponseDueOn. Alert-only — never
            // escalates — and fires at most once per ticket (guarded by the "already notified"
            // check below), unlike the resolution path which re-evaluates every tick until the
            // ticket is escalated.
            if (ticket.RespondedOn is null && ticket.ResponseDueOn is not null && now > ticket.ResponseDueOn
                && !await dbContext.SlaBreachNotifications.AnyAsync(
                    n => n.TicketId == ticket.Id && n.BreachType == SlaBreachType.ResponseOverdue, ct))
            {
                newNotifications.Add(new SlaBreachNotification
                {
                    TicketId = ticket.Id,
                    BreachType = SlaBreachType.ResponseOverdue,
                    NotifiedUserId = ticket.AssignedToUserId,
                    Message = $"Ticket '{ticket.Title}' has not been responded to and is past its response SLA."
                });
            }

            // Resolution breach: identical matching logic to Story 19's original scan, now also
            // recording a notification alongside every escalation it performs.
            if (!ticket.IsEscalated && ticket.ResolutionDueOn is not null)
            {
                var rule = activeEscalationRules.FirstOrDefault(r => r.Priority == ticket.Priority)
                           ?? activeEscalationRules.FirstOrDefault(r => r.Priority == null);

                if (rule is not null && now >= ticket.ResolutionDueOn.Value.AddMinutes(rule.OverdueMinutes))
                {
                    ticket.IsEscalated = true;
                    ticket.EscalatedOn = now;

                    dbContext.TicketHistories.Add(new TicketHistory
                    {
                        TicketId = ticket.Id,
                        EventType = TicketHistoryEventType.Escalated,
                        Description = $"Automatically escalated: resolution SLA breached (rule '{rule.Name}')."
                    });

                    escalatedCount++;

                    newNotifications.Add(new SlaBreachNotification
                    {
                        TicketId = ticket.Id,
                        BreachType = SlaBreachType.ResolutionOverdue,
                        NotifiedUserId = ticket.AssignedToUserId,
                        Message = $"Ticket '{ticket.Title}' was automatically escalated for missing its resolution SLA."
                    });
                }
            }
        }

        if (newNotifications.Count == 0)
            return Result<int>.Success(escalatedCount);

        var notifiedUserIds = newNotifications
            .Where(n => n.NotifiedUserId is not null)
            .Select(n => n.NotifiedUserId!.Value)
            .Distinct();
        var userInfo = await identityQueryService.GetUsersInfoAsync(notifiedUserIds, ct);

        foreach (var notification in newNotifications)
        {
            if (notification.NotifiedUserId is not null &&
                userInfo.TryGetValue(notification.NotifiedUserId.Value, out var info) &&
                info.Email is not null)
            {
                try
                {
                    await emailSender.SendAsync(info.Email, "SLA breach alert", notification.Message, ct);
                    notification.EmailSent = true;
                }
                catch (Exception ex)
                {
                    // A failed email must not lose the notification row or fail the whole scan —
                    // the breach is still visible via GET /api/sla-breach-notifications either way.
                    logger.LogError(ex, "Failed to send SLA breach email for ticket {TicketId}.", notification.TicketId);
                }
            }

            dbContext.SlaBreachNotifications.Add(notification);
        }

        await dbContext.SaveChangesAsync(ct);

        return Result<int>.Success(escalatedCount);
    }
}
```

Note the response-breach `AnyAsync` "already notified" check runs once per open ticket per tick — acceptable at this codebase's scale (matches the existing per-row query style already used elsewhere, e.g. `CreateTicketCommandHandler`'s `dbContext.Customers.AnyAsync(...)`), and is the only way to make the response-breach alert fire exactly once per ticket without a dedicated `Ticket.ResponseBreachNotifiedOn` column.

### 3 — Infrastructure layer

**Create file: `src/AzmCrm.Infrastructure/Data/Configurations/SlaBreachNotificationConfiguration.cs`**

```csharp
using AzmCrm.Domain.Features.Sla;
using AzmCrm.Domain.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class SlaBreachNotificationConfiguration : IEntityTypeConfiguration<SlaBreachNotification>
{
    public void Configure(EntityTypeBuilder<SlaBreachNotification> builder)
    {
        builder.ToTable("SlaBreachNotifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id)
            .ValueGeneratedNever();

        builder.Property(n => n.BreachType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(n => n.Message)
            .IsRequired()
            .HasMaxLength(1000);

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(n => n.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(n => !n.IsDeleted);

        builder.HasIndex(n => n.TicketId);
        builder.HasIndex(n => n.NotifiedUserId);
    }
}
```

**Edit file: `src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs`** — add, after the `SlaPolicies` line:

```csharp
public DbSet<SlaBreachNotification> SlaBreachNotifications => Set<SlaBreachNotification>();
```

**Generate migration:**

```bash
dotnet ef migrations add AddSlaBreachNotifications --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API
```

### 4 — API layer

**Create file: `src/AzmCrm.API/Controllers/SlaBreachNotificationsController.cs`**

```csharp
using AzmCrm.API.Controllers.Base;
using AzmCrm.Application.Features.Sla.DTOs;
using AzmCrm.Application.Features.Sla.Queries.GetSlaBreachNotificationById;
using AzmCrm.Application.Features.Sla.Queries.GetSlaBreachNotificationsList;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Sla;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AzmCrm.API.Controllers;

[Route("api/sla-breach-notifications")]
public sealed class SlaBreachNotificationsController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Result<SlaBreachNotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetSlaBreachNotificationByIdQuery(id), ct);
        return ToResult(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(Result<PaginatedResult<SlaBreachNotificationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] Guid? ticketId = null, [FromQuery] Guid? notifiedUserId = null,
        [FromQuery] SlaBreachType? breachType = null, CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetSlaBreachNotificationsListQuery(pageNumber, pageSize, ticketId, notifiedUserId, breachType), ct);
        return ToResult(result);
    }
}
```

### 5 — Test doubles

**Edit file: `tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs`** — add `public DbSet<SlaBreachNotification> SlaBreachNotifications => Set<SlaBreachNotification>();` after the `SlaPolicies` line, and `modelBuilder.Entity<SlaBreachNotification>().HasQueryFilter(n => !n.IsDeleted);` after the `SlaPolicy` query filter line.

**Create file: `tests/AzmCrm.Application.Tests/TestDoubles/StubEmailSender.cs`** (if no existing stub covers `IEmailSender` — check `tests/AzmCrm.Application.Tests/TestDoubles/` first; if one already exists from a KAN-3 story, reuse it instead) — a minimal in-memory recorder:

```csharp
using AzmCrm.Application.Shared.Interfaces;

namespace AzmCrm.Application.Tests.TestDoubles;

public sealed class StubEmailSender : IEmailSender
{
    public List<(string ToEmail, string Subject, string Body)> SentEmails { get; } = [];
    public bool ThrowOnSend { get; set; }

    public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        if (ThrowOnSend)
            throw new InvalidOperationException("Simulated SMTP failure.");

        SentEmails.Add((toEmail, subject, body));
        return Task.CompletedTask;
    }
}
```

## Edge Cases & Failure Modes

- **A ticket has no `AssignedToUserId`** — the notification row is still created with `NotifiedUserId = null` (so it remains visible via `GET /api/sla-breach-notifications`), but `identityQueryService.GetUsersInfoAsync` is never asked to resolve a `null` id (filtered out by `.Where(n => n.NotifiedUserId is not null)` before the batch call) and `EmailSent` stays `false` — no email is attempted, matching the Story Goal's explicit scope note.
- **`IEmailSender.SendAsync` throws** (SMTP down, invalid recipient, etc.) — caught per-notification inside the `foreach`; that notification's `EmailSent` stays `false`, the error is logged, and every other notification in the same tick is still attempted and the scan still completes and calls `SaveChangesAsync` once at the end. A single bad email never loses or blocks the rest of the batch.
- **The same ticket is still response-overdue on the next scan tick** — the `AnyAsync(n => n.TicketId == ... && n.BreachType == ResponseOverdue, ...)` guard means only the *first* tick after the breach creates a notification; later ticks see the row already exists and skip it. There is no re-alert/reminder mechanism in this story — flagged as a follow-up if the business wants a nagging reminder rather than a one-time alert.
- **A ticket is escalated manually (`POST /api/tickets/{id}/escalate`, Story 07) rather than by the scan** — no `SlaBreachNotification` is created for it; this story's `ResolutionOverdue` notification is only ever raised from inside `ScanSlaBreachesCommandHandler`'s own escalation branch, not from `EscalateTicketCommandHandler` (which Story 07 explicitly left unmodified — see [19-story-escalation-rules-KAN-5.md](19-story-escalation-rules-KAN-5.md)'s Prerequisites). A manual escalation is presumed to already be a deliberate, informed action by the agent doing it.
- **`GetUsersInfoAsync` returns an entry with a `null` `Email`** for a resolved user id (e.g. an `ApplicationUser` somehow has no email — `RequireUniqueEmail = true` is configured in `AddInfrastructure`, so this should not occur in practice, but the type is nullable) — `info.Email is not null` guards against calling `SendAsync(null!, ...)`; the notification row is still created with `EmailSent = false`.
- **Two scan ticks race on the same ticket's response breach** (only possible if `SlaMonitoring:IntervalMinutes` is misconfigured to overlap with a slow-running previous tick — not possible with the sequential `PeriodicTimer` loop from Story 19, since `WaitForNextTickAsync` only fires again after the previous iteration's body has returned) — not a real risk given `SlaMonitoringBackgroundService`'s single-threaded await loop; the `AnyAsync` guard is defense-in-depth, not the only thing preventing duplicates.
- **`Message` exceeds 1000 characters** — cannot happen from this story's own code (both `Message` templates interpolate only `ticket.Title`, itself capped at 200 characters by `TicketConfiguration`), but `SlaBreachNotificationConfiguration.Property(n => n.Message).HasMaxLength(1000)` still bounds the column defensively, consistent with every other free-text column in this codebase always declaring an explicit `HasMaxLength`.

## Test Plan

1. **Create file: `tests/AzmCrm.Application.Tests/Features/Sla/GetSlaBreachNotificationsListQueryHandlerTests.cs`** — `List_filters_by_ticketId`; `List_filters_by_notifiedUserId`; `List_filters_by_breachType`; `List_orders_newest_first`.
2. **Edit `tests/AzmCrm.Application.Tests/Features/Automation/ScanSlaBreachesCommandHandlerTests.cs`** (Story 19) — inject `StubEmailSender` and a stub `IIdentityQueryService` (returning a fixed `(FullName, Email)` for any requested id, per Story 18's precedent) into every existing test's handler construction, then add:
   - `Scan_creates_ResponseOverdue_notification_and_sends_email` (seed a ticket with `ResponseDueOn` in the past, `RespondedOn` null, `AssignedToUserId` set; assert one `SlaBreachNotification` row with `BreachType = ResponseOverdue`, `EmailSent = true`, and one entry in `StubEmailSender.SentEmails`).
   - `Scan_does_not_duplicate_ResponseOverdue_notification_on_second_tick` (call `Handle` twice against the same seeded state; assert still exactly one `SlaBreachNotification` row and one sent email).
   - `Scan_creates_ResolutionOverdue_notification_alongside_escalation` (reuse the existing `Scan_escalates_ticket_past_its_grace_period` seed; assert both the ticket is escalated *and* one `SlaBreachNotification` with `BreachType = ResolutionOverdue` exists).
   - `Scan_unassigned_ticket_creates_notification_with_null_NotifiedUserId_and_no_email` (assert `EmailSent = false` and `StubEmailSender.SentEmails` stays empty for that ticket).
   - `Scan_email_failure_does_not_prevent_notification_persistence_or_other_emails` (set `StubEmailSender.ThrowOnSend = true` for one seeded breach among two; assert both `SlaBreachNotification` rows are still persisted, with `EmailSent = false` for the failing one — note `ThrowOnSend` is a single flag here, so this test may need two ticket fixtures wired to two separate handler instances, or the plan may instead assert on a single-ticket scenario where the entire `SendAsync` call throws and `EmailSent` is `false` while the row is still persisted; either shape satisfies "a failed email doesn't lose the notification").
3. All new/edited tests continue using `TestApplicationDbContext.Create()`, following the exact seeding style of `EscalateTicketCommandHandlerTests.SeedTicketAsync`/Story 19's own seeding helpers.

## Migration / Rollback

- The migration generated in Task 3 only **adds** the new `SlaBreachNotifications` table (with an FK to `Tickets` and two indexes) — additive, safe on top of Story 19's `AddEscalationRules` migration. No existing table is altered.
- **Rollback**: `dotnet ef database update AddEscalationRules --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` drops the `SlaBreachNotifications` table.
- **Half-applied state**: same existing behavior — `DatabaseInitializer` logs and rethrows on migration failure, so the app fails to start rather than running against a partial schema.
- **Rolling back just the alerting behavior** without a schema rollback: reverting `ScanSlaBreachesCommandHandler` to its Story 19 body (no `IIdentityQueryService`/`IEmailSender`/notification creation) stops new alerts from being raised while leaving the `SlaBreachNotifications` table and any rows already in it untouched — a safe, code-only rollback path if email sending needs to be disabled quickly.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Migration applies cleanly:** `dotnet ef database update --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` (or let the API apply it automatically on startup).
4. **Manual smoke test:** with a working `Smtp` configuration (or a local SMTP catcher like MailHog pointed at by `appsettings.Development.json`), create an SLA-tracked, assigned ticket (Stories 06+17) whose `responseDueOn` is a minute in the future, wait past it and one scan interval, confirm an email arrives at the assigned agent's address and `GET /api/sla-breach-notifications?ticketId={id}` shows one `ResponseOverdue` row with `emailSent:true`.
5. **Regression:** re-run Story 19's `ScanSlaBreachesCommandHandlerTests` resolution-breach scenarios to confirm escalation behavior is unchanged by this story's edits, only augmented with notification creation.

## Done Criteria

- [ ] `SlaBreachNotification`/`SlaBreachType`, EF configuration, and migration exist and apply cleanly on top of Story 19's schema.
- [ ] `ScanSlaBreachesCommandHandler` raises exactly one `ResponseOverdue` notification per ticket (never duplicated across ticks) and one `ResolutionOverdue` notification per automatic escalation.
- [ ] Each new notification with an assigned agent triggers a best-effort email via the existing `IEmailSender`, recording `EmailSent` accurately; a failed email never loses the notification row or blocks the rest of the scan.
- [ ] `GET /api/sla-breach-notifications` and `GET /api/sla-breach-notifications/{id}` work, filterable by `ticketId`/`notifiedUserId`/`breachType`.
- [ ] All new and updated handler and validator unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

This completes KAN-5's four acceptance criteria across Stories 17-20: SLA targets (17), auto-assignment (18), escalation rules (19), and breach alerts/notifications (20, this story).
