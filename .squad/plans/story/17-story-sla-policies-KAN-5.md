# Story 17 — SLA Policies: Response & Resolution Time Targets (Story: KAN-5)

## Prerequisites

- [07-story-ticket-status-escalation-KAN-2.md](07-story-ticket-status-escalation-KAN-2.md) completed: requires `Ticket.Status`/`IsEscalated`/`EscalatedOn`, `ChangeTicketStatusCommandHandler`, and the `TicketHistory`/`TicketHistoryEventType.StatusChanged` pattern this story extends.
- [05-story-ticket-core-crud-KAN-2.md](05-story-ticket-core-crud-KAN-2.md) completed: requires `Ticket`/`TicketHistory`, `IApplicationDbContext.Tickets`, `TicketsController`, `TicketDto`/`TicketListItemDto`, and `TestApplicationDbContext`.
- [15-story-quick-reply-templates-KAN-4.md](15-story-quick-reply-templates-KAN-4.md) — read as the worked example for this story's team-shared CRUD shape (`QuickReplyTemplate`/`QuickReplyTemplatesController`): create/update/delete/get/list, no per-agent ownership, kebab-case route override.

## Story Goal

Let a support manager define, per ticket priority, how many minutes a ticket may wait for a first response and for full resolution, satisfying KAN-5's "Set response and resolution time targets" acceptance criterion. Every new ticket is automatically stamped with concrete due timestamps computed from the policy matching its priority, and the ticket records when it was first responded to — the foundation [18-story-auto-assignment-rules-KAN-5.md](18-story-auto-assignment-rules-KAN-5.md), [19-story-escalation-rules-KAN-5.md](19-story-escalation-rules-KAN-5.md), and [20-story-sla-breach-alerts-KAN-5.md](20-story-sla-breach-alerts-KAN-5.md) build on.

