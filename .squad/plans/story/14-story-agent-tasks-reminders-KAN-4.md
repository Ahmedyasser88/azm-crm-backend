# Story 14 — Agent Tasks & Reminders (Story: KAN-4)

## Prerequisites

- [13-story-dashboard-core-tickets-customers-KAN-4.md](13-story-dashboard-core-tickets-customers-KAN-4.md): not a hard dependency (this story adds its own entity, controller, and folder, editing none of Story 13's files), but read its Context items 8-9 for the `ICurrentUserService`/`?? Guid.Empty` convention this story reuses.
- [01-story-customer-core-crud-KAN-1.md](01-story-customer-core-crud-KAN-1.md) completed: an `AgentTask` may optionally reference an existing `Customer` via `CustomerId`; this story validates that id against `dbContext.Customers` when provided.
- [05-story-ticket-core-crud-KAN-2.md](05-story-ticket-core-crud-KAN-2.md) completed: an `AgentTask` may optionally reference an existing `Ticket` via `TicketId`, validated the same way.

## Story Goal

Let a support agent create, view, edit, complete, and delete personal to-do items and reminders — optionally linked to the customer or ticket they relate to — satisfying KAN-4's **"Manage tasks and reminders"** acceptance criterion.

Outcomes:
1. `POST /api/agent-tasks` creates a task always owned by the caller (`AssignedToUserId` is never taken from the request body — it's always the authenticated user, resolved via `ICurrentUserService`), with a required `Title`, optional `Description`, optional `DueOn` (the reminder time), and optional `CustomerId`/`TicketId` links.
2. `GET /api/agent-tasks/{id}` and `GET /api/agent-tasks` return only tasks owned by the caller — this is a personal to-do list, not a shared queue.
3. `GET /api/agent-tasks` sorts incomplete tasks first, soonest-due first, so the dashboard can render it directly as a reminders panel without client-side re-sorting; supports an `isCompleted` filter.
4. `PUT /api/agent-tasks/{id}` edits `Title`/`Description`/`DueOn`.
5. `PUT /api/agent-tasks/{id}/completion` marks a task complete or incomplete, stamping/clearing `CompletedOn` accordingly.
6. `DELETE /api/agent-tasks/{id}` soft-deletes a task, mirroring the existing `Customer` soft-delete convention.

**Not in scope**: assigning a task to a different agent than its creator (no "assign task to teammate" workflow — that would overlap with KAN-4's separate "Collaborate with team members" criterion, covered instead by [16-story-ticket-collaboration-comments-KAN-4.md](16-story-ticket-collaboration-comments-KAN-4.md)'s ticket comments), recurring reminders, push/email notifications when a `DueOn` passes, and a dedicated "overdue tasks" endpoint (the frontend can derive "overdue" client-side from `DueOn < now && !IsCompleted` on the existing list response).

## Context — Read These Files First

1. [src/AzmCrm.Domain/Common/BaseEntity.cs](../../../src/AzmCrm.Domain/Common/BaseEntity.cs) — read in full (19 lines). `AgentTask` extends this: `Id` (client-assigned `Guid.CreateVersion7()`), `CreatedBy`/`CreatedOn` (auto-stamped), `IsDeleted`/`DeletedBy`/`DeletedOn` (used by `DeleteAgentTaskCommandHandler`, Task 4).
2. [src/AzmCrm.Domain/Features/Customers/Customer.cs](../../../src/AzmCrm.Domain/Features/Customers/Customer.cs) and [src/AzmCrm.Domain/Features/Tickets/Ticket.cs](../../../src/AzmCrm.Domain/Features/Tickets/Ticket.cs) — read both in full. `AgentTask.CustomerId`/`TicketId` reference these; both FKs are optional (`Guid?`).
3. [src/AzmCrm.Infrastructure/Data/Configurations/TicketConfiguration.cs](../../../src/AzmCrm.Infrastructure/Data/Configurations/TicketConfiguration.cs) — lines 46-49: `builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);`. Exact precedent for an **optional** FK with no navigation property (`HasOne<T>()` with a type argument, not `HasOne(x => x.Nav)`) and `DeleteBehavior.SetNull` — `AgentTaskConfiguration` (Task 3) uses this same shape twice, for `CustomerId` and `TicketId`, since `AgentTask` needs no `Customer`/`Ticket` navigation property (the frontend already has dedicated endpoints to fetch either by id).
4. [src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandHandler.cs](../../../src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandHandler.cs), [UpdateCustomer/UpdateCustomerCommandHandler.cs](../../../src/AzmCrm.Application/Features/Customers/Commands/UpdateCustomer/UpdateCustomerCommandHandler.cs), and [DeleteCustomer/DeleteCustomerCommandHandler.cs](../../../src/AzmCrm.Application/Features/Customers/Commands/DeleteCustomer/DeleteCustomerCommandHandler.cs) — read all three in full. The exact "construct-and-save" / "load-or-404, mutate, save" / "load-or-404, soft-delete, save" handler shapes `CreateAgentTaskCommandHandler`/`UpdateAgentTaskCommandHandler`/`DeleteAgentTaskCommandHandler` follow, with one addition: every load in this story filters by **both** `Id` and `AssignedToUserId == currentUserService.UserId` (see Edge Cases for why a mismatch reports 404, not 403).
5. [src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs](../../../src/AzmCrm.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs) — lines 15-17 (the `customerExists` `AnyAsync` guard + `NotFoundException`). `CreateAgentTaskCommandHandler` repeats this pattern twice — once for `CustomerId`, once for `TicketId` — each only when that optional field is non-null.
6. [src/AzmCrm.Application/Features/Customers/Commands/DeleteCustomer/DeleteCustomerCommandHandler.cs](../../../src/AzmCrm.Application/Features/Customers/Commands/DeleteCustomer/DeleteCustomerCommandHandler.cs) — lines 19-21 (`IsDeleted = true; DeletedBy = currentUserService.UserId ?? Guid.Empty; DeletedOn = DateTime.UtcNow;`). Exact soft-delete pattern `DeleteAgentTaskCommandHandler` copies.
7. [src/AzmCrm.Application/Shared/Interfaces/ICurrentUserService.cs](../../../src/AzmCrm.Application/Shared/Interfaces/ICurrentUserService.cs) — read in full (13 lines). Already registered (`src/AzmCrm.Infrastructure/DependencyInjection.cs:104`) — no DI change needed.
8. [src/AzmCrm.Application/Features/Customers/Queries/GetCustomersList/GetCustomersListQueryHandler.cs](../../../src/AzmCrm.Application/Features/Customers/Queries/GetCustomersList/GetCustomersListQueryHandler.cs) — read in full (47 lines). `GetAgentTasksListQueryHandler`'s filter→count→page→project shape mirrors this exactly, except the base filter is `AssignedToUserId == currentUserService.UserId` (always applied, not optional) instead of a search term, and the ordering differs — see Task 2 for the deliberate `IsCompleted` → `DueOn` → `CreatedOn` compound sort.
9. [src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandValidator.cs](../../../src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandValidator.cs) — read in full (38 lines). Exact `RuleFor(...).NotEmpty()...MaximumLength(...)` shape this story's validators reuse.
10. [src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs) — read in full (27 lines, current end-state after KAN-3). Add `using AzmCrm.Domain.Features.AgentTasks;` and, after the existing `Message` member: `DbSet<AgentTask> AgentTasks { get; }`.
11. [src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs](../../../src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs) — read in full (66 lines). Lines 25-33 are the `DbSet<T>` properties to extend; line 35's comment marks where. `SaveChangesAsync` (lines 44-65) auto-stamps `CreatedBy`/`CreatedOn`/`UpdatedBy`/`UpdatedOn` for every tracked `BaseEntity` — covers `AgentTask` automatically, no extra code needed for those fields.
12. [src/AzmCrm.Application/Shared/Exceptions/NotFoundException.cs](../../../src/AzmCrm.Application/Shared/Exceptions/NotFoundException.cs) (3 lines) and [src/AzmCrm.API/Middleware/ExceptionHandlingMiddleware.cs](../../../src/AzmCrm.API/Middleware/ExceptionHandlingMiddleware.cs) lines 26-43 (`NotFoundException` → HTTP 404 at lines 33-37).
13. [src/AzmCrm.API/Controllers/CustomersController.cs](../../../src/AzmCrm.API/Controllers/CustomersController.cs) — lines 22-80 (`Create`/`GetById`/`GetList`/`Update`/`Delete`). Exact controller-action shape `AgentTasksController` mirrors for five of its six actions; the sixth (`completion`) mirrors `TicketsController`'s `Assign`/`ChangeStatus` PUT-with-body-DTO shape ([src/AzmCrm.API/Controllers/TicketsController.cs:72-90](../../../src/AzmCrm.API/Controllers/TicketsController.cs)).
14. [src/AzmCrm.Application/Localization/LocalizationKeys.cs](../../../src/AzmCrm.Application/Localization/LocalizationKeys.cs) — read in full (46 lines). This story reuses `Validation.Required` and `Validation.MaxLength` only — no new keys or `Messages.*.json` edits are needed.
15. [src/AzmCrm.Infrastructure/Data/Migrations/20260828165442_AddCommunications.cs](../../../src/AzmCrm.Infrastructure/Data/Migrations/20260828165442_AddCommunications.cs) — the most recent migration; the new migration for this story adds the `AgentTasks` table on top of this baseline.
16. [tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs](../../../tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs) — read in full (44 lines). Add `AgentTask` `DbSet<T>` property and its query filter, following the exact pattern already used for every other aggregate.
17. [tests/AzmCrm.Application.Tests/Features/Customers/DeleteCustomerCommandHandlerTests.cs](../../../tests/AzmCrm.Application.Tests/Features/Customers/DeleteCustomerCommandHandlerTests.cs) — read in full (63 lines). Precedent for asserting soft-delete via `IgnoreQueryFilters()`.

## Implementation tasks

### 1 — Domain layer

**Create file: `src/AzmCrm.Domain/Features/AgentTasks/AgentTask.cs`**

```csharp
using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.AgentTasks;

public sealed class AgentTask : BaseEntity
{
    public required Guid AssignedToUserId { get; init; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTime? DueOn { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedOn { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? TicketId { get; set; }
}
```

No navigation properties to `ApplicationUser`, `Customer`, or `Ticket` — Application-layer code always goes through `IApplicationDbContext.Customers`/`Tickets` directly (same `DbContext`) or `ICurrentUserService` for the owner, matching the `Ticket.AssignedToUserId` precedent (Context item 3).

### 2 — Application layer

**Create file: `src/AzmCrm.Application/Features/AgentTasks/DTOs/AgentTaskDto.cs`**

```csharp
namespace AzmCrm.Application.Features.AgentTasks.DTOs;

public sealed record AgentTaskDto(
    Guid Id,
    string Title,
    string? Description,
    DateTime? DueOn,
    bool IsCompleted,
    DateTime? CompletedOn,
    Guid? CustomerId,
    Guid? TicketId,
    DateTime CreatedOn,
    DateTime? UpdatedOn
);
```

**Create file: `src/AzmCrm.Application/Features/AgentTasks/DTOs/CreateAgentTaskRequest.cs`**

```csharp
namespace AzmCrm.Application.Features.AgentTasks.DTOs;

public sealed record CreateAgentTaskRequest(
    string Title, string? Description, DateTime? DueOn, Guid? CustomerId, Guid? TicketId);
```

**Create file: `src/AzmCrm.Application/Features/AgentTasks/DTOs/UpdateAgentTaskRequest.cs`**

```csharp
namespace AzmCrm.Application.Features.AgentTasks.DTOs;

public sealed record UpdateAgentTaskRequest(string Title, string? Description, DateTime? DueOn);
```

**Create file: `src/AzmCrm.Application/Features/AgentTasks/DTOs/SetAgentTaskCompletionRequest.cs`**

```csharp
namespace AzmCrm.Application.Features.AgentTasks.DTOs;

public sealed record SetAgentTaskCompletionRequest(bool IsCompleted);
```

**Create file: `src/AzmCrm.Application/Features/AgentTasks/Commands/CreateAgentTask/CreateAgentTaskCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.AgentTasks.Commands.CreateAgentTask;

public sealed record CreateAgentTaskCommand(
    string Title, string? Description, DateTime? DueOn, Guid? CustomerId, Guid? TicketId
) : IRequest<Result<Guid>>;
```

**Create file: `src/AzmCrm.Application/Features/AgentTasks/Commands/CreateAgentTask/CreateAgentTaskCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.AgentTasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.AgentTasks.Commands.CreateAgentTask;

internal sealed class CreateAgentTaskCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<CreateAgentTaskCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateAgentTaskCommand request, CancellationToken ct)
    {
        if (request.CustomerId is not null &&
            !await dbContext.Customers.AnyAsync(c => c.Id == request.CustomerId, ct))
            throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        if (request.TicketId is not null &&
            !await dbContext.Tickets.AnyAsync(t => t.Id == request.TicketId, ct))
            throw new NotFoundException($"Ticket '{request.TicketId}' was not found.");

        var task = new AgentTask
        {
            AssignedToUserId = currentUserService.UserId ?? Guid.Empty,
            Title = request.Title,
            Description = request.Description,
            DueOn = request.DueOn,
            CustomerId = request.CustomerId,
            TicketId = request.TicketId
        };

        dbContext.AgentTasks.Add(task);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(task.Id);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/AgentTasks/Commands/CreateAgentTask/CreateAgentTaskCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.AgentTasks.Commands.CreateAgentTask;

public sealed class CreateAgentTaskCommandValidator : AbstractValidator<CreateAgentTaskCommand>
{
    public CreateAgentTaskCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Title"])
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Title", 200]);

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Description", 2000]);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/AgentTasks/Commands/UpdateAgentTask/UpdateAgentTaskCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.AgentTasks.Commands.UpdateAgentTask;

public sealed record UpdateAgentTaskCommand(
    Guid Id, string Title, string? Description, DateTime? DueOn
) : IRequest<Result>;
```

**Create file: `src/AzmCrm.Application/Features/AgentTasks/Commands/UpdateAgentTask/UpdateAgentTaskCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.AgentTasks.Commands.UpdateAgentTask;

internal sealed class UpdateAgentTaskCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<UpdateAgentTaskCommand, Result>
{
    public async Task<Result> Handle(UpdateAgentTaskCommand request, CancellationToken ct)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;

        var task = await dbContext.AgentTasks
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.AssignedToUserId == userId, ct)
            ?? throw new NotFoundException($"Task '{request.Id}' was not found.");

        task.Title = request.Title;
        task.Description = request.Description;
        task.DueOn = request.DueOn;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

**Create file: `src/AzmCrm.Application/Features/AgentTasks/Commands/UpdateAgentTask/UpdateAgentTaskCommandValidator.cs`** — same rules as `CreateAgentTaskCommandValidator` plus `RuleFor(x => x.Id).NotEmpty()...`.

**Create file: `src/AzmCrm.Application/Features/AgentTasks/Commands/SetAgentTaskCompletion/SetAgentTaskCompletionCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.AgentTasks.Commands.SetAgentTaskCompletion;

public sealed record SetAgentTaskCompletionCommand(Guid Id, bool IsCompleted) : IRequest<Result>;
```

**Create file: `src/AzmCrm.Application/Features/AgentTasks/Commands/SetAgentTaskCompletion/SetAgentTaskCompletionCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.AgentTasks.Commands.SetAgentTaskCompletion;

internal sealed class SetAgentTaskCompletionCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<SetAgentTaskCompletionCommand, Result>
{
    public async Task<Result> Handle(SetAgentTaskCompletionCommand request, CancellationToken ct)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;

        var task = await dbContext.AgentTasks
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.AssignedToUserId == userId, ct)
            ?? throw new NotFoundException($"Task '{request.Id}' was not found.");

        task.IsCompleted = request.IsCompleted;
        task.CompletedOn = request.IsCompleted ? DateTime.UtcNow : null;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

**Create file: `src/AzmCrm.Application/Features/AgentTasks/Commands/SetAgentTaskCompletion/SetAgentTaskCompletionCommandValidator.cs`** — `RuleFor(x => x.Id).NotEmpty()...` only; `IsCompleted` is a plain `bool`, no rule needed.

**Create file: `src/AzmCrm.Application/Features/AgentTasks/Commands/DeleteAgentTask/DeleteAgentTaskCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.AgentTasks.Commands.DeleteAgentTask;

public sealed record DeleteAgentTaskCommand(Guid Id) : IRequest<Result>;
```

**Create file: `src/AzmCrm.Application/Features/AgentTasks/Commands/DeleteAgentTask/DeleteAgentTaskCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.AgentTasks.Commands.DeleteAgentTask;

internal sealed class DeleteAgentTaskCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<DeleteAgentTaskCommand, Result>
{
    public async Task<Result> Handle(DeleteAgentTaskCommand request, CancellationToken ct)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;

        var task = await dbContext.AgentTasks
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.AssignedToUserId == userId, ct)
            ?? throw new NotFoundException($"Task '{request.Id}' was not found.");

        task.IsDeleted = true;
        task.DeletedBy = userId;
        task.DeletedOn = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

**Create file: `src/AzmCrm.Application/Features/AgentTasks/Commands/DeleteAgentTask/DeleteAgentTaskCommandValidator.cs`** — `RuleFor(x => x.Id).NotEmpty()...` only.

**Create file: `src/AzmCrm.Application/Features/AgentTasks/Queries/GetAgentTaskById/GetAgentTaskByIdQuery.cs`**

```csharp
using AzmCrm.Application.Features.AgentTasks.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.AgentTasks.Queries.GetAgentTaskById;

public sealed record GetAgentTaskByIdQuery(Guid Id) : IRequest<Result<AgentTaskDto>>;
```

**Create file: `src/AzmCrm.Application/Features/AgentTasks/Queries/GetAgentTaskById/GetAgentTaskByIdQueryHandler.cs`**

```csharp
using AzmCrm.Application.Features.AgentTasks.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.AgentTasks.Queries.GetAgentTaskById;

internal sealed class GetAgentTaskByIdQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetAgentTaskByIdQuery, Result<AgentTaskDto>>
{
    public async Task<Result<AgentTaskDto>> Handle(GetAgentTaskByIdQuery request, CancellationToken ct)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;

        var task = await dbContext.AgentTasks
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.AssignedToUserId == userId, ct)
            ?? throw new NotFoundException($"Task '{request.Id}' was not found.");

        var dto = new AgentTaskDto(
            task.Id, task.Title, task.Description, task.DueOn, task.IsCompleted, task.CompletedOn,
            task.CustomerId, task.TicketId, task.CreatedOn, task.UpdatedOn);

        return Result<AgentTaskDto>.Success(dto);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/AgentTasks/Queries/GetAgentTasksList/GetAgentTasksListQuery.cs`**

```csharp
using AzmCrm.Application.Features.AgentTasks.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.AgentTasks.Queries.GetAgentTasksList;

public sealed record GetAgentTasksListQuery(
    int PageNumber = 1,
    int PageSize = 20,
    bool? IsCompleted = null
) : IRequest<Result<PaginatedResult<AgentTaskDto>>>;
```

**Create file: `src/AzmCrm.Application/Features/AgentTasks/Queries/GetAgentTasksList/GetAgentTasksListQueryHandler.cs`**

```csharp
using AzmCrm.Application.Features.AgentTasks.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.AgentTasks.Queries.GetAgentTasksList;

internal sealed class GetAgentTasksListQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetAgentTasksListQuery, Result<PaginatedResult<AgentTaskDto>>>
{
    public async Task<Result<PaginatedResult<AgentTaskDto>>> Handle(
        GetAgentTasksListQuery request, CancellationToken ct)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;

        var query = dbContext.AgentTasks.Where(t => t.AssignedToUserId == userId);

        if (request.IsCompleted is not null)
            query = query.Where(t => t.IsCompleted == request.IsCompleted);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            // Incomplete tasks first, soonest due first, so this list can be rendered directly
            // as a reminders panel — a deliberate deviation from the CreatedOn-desc convention
            // used by every other list query in this codebase (see Edge Cases).
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => t.DueOn ?? DateTime.MaxValue)
            .ThenByDescending(t => t.CreatedOn)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new AgentTaskDto(
                t.Id, t.Title, t.Description, t.DueOn, t.IsCompleted, t.CompletedOn,
                t.CustomerId, t.TicketId, t.CreatedOn, t.UpdatedOn))
            .ToListAsync(ct);

        var result = new PaginatedResult<AgentTaskDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<AgentTaskDto>>.Success(result);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/AgentTasks/Queries/GetAgentTasksList/GetAgentTasksListQueryValidator.cs`** — same paging-range rules as `GetTicketsListQueryValidator` (`PageNumber >= 1`, `PageSize` between 1 and 100).

**Edit file: `src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs`** — add `using AzmCrm.Domain.Features.AgentTasks;` and, after `DbSet<Message> Messages { get; }`:

```csharp
DbSet<AgentTask> AgentTasks { get; }
```

### 3 — Infrastructure layer

**Create file: `src/AzmCrm.Infrastructure/Data/Configurations/AgentTaskConfiguration.cs`**

```csharp
using AzmCrm.Domain.Features.AgentTasks;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Identity;
using AzmCrm.Domain.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class AgentTaskConfiguration : IEntityTypeConfiguration<AgentTask>
{
    public void Configure(EntityTypeBuilder<AgentTask> builder)
    {
        builder.ToTable("AgentTasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedNever();

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(2000);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.AssignedToUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(t => t.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(t => t.TicketId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(t => !t.IsDeleted);

        builder.HasIndex(t => t.AssignedToUserId);
        builder.HasIndex(t => t.CustomerId);
        builder.HasIndex(t => t.TicketId);
        builder.HasIndex(t => t.DueOn);
    }
}
```

`AssignedToUserId` uses `DeleteBehavior.Cascade` (not `SetNull`, since it is a **required**, non-nullable `Guid` — an `AgentTask` cannot exist without an owner) — unlike `Ticket.AssignedToUserId`, which is optional. `CustomerId`/`TicketId` use `SetNull` since both are optional links, mirroring `TicketConfiguration`'s `AssignedToUserId` FK exactly (Context item 3).

**Edit file: `src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs`** — add `using AzmCrm.Domain.Features.AgentTasks;` and, replacing the placeholder comment:

```csharp
public DbSet<AgentTask> AgentTasks => Set<AgentTask>();
```

**Generate migration:**

```bash
dotnet ef migrations add AddAgentTasks --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API
```

### 4 — API layer

**Create file: `src/AzmCrm.API/Controllers/AgentTasksController.cs`**

```csharp
using AzmCrm.API.Controllers.Base;
using AzmCrm.Application.Features.AgentTasks.Commands.CreateAgentTask;
using AzmCrm.Application.Features.AgentTasks.Commands.DeleteAgentTask;
using AzmCrm.Application.Features.AgentTasks.Commands.SetAgentTaskCompletion;
using AzmCrm.Application.Features.AgentTasks.Commands.UpdateAgentTask;
using AzmCrm.Application.Features.AgentTasks.DTOs;
using AzmCrm.Application.Features.AgentTasks.Queries.GetAgentTaskById;
using AzmCrm.Application.Features.AgentTasks.Queries.GetAgentTasksList;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AzmCrm.API.Controllers;

public sealed class AgentTasksController(IMediator mediator) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateAgentTaskRequest request, CancellationToken ct)
    {
        var command = new CreateAgentTaskCommand(
            request.Title, request.Description, request.DueOn, request.CustomerId, request.TicketId);

        var result = await mediator.Send(command, ct);

        return ToCreatedResult(result, id => $"/api/agent-tasks/{id}");
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Result<AgentTaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetAgentTaskByIdQuery(id), ct);
        return ToResult(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(Result<PaginatedResult<AgentTaskDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] bool? isCompleted = null, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetAgentTasksListQuery(pageNumber, pageSize, isCompleted), ct);
        return ToResult(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAgentTaskRequest request, CancellationToken ct)
    {
        var command = new UpdateAgentTaskCommand(id, request.Title, request.Description, request.DueOn);

        var result = await mediator.Send(command, ct);
        return ToResult(result);
    }

    [HttpPut("{id:guid}/completion")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetCompletion(
        Guid id, [FromBody] SetAgentTaskCompletionRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new SetAgentTaskCompletionCommand(id, request.IsCompleted), ct);
        return ToResult(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteAgentTaskCommand(id), ct);
        return ToNoContentResult(result);
    }
}
```

## Edge Cases & Failure Modes

- **`CustomerId`/`TicketId` on create does not resolve to an existing, non-deleted row** — `CreateAgentTaskCommandHandler` checks each with `AnyAsync` (the query filters on `Customer`/`Ticket` already exclude soft-deleted rows) and throws `NotFoundException` → 404, identical to KAN-2 Story 05's `CustomerId` guard on ticket create.
- **Agent A requests a task owned by Agent B** (`GetAgentTaskByIdQuery`, `UpdateAgentTaskCommand`, `SetAgentTaskCompletionCommand`, `DeleteAgentTaskCommand`) — every handler's lookup filters on **both** `Id` and `AssignedToUserId == currentUserService.UserId`, so a task that exists but belongs to someone else produces the same `NotFoundException` → 404 as a task that doesn't exist at all. This is a deliberate choice to avoid leaking "this id exists but isn't yours" (a 403 would confirm existence); no other endpoint in this codebase currently needs this ownership-scoped-404 pattern, so this story introduces it — document it clearly for API consumers.
- **Marking an already-completed task complete again** (`IsCompleted: true` sent twice) — `SetAgentTaskCompletionCommandHandler` unconditionally re-sets `CompletedOn = DateTime.UtcNow`, so `CompletedOn` advances to "now" on every repeated call rather than staying at the original completion time. Flag as a follow-up if "first completed" timestamp semantics are ever required — this story's acceptance criteria doesn't call for it.
- **Un-completing a task** (`IsCompleted: false`) — `CompletedOn` is cleared back to `null`, so a task's completion history (if it was ever completed before) is not preserved; only the current state matters.
- **`DueOn` in the past at creation time** — not rejected; a reminder due in the past is valid (e.g. logging a task retroactively) and simply sorts to the top of `GetAgentTasksListQueryHandler`'s incomplete-first, soonest-due-first ordering.
- **A task has no `DueOn`** (`null`) — `GetAgentTasksListQueryHandler`'s `.ThenBy(t => t.DueOn ?? DateTime.MaxValue)` sorts undated tasks to the end of their `IsCompleted` group, not the beginning — a task with no reminder time is lower priority than one with any concrete due date.
- **`PageNumber`/`PageSize` out of range** — enforced by `GetAgentTasksListQueryValidator` via the existing `ValidationBehavior` pipeline, turned into a 400 before the handler runs.
- **A hard-deleted `ApplicationUser`** (never performed by this codebase's own endpoints) — `AgentTaskConfiguration`'s `AssignedToUserId` FK uses `DeleteBehavior.Cascade`, so all of that user's tasks are deleted along with the account, unlike `Ticket.AssignedToUserId`'s `SetNull` — this is intentional: a personal to-do list has no meaning once its owner is gone, whereas a ticket must survive its assignee's removal.

## Test Plan

All new tests live in `tests/AzmCrm.Application.Tests/`, following the existing `TestApplicationDbContext`/`StubCurrentUserService`/`StubLocalizationService` infrastructure.

1. **Edit `tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs`** — add `public DbSet<AgentTask> AgentTasks => Set<AgentTask>();` and `modelBuilder.Entity<AgentTask>().HasQueryFilter(t => !t.IsDeleted);` to `OnModelCreating`.
2. **Create file: `tests/AzmCrm.Application.Tests/Features/AgentTasks/CreateAgentTaskCommandHandlerTests.cs`** — `Create_persists_task_owned_by_current_user`; `Create_with_unknown_customerId_throws_NotFoundException`; `Create_with_unknown_ticketId_throws_NotFoundException`; `Create_without_optional_links_succeeds`.
3. **Create file: `tests/AzmCrm.Application.Tests/Features/AgentTasks/UpdateAgentTaskCommandHandlerTests.cs`** — `Update_modifies_title_description_and_dueOn`; `Update_missing_task_throws_NotFoundException`; `Update_task_owned_by_another_user_throws_NotFoundException` (seed a task with `AssignedToUserId = Guid.NewGuid()`, attempt update with a `StubCurrentUserService` whose `UserId` differs, assert `NotFoundException`).
4. **Create file: `tests/AzmCrm.Application.Tests/Features/AgentTasks/SetAgentTaskCompletionCommandHandlerTests.cs`** — `Completing_sets_IsCompleted_and_CompletedOn`; `Uncompleting_clears_CompletedOn`; `SetCompletion_task_owned_by_another_user_throws_NotFoundException`.
5. **Create file: `tests/AzmCrm.Application.Tests/Features/AgentTasks/DeleteAgentTaskCommandHandlerTests.cs`** — `Delete_sets_IsDeleted_and_DeletedBy_DeletedOn` (assert via `IgnoreQueryFilters()`, mirroring `DeleteCustomerCommandHandlerTests`); `Delete_missing_task_throws_NotFoundException`; `Deleted_task_is_excluded_from_GetById`.
6. **Create file: `tests/AzmCrm.Application.Tests/Features/AgentTasks/GetAgentTasksListQueryHandlerTests.cs`** — `Returns_only_tasks_owned_by_current_user`; `Filters_by_isCompleted`; `Orders_incomplete_first_then_soonest_due_first` (seed one completed task, one incomplete task due tomorrow, one incomplete task due in an hour, one incomplete task with no `DueOn`; assert the returned order is: due-in-an-hour, due-tomorrow, no-due-date, completed).
7. **Create file: `tests/AzmCrm.Application.Tests/Features/AgentTasks/CreateAgentTaskCommandValidatorTests.cs`** — `Empty_Title_fails`; `Title_over_200_chars_fails`; `Valid_command_passes` — construct the validator with `StubLocalizationService`.

## Migration / Rollback

- The EF Core migration generated in Task 3 only **adds** the `AgentTasks` table — additive, safe to apply on top of `20260828165442_AddCommunications` (or whichever migration is latest at implementation time).
- **Rollback**: `dotnet ef database update 20260828165442_AddCommunications --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` drops the `AgentTasks` table. No other table has a foreign key into `AgentTasks`, so this is a clean rollback with no orphaned data.
- **Half-applied state**: same existing behavior as every prior migration — `DatabaseInitializer.InitializeAsync` logs and rethrows on failure, so the app fails to start rather than running against a partially-migrated schema.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Migration applies cleanly:** `dotnet ef database update --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` (or let the API apply it automatically on startup).
4. **Manual smoke test:** obtain a bearer token via `POST /api/identity/login`, then `POST /api/agent-tasks` with `{"title":"Call back customer","dueOn":"2026-09-01T09:00:00Z"}`, confirm 201; `GET /api/agent-tasks` shows it; `PUT /api/agent-tasks/{id}/completion` with `{"isCompleted":true}`, confirm `GET /api/agent-tasks/{id}` shows `isCompleted: true` and a `completedOn` timestamp; `DELETE /api/agent-tasks/{id}`, confirm subsequent `GET /api/agent-tasks/{id}` returns 404; log in as a second user and confirm `GET /api/agent-tasks` never shows the first user's tasks.

## Done Criteria

- [ ] `AgentTask` entity, EF configuration (FKs + indexes), and migration exist and `dotnet ef database update` applies cleanly.
- [ ] `POST /api/agent-tasks`, `GET /api/agent-tasks/{id}`, `GET /api/agent-tasks`, `PUT /api/agent-tasks/{id}`, `PUT /api/agent-tasks/{id}/completion`, `DELETE /api/agent-tasks/{id}` all work end-to-end, scoped strictly to the caller's own tasks.
- [ ] `GET /api/agent-tasks` sorts incomplete-first, soonest-due-first, and supports `isCompleted` filtering.
- [ ] Optional `CustomerId`/`TicketId` links are validated against existing, non-deleted rows on create.
- [ ] All new handler and validator unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 15.**