Outcomes:
1. `POST/PUT/DELETE /api/sla-policies` and `GET /api/sla-policies`, `GET /api/sla-policies/{id}` let a manager manage one active `SlaPolicy` per `TicketPriority`.
2. Creating a ticket resolves the active `SlaPolicy` matching its `Priority` and stamps `Ticket.ResponseDueOn`/`ResolutionDueOn` (`CreatedOn` + the policy's minutes). A ticket created with no matching active policy gets no due dates (SLA is opt-in per priority).
3. The first time a ticket's status changes away from `TicketStatus.New`, `Ticket.RespondedOn` is stamped — this is this codebase's only "first response" signal, since there's no separate reply-tracking mechanism.
4. `GET /api/tickets/{id}` and `GET /api/tickets` responses include `slaPolicyId`, `responseDueOn`, `resolutionDueOn`, `respondedOn`.

**Not in scope**: per-category SLA targets (targets are keyed by `TicketPriority` only, matching the acceptance criterion's "response and resolution time targets" with no mention of category); editing a ticket's `SlaPolicyId`/due dates directly via API (they're only ever set once, at creation); recomputing due dates when a ticket's `Priority` changes via `UpdateTicketCommand` (a priority change does not re-stamp SLA dates in this story — flagged as a follow-up in Edge Cases); breach detection and escalation (that's Story 19); notifications (that's Story 20).

## Context — Read These Files First

1. [15-story-quick-reply-templates-KAN-4.md](15-story-quick-reply-templates-KAN-4.md) — read in full. `SlaPoliciesController`/`SlaPolicy` CRUD copies this story's exact shape (team-shared, hard 404 on missing id, soft delete, kebab-case route override), substituting `QuickReplyTemplate`'s two `string` fields for `SlaPolicy`'s `TicketPriority`/two `int` fields.
2. [src/AzmCrm.Domain/Features/QuickReplies/QuickReplyTemplate.cs](../../../src/AzmCrm.Domain/Features/QuickReplies/QuickReplyTemplate.cs) and [src/AzmCrm.Infrastructure/Data/Configurations/QuickReplyTemplateConfiguration.cs](../../../src/AzmCrm.Infrastructure/Data/Configurations/QuickReplyTemplateConfiguration.cs) — the entity/configuration shape `SlaPolicy`/`SlaPolicyConfiguration` follow.
3. [src/AzmCrm.API/Controllers/QuickReplyTemplatesController.cs](../../../src/AzmCrm.API/Controllers/QuickReplyTemplatesController.cs) — the controller shape `SlaPoliciesController` follows, including the kebab-case `[Route("api/quick-reply-templates")]` override pattern (`api/sla-policies` needs the same override since `ApiControllerBase`'s `api/[controller]` would otherwise resolve to `api/SlaPolicies`).
4. [src/AzmCrm.Domain/Features/Tickets/Ticket.cs](../../../src/AzmCrm.Domain/Features/Tickets/Ticket.cs) (19 lines) — this story adds four properties after `EscalatedOn`: `SlaPolicyId`, `ResponseDueOn`, `ResolutionDueOn`, `RespondedOn`.
5. [src/AzmCrm.Domain/Features/Tickets/TicketPriority.cs](../../../src/AzmCrm.Domain/Features/Tickets/TicketPriority.cs) — read in full (4 values: `Low`, `Medium`, `High`, `Urgent`). `SlaPolicy.Priority` is one of these; `CreateSlaPolicyCommandValidator`'s `IsInEnum()` rule validates against exactly these four.
6. [src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs) (41 lines, read in full) — this story adds an `SlaPolicy` lookup by `Priority` between constructing the `Ticket` and calling `dbContext.Tickets.Add(ticket)`, setting the three new due-date fields before the ticket is added.
7. [src/AzmCrm.Application/Features/Tickets/Commands/ChangeTicketStatus/ChangeTicketStatusCommandHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Commands/ChangeTicketStatus/ChangeTicketStatusCommandHandler.cs) (37 lines, read in full) — this story adds the `RespondedOn` stamp inside the existing `if (ticket.Status != request.Status)` block.
8. [src/AzmCrm.Application/Features/Tickets/DTOs/TicketDto.cs](../../../src/AzmCrm.Application/Features/Tickets/DTOs/TicketDto.cs) and [TicketListItemDto.cs](../../../src/AzmCrm.Application/Features/Tickets/DTOs/TicketListItemDto.cs) — both already end with `bool IsEscalated, DateTime? EscalatedOn` (Story 07). This story appends four more trailing parameters after those.
9. [src/AzmCrm.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs) and [GetTicketsList/GetTicketsListQueryHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQueryHandler.cs) — append the four new `ticket.*`/`t.*` fields as trailing arguments to the existing `new TicketDto(...)`/`new TicketListItemDto(...)` calls. No new query filters are added by this story.
10. [src/AzmCrm.Infrastructure/Data/Configurations/TicketConfiguration.cs](../../../src/AzmCrm.Infrastructure/Data/Configurations/TicketConfiguration.cs) (39 lines, read in full) — this story adds an `SlaPolicy` FK (`HasOne<SlaPolicy>().WithMany().HasForeignKey(t => t.SlaPolicyId).OnDelete(DeleteBehavior.SetNull)`, following the exact same shape as the existing `HasOne<ApplicationUser>()` block for `AssignedToUserId`) plus one index.
11. [src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs), [src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs](../../../src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs), and [tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs](../../../tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs) — each needs one new `DbSet<SlaPolicy> SlaPolicies` line, following the existing `DbSet<QuickReplyTemplate>` line; `TestApplicationDbContext.OnModelCreating` needs one new `modelBuilder.Entity<SlaPolicy>().HasQueryFilter(p => !p.IsDeleted);` line.
12. [src/AzmCrm.Application/Localization/LocalizationKeys.cs](../../../src/AzmCrm.Application/Localization/LocalizationKeys.cs) lines 8-18 — reuses `Validation.Required`, `Validation.MaxLength`, `Validation.InvalidValue`, `Validation.MustBeGreaterThan`. No new keys or `Messages.*.json` edits needed.

## Implementation tasks

### 1 — Domain layer

**Create file: `src/AzmCrm.Domain/Features/Sla/SlaPolicy.cs`**

```csharp
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Domain.Features.Sla;

public sealed class SlaPolicy : BaseEntity
{
    public required string Name { get; set; }
    public required TicketPriority Priority { get; set; }
    public required int ResponseTimeMinutes { get; set; }
    public required int ResolutionTimeMinutes { get; set; }
    public bool IsActive { get; set; } = true;
}
```

**Edit file: `src/AzmCrm.Domain/Features/Tickets/Ticket.cs`** — add four properties after `EscalatedOn`:

```csharp
public Guid? SlaPolicyId { get; set; }
public DateTime? ResponseDueOn { get; set; }
public DateTime? ResolutionDueOn { get; set; }
public DateTime? RespondedOn { get; set; }
```

### 2 — Application layer

**Create file: `src/AzmCrm.Application/Features/Sla/DTOs/SlaPolicyDto.cs`**

```csharp
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Sla.DTOs;

public sealed record SlaPolicyDto(
    Guid Id, string Name, TicketPriority Priority,
    int ResponseTimeMinutes, int ResolutionTimeMinutes, bool IsActive,
    DateTime CreatedOn, DateTime? UpdatedOn);
```

**Create file: `src/AzmCrm.Application/Features/Sla/DTOs/SlaPolicyListItemDto.cs`**

```csharp
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Sla.DTOs;

public sealed record SlaPolicyListItemDto(
    Guid Id, string Name, TicketPriority Priority,
    int ResponseTimeMinutes, int ResolutionTimeMinutes, bool IsActive);
```

**Create file: `src/AzmCrm.Application/Features/Sla/DTOs/CreateSlaPolicyRequest.cs`**

```csharp
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Sla.DTOs;

public sealed record CreateSlaPolicyRequest(
    string Name, TicketPriority Priority, int ResponseTimeMinutes, int ResolutionTimeMinutes);
```

**Create file: `src/AzmCrm.Application/Features/Sla/DTOs/UpdateSlaPolicyRequest.cs`**

```csharp
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Sla.DTOs;

public sealed record UpdateSlaPolicyRequest(
    string Name, TicketPriority Priority, int ResponseTimeMinutes, int ResolutionTimeMinutes, bool IsActive);
```

**Create file: `src/AzmCrm.Application/Features/Sla/Commands/CreateSlaPolicy/CreateSlaPolicyCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Sla.Commands.CreateSlaPolicy;

public sealed record CreateSlaPolicyCommand(
    string Name, TicketPriority Priority, int ResponseTimeMinutes, int ResolutionTimeMinutes)
    : IRequest<Result<Guid>>;
```

**Create file: `src/AzmCrm.Application/Features/Sla/Commands/CreateSlaPolicy/CreateSlaPolicyCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Sla;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Sla.Commands.CreateSlaPolicy;

internal sealed class CreateSlaPolicyCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreateSlaPolicyCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateSlaPolicyCommand request, CancellationToken ct)
    {
        var alreadyExists = await dbContext.SlaPolicies
            .AnyAsync(p => p.Priority == request.Priority && p.IsActive, ct);
        if (alreadyExists)
            return Result<Guid>.Failure(
                $"An active SLA policy already exists for priority '{request.Priority}'.");

        var policy = new SlaPolicy
        {
            Name = request.Name,
            Priority = request.Priority,
            ResponseTimeMinutes = request.ResponseTimeMinutes,
            ResolutionTimeMinutes = request.ResolutionTimeMinutes
        };

        dbContext.SlaPolicies.Add(policy);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(policy.Id);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Sla/Commands/CreateSlaPolicy/CreateSlaPolicyCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Sla.Commands.CreateSlaPolicy;

public sealed class CreateSlaPolicyCommandValidator : AbstractValidator<CreateSlaPolicyCommand>
{
    public CreateSlaPolicyCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Name"])
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Name", 200]);

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage(localization[LocalizationKeys.Validation.InvalidValue, "Priority"]);

        RuleFor(x => x.ResponseTimeMinutes)
            .GreaterThan(0)
            .WithMessage(localization[LocalizationKeys.Validation.MustBeGreaterThan, "Response Time (minutes)", 0]);

        RuleFor(x => x.ResolutionTimeMinutes)
            .GreaterThan(x => x.ResponseTimeMinutes)
            .WithMessage("Resolution Time (minutes) must be greater than Response Time (minutes).");
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Sla/Commands/UpdateSlaPolicy/UpdateSlaPolicyCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Sla.Commands.UpdateSlaPolicy;

public sealed record UpdateSlaPolicyCommand(
    Guid Id, string Name, TicketPriority Priority,
    int ResponseTimeMinutes, int ResolutionTimeMinutes, bool IsActive) : IRequest<Result>;
```

**Create file: `src/AzmCrm.Application/Features/Sla/Commands/UpdateSlaPolicy/UpdateSlaPolicyCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Sla.Commands.UpdateSlaPolicy;

internal sealed class UpdateSlaPolicyCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UpdateSlaPolicyCommand, Result>
{
    public async Task<Result> Handle(UpdateSlaPolicyCommand request, CancellationToken ct)
    {
        var policy = await dbContext.SlaPolicies.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new NotFoundException($"SLA policy '{request.Id}' was not found.");

        if (request.IsActive)
        {
            var conflicts = await dbContext.SlaPolicies
                .AnyAsync(p => p.Id != request.Id && p.Priority == request.Priority && p.IsActive, ct);
            if (conflicts)
                return Result.Failure(
                    $"An active SLA policy already exists for priority '{request.Priority}'.");
        }

        policy.Name = request.Name;
        policy.Priority = request.Priority;
        policy.ResponseTimeMinutes = request.ResponseTimeMinutes;
        policy.ResolutionTimeMinutes = request.ResolutionTimeMinutes;
        policy.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Sla/Commands/UpdateSlaPolicy/UpdateSlaPolicyCommandValidator.cs`** — same rules as `CreateSlaPolicyCommandValidator` plus `RuleFor(x => x.Id).NotEmpty()...`, following [UpdateQuickReplyTemplateCommandValidator.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Commands/UpdateQuickReplyTemplate/UpdateQuickReplyTemplateCommandValidator.cs)'s shape exactly.

**Create file: `src/AzmCrm.Application/Features/Sla/Commands/DeleteSlaPolicy/DeleteSlaPolicyCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Sla.Commands.DeleteSlaPolicy;

public sealed record DeleteSlaPolicyCommand(Guid Id) : IRequest<Result>;
```

**Create file: `src/AzmCrm.Application/Features/Sla/Commands/DeleteSlaPolicy/DeleteSlaPolicyCommandHandler.cs`** — copy [DeleteQuickReplyTemplateCommandHandler.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Commands/DeleteQuickReplyTemplate/DeleteQuickReplyTemplateCommandHandler.cs) exactly, substituting `dbContext.SlaPolicies`/`SlaPolicy '{request.Id}'`.

**Create file: `src/AzmCrm.Application/Features/Sla/Commands/DeleteSlaPolicy/DeleteSlaPolicyCommandValidator.cs`** — copy [DeleteQuickReplyTemplateCommandValidator.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Commands/DeleteQuickReplyTemplate/DeleteQuickReplyTemplateCommandValidator.cs) exactly.

**Create file: `src/AzmCrm.Application/Features/Sla/Queries/GetSlaPolicyById/GetSlaPolicyByIdQuery.cs`**

```csharp
using AzmCrm.Application.Features.Sla.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Sla.Queries.GetSlaPolicyById;

public sealed record GetSlaPolicyByIdQuery(Guid Id) : IRequest<Result<SlaPolicyDto>>;
```

**Create file: `src/AzmCrm.Application/Features/Sla/Queries/GetSlaPolicyById/GetSlaPolicyByIdQueryHandler.cs`**

```csharp
using AzmCrm.Application.Features.Sla.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Sla.Queries.GetSlaPolicyById;

internal sealed class GetSlaPolicyByIdQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetSlaPolicyByIdQuery, Result<SlaPolicyDto>>
{
    public async Task<Result<SlaPolicyDto>> Handle(GetSlaPolicyByIdQuery request, CancellationToken ct)
    {
        var policy = await dbContext.SlaPolicies.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new NotFoundException($"SLA policy '{request.Id}' was not found.");

        var dto = new SlaPolicyDto(
            policy.Id, policy.Name, policy.Priority, policy.ResponseTimeMinutes,
            policy.ResolutionTimeMinutes, policy.IsActive, policy.CreatedOn, policy.UpdatedOn);

        return Result<SlaPolicyDto>.Success(dto);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Sla/Queries/GetSlaPoliciesList/GetSlaPoliciesListQuery.cs`**

```csharp
using AzmCrm.Application.Features.Sla.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Tickets;
using MediatR;

namespace AzmCrm.Application.Features.Sla.Queries.GetSlaPoliciesList;

public sealed record GetSlaPoliciesListQuery(
    int PageNumber = 1, int PageSize = 20, TicketPriority? Priority = null, bool? IsActive = null
) : IRequest<Result<PaginatedResult<SlaPolicyListItemDto>>>;
```

**Create file: `src/AzmCrm.Application/Features/Sla/Queries/GetSlaPoliciesList/GetSlaPoliciesListQueryHandler.cs`** — same shape as [GetQuickReplyTemplatesListQueryHandler.cs](../../../src/AzmCrm.Application/Features/QuickReplies/Queries/GetQuickReplyTemplatesList/GetQuickReplyTemplatesListQueryHandler.cs): `AsQueryable()` on `dbContext.SlaPolicies`, `if (request.Priority is not null) query = query.Where(p => p.Priority == request.Priority);`, `if (request.IsActive is not null) query = query.Where(p => p.IsActive == request.IsActive);`, order `.OrderBy(p => p.Priority)` (a small, fixed-size, priority-keyed list reads best sorted by its key, not by recency — same reasoning as the Story 15 precedent's alphabetical-by-`Title` sort), then project into `SlaPolicyListItemDto`.

**Create file: `src/AzmCrm.Application/Features/Sla/Queries/GetSlaPoliciesList/GetSlaPoliciesListQueryValidator.cs`** — copy [GetAgentTasksListQueryValidator.cs](../../../src/AzmCrm.Application/Features/AgentTasks/Queries/GetAgentTasksList/GetAgentTasksListQueryValidator.cs)'s `PageNumber`/`PageSize` rules exactly.

**Edit file: `src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs`** — insert an SLA policy lookup between constructing `ticket` and `dbContext.Tickets.Add(ticket)`:

```csharp
var slaPolicy = await dbContext.SlaPolicies
    .FirstOrDefaultAsync(p => p.Priority == request.Priority && p.IsActive, ct);

if (slaPolicy is not null)
{
    ticket.SlaPolicyId = slaPolicy.Id;
    ticket.ResponseDueOn = ticket.CreatedOn.AddMinutes(slaPolicy.ResponseTimeMinutes);
    ticket.ResolutionDueOn = ticket.CreatedOn.AddMinutes(slaPolicy.ResolutionTimeMinutes);
}
```

Note `ticket.CreatedOn` reads the in-memory default set by `BaseEntity.CreatedOn { get; set; } = DateTime.UtcNow;` (not yet overwritten by `ApplicationDbContext.SaveChangesAsync`'s `EntityState.Added` branch, which runs later and sets the *same* `utcNow` value it captures at that point) — the few milliseconds between object construction and `SaveChangesAsync` are immaterial to a minutes-granularity SLA target, so no explicit `DateTime.UtcNow` capture is needed here.

**Edit file: `src/AzmCrm.Application/Features/Tickets/Commands/ChangeTicketStatus/ChangeTicketStatusCommandHandler.cs`** — inside the existing `if (ticket.Status != request.Status)` block, before `ticket.Status = request.Status;`, add:

```csharp
if (ticket.RespondedOn is null && ticket.Status == TicketStatus.New)
    ticket.RespondedOn = DateTime.UtcNow;
```

**Edit file: `src/AzmCrm.Application/Features/Tickets/DTOs/TicketDto.cs`** — append four trailing parameters after `DateTime? EscalatedOn`:

```csharp
Guid? SlaPolicyId,
DateTime? ResponseDueOn,
DateTime? ResolutionDueOn,
DateTime? RespondedOn
```

**Edit file: `src/AzmCrm.Application/Features/Tickets/DTOs/TicketListItemDto.cs`** — append the same four trailing parameters.

**Edit file: `src/AzmCrm.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs`** — append `ticket.SlaPolicyId, ticket.ResponseDueOn, ticket.ResolutionDueOn, ticket.RespondedOn` as trailing arguments to the existing `new TicketDto(...)` construction.

**Edit file: `src/AzmCrm.Application/Features/Tickets/Queries/GetTicketsList/GetTicketsListQueryHandler.cs`** — append `t.SlaPolicyId, t.ResponseDueOn, t.ResolutionDueOn, t.RespondedOn` as trailing arguments to the existing `new TicketListItemDto(...)` construction. No new filter is added.

**Edit file: `src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs`** — add, after `DbSet<QuickReplyTemplate> QuickReplyTemplates { get; }`:

```csharp
DbSet<SlaPolicy> SlaPolicies { get; }
```

(add `using AzmCrm.Domain.Features.Sla;` to the file's usings)

### 3 — Infrastructure layer

**Create file: `src/AzmCrm.Infrastructure/Data/Configurations/SlaPolicyConfiguration.cs`**

```csharp
using AzmCrm.Domain.Features.Sla;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class SlaPolicyConfiguration : IEntityTypeConfiguration<SlaPolicy>
{
    public void Configure(EntityTypeBuilder<SlaPolicy> builder)
    {
        builder.ToTable("SlaPolicies");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Priority)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasQueryFilter(p => !p.IsDeleted);

        builder.HasIndex(p => p.Priority);
    }
}
```

**Edit file: `src/AzmCrm.Infrastructure/Data/Configurations/TicketConfiguration.cs`** — add, after the `IsEscalated` block:

```csharp
builder.HasOne<SlaPolicy>()
    .WithMany()
    .HasForeignKey(t => t.SlaPolicyId)
    .OnDelete(DeleteBehavior.SetNull);
```

and add `builder.HasIndex(t => t.SlaPolicyId);` after the existing `builder.HasIndex(t => t.IsEscalated);` line. Add `using AzmCrm.Domain.Features.Sla;` to the file's usings.

**Edit file: `src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs`** — add `using AzmCrm.Domain.Features.Sla;` and, after `public DbSet<QuickReplyTemplate> QuickReplyTemplates => Set<QuickReplyTemplate>();`:

```csharp
public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
```

**Generate migration:**

```bash
dotnet ef migrations add AddSlaPolicies --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API
```

### 4 — API layer

**Create file: `src/AzmCrm.API/Controllers/SlaPoliciesController.cs`** — copy [QuickReplyTemplatesController.cs](../../../src/AzmCrm.API/Controllers/QuickReplyTemplatesController.cs)'s exact shape: `[Route("api/sla-policies")]` (same kebab-case-override reasoning as that file's comment), `Create`/`GetById`/`GetList`/`Update`/`Delete` actions wired to the commands/query above. `GetList` takes `[FromQuery] TicketPriority? priority`, `[FromQuery] bool? isActive` instead of `search`.

### 5 — Test doubles

**Edit file: `tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs`** — add `using AzmCrm.Domain.Features.Sla;`, add `public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();` after the `QuickReplyTemplates` line, and add `modelBuilder.Entity<SlaPolicy>().HasQueryFilter(p => !p.IsDeleted);` after the `QuickReplyTemplate` query filter line.

## Edge Cases & Failure Modes

- **No active policy matches a ticket's priority** — `CreateTicketCommandHandler`'s `slaPolicy is not null` guard leaves `SlaPolicyId`/`ResponseDueOn`/`ResolutionDueOn` all `null`; the ticket has no SLA tracking (matches "SLA is opt-in per priority" from the Story Goal). Confirmed by [15-story-quick-reply-templates-KAN-4.md](15-story-quick-reply-templates-KAN-4.md)'s precedent of every KAN-5 story starting from an empty policy table.
- **Two active `SlaPolicy` rows for the same `Priority`** — prevented at the application level (not the database) by `CreateSlaPolicyCommandHandler`/`UpdateSlaPolicyCommandHandler`'s `AnyAsync` uniqueness check, which returns `Result.Failure` (not an exception) so it renders as a 400, matching this codebase's convention of `NotFoundException` for missing rows but `Result.Failure` for business-rule violations. Multiple **inactive** policies for the same priority are allowed (kept as history).
- **`ResolutionTimeMinutes` not greater than `ResponseTimeMinutes`** — rejected by `CreateSlaPolicyCommandValidator`/`UpdateSlaPolicyCommandValidator`'s cross-field `GreaterThan` rule before the command reaches the handler.
- **Changing a ticket's `Priority` via `UpdateTicketCommand` after creation** — does **not** recompute `SlaPolicyId`/`ResponseDueOn`/`ResolutionDueOn`; they remain pinned to whatever was active at creation time. This is a deliberate, documented gap (see Story Goal's "Not in scope") — flag as a follow-up if the business needs re-stamping on priority change.
- **A ticket's status is changed directly to a non-`New` value more than once** — `ticket.RespondedOn is null && ticket.Status == TicketStatus.New` only stamps `RespondedOn` on the *first* transition away from `New`; every subsequent `ChangeTicketStatusCommand` call leaves it untouched, even a transition back to `New` via a later status change (there's no "un-respond" — `RespondedOn` is a one-way timestamp, mirroring `EscalatedOn`'s one-way nature from Story 07, except `RespondedOn` itself is never re-stamped on repeat calls the way `EscalatedOn` deliberately is).
- **A ticket created directly with a non-`New` `Status`** — impossible in this codebase: `CreateTicketCommandHandler` always constructs `Ticket` without setting `Status`, so it always defaults to `TicketStatus.New` (`Ticket.cs` line 13); `RespondedOn` is therefore never pre-populated at creation.
- **Deleting (`DeleteSlaPolicyCommand`) a policy already referenced by existing tickets' `SlaPolicyId`** — allowed; `TicketConfiguration`'s `OnDelete(DeleteBehavior.SetNull)` only fires on a hard database delete, and `DeleteSlaPolicyCommandHandler` performs a **soft** delete (`IsDeleted = true`), so `Ticket.SlaPolicyId` values referencing a soft-deleted policy remain unchanged and still resolve via a direct id lookup — only `GetSlaPoliciesList`'s query filter hides the deleted policy from listings.

## Test Plan

1. **Create file: `tests/AzmCrm.Application.Tests/Features/Sla/CreateSlaPolicyCommandHandlerTests.cs`** — `Create_persists_policy_and_returns_id`; `Create_with_active_priority_conflict_returns_failure`; `Create_with_inactive_priority_conflict_succeeds`.
2. **Create file: `tests/AzmCrm.Application.Tests/Features/Sla/UpdateSlaPolicyCommandHandlerTests.cs`** — `Update_persists_changes`; `Update_missing_policy_throws_NotFoundException`; `Update_to_active_with_priority_conflict_returns_failure`.
3. **Create file: `tests/AzmCrm.Application.Tests/Features/Sla/DeleteSlaPolicyCommandHandlerTests.cs`** — `Delete_soft_deletes_policy`; `Delete_missing_policy_throws_NotFoundException`.
4. **Create file: `tests/AzmCrm.Application.Tests/Features/Sla/GetSlaPoliciesListQueryHandlerTests.cs`** — `List_returns_all_policies_ordered_by_priority`; `List_filters_by_priority`; `List_filters_by_isActive`.
5. **Create file: `tests/AzmCrm.Application.Tests/Features/Sla/CreateSlaPolicyCommandValidatorTests.cs`** — `ResolutionTimeMinutes_not_greater_than_ResponseTimeMinutes_fails`; `Undefined_Priority_fails`; `Valid_command_passes`.
6. **Edit `tests/AzmCrm.Application.Tests/Features/Tickets/CreateTicketCommandHandlerTests.cs`** (Story 05) — add `Create_with_matching_active_SlaPolicy_stamps_due_dates` (seed an `SlaPolicy` for `TicketPriority.High`, create a ticket with that priority, assert `SlaPolicyId`/`ResponseDueOn`/`ResolutionDueOn` are set to `CreatedOn + minutes`); add `Create_with_no_matching_SlaPolicy_leaves_due_dates_null`.
7. **Edit `tests/AzmCrm.Application.Tests/Features/Tickets/ChangeTicketStatusCommandHandlerTests.cs`** (Story 07) — add `Change_from_New_stamps_RespondedOn_once`; add `Change_status_twice_away_from_New_does_not_overwrite_RespondedOn` (seed a ticket already at `InProgress` with `RespondedOn` pre-set, call `ChangeTicketStatusCommand` to `OnHold`, assert `RespondedOn` is unchanged).
8. All new tests use `TestApplicationDbContext.Create()` and `StubLocalizationService` exactly as established in prior KAN-5 dependency stories — no new test doubles are needed.

## Migration / Rollback

- The migration generated in Task 3 **adds** the `SlaPolicies` table and four new nullable/FK columns on `Tickets` (`SlaPolicyId`, `ResponseDueOn`, `ResolutionDueOn`, `RespondedOn`) plus two indexes — additive, safe on top of the latest existing migration (`AddTicketComments`).
- **Rollback**: `dotnet ef database update AddTicketComments --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` drops the `SlaPolicies` table, the FK, and all four `Tickets` columns.
- **Half-applied state**: same existing behavior — `DatabaseInitializer` logs and rethrows on migration failure, so the app fails to start rather than running against a partial schema.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Migration applies cleanly:** `dotnet ef database update --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` (or let the API apply it automatically on startup).
4. **Manual smoke test:** `POST /api/sla-policies` with `{"name":"High Priority","priority":"High","responseTimeMinutes":30,"resolutionTimeMinutes":240}`, confirm 201; create a ticket (Story 05) with `"priority":"High"`, confirm `GET /api/tickets/{id}` shows a non-null `slaPolicyId`/`responseDueOn`/`resolutionDueOn` roughly 30/240 minutes after `createdOn`; `PUT /api/tickets/{id}/status` to `InProgress`, confirm `respondedOn` is now populated; create a second ticket with `"priority":"Low"` (no policy exists), confirm its SLA fields are all `null`.

## Done Criteria

- [ ] `SlaPolicy` entity, EF configuration, and migration exist and apply cleanly on top of `AddTicketComments`.
- [ ] `POST/PUT/DELETE /api/sla-policies` and `GET /api/sla-policies`, `GET /api/sla-policies/{id}` work, enforcing at most one active policy per `TicketPriority`.
- [ ] Creating a ticket with a priority matching an active policy stamps `SlaPolicyId`/`ResponseDueOn`/`ResolutionDueOn`; no match leaves them `null`.
- [ ] The first status change away from `New` stamps `Ticket.RespondedOn`; later status changes never overwrite it.
- [ ] `GET /api/tickets/{id}` and `GET /api/tickets` responses include `slaPolicyId`/`responseDueOn`/`resolutionDueOn`/`respondedOn`.
- [ ] All new and updated handler and validator unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

This story satisfies KAN-5's "Set response and resolution time targets" acceptance criterion and lays the `ResponseDueOn`/`ResolutionDueOn`/`RespondedOn` groundwork Stories 19-20 read to detect and act on SLA breaches.
