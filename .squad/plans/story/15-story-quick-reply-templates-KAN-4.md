# Story 15 — Quick Reply Templates (Story: KAN-4)

## Prerequisites

- None. This story introduces its own `QuickReplyTemplate` entity, `Features/QuickReplies` folder, and `QuickReplyTemplatesController`, editing no file created by [13-story-dashboard-core-tickets-customers-KAN-4.md](13-story-dashboard-core-tickets-customers-KAN-4.md), [14-story-agent-tasks-reminders-KAN-4.md](14-story-agent-tasks-reminders-KAN-4.md), or [16-story-ticket-collaboration-comments-KAN-4.md](16-story-ticket-collaboration-comments-KAN-4.md). It can be implemented in any order relative to the other three KAN-4 stories.

## Story Goal

Let any support agent maintain a shared library of canned response templates (a title plus a body) that the team can reuse when replying to customers, satisfying KAN-4's **"Use quick reply templates"** acceptance criterion.

Outcomes:
1. `POST /api/quick-reply-templates` creates a template with a required `Title` and `Body`. Templates are **team-shared, not per-agent** — any authenticated agent can create one, and every agent sees the full list; `CreatedBy` (inherited from `BaseEntity`, auto-stamped) records who authored it, but authorship carries no access restriction.
2. `GET /api/quick-reply-templates/{id}` returns a single template.
3. `GET /api/quick-reply-templates` returns a paginated, optionally title/body-searched list, ordered alphabetically by `Title` — templates are picked from what is effectively a dropdown/picker, not read as a chronological feed, so alphabetical order (not newest-first) is the right default here.
4. `PUT /api/quick-reply-templates/{id}` edits `Title`/`Body`. Any authenticated agent may edit any template (no ownership check) — see "Not in scope" below.
5. `DELETE /api/quick-reply-templates/{id}` soft-deletes a template.

**Not in scope**: per-agent private templates, role-based restriction of who can create/edit/delete a template (every authenticated agent has equal access — there is no "admin-only" concept in this codebase's `ApplicationUser`/role model to hang such a restriction on), template categories/folders, variable/placeholder substitution (e.g. `{{customerName}}`) in a template body, and wiring a template directly into `POST /api/conversations/{id}/messages` (KAN-3 Story 08) — the frontend fetches a template's `Body` from this API and pastes it into the existing send-message call itself; no change to `SendMessageCommand`/`ConversationsController` is made here.

## Context — Read These Files First

1. [src/AzmCrm.Domain/Common/BaseEntity.cs](../../../src/AzmCrm.Domain/Common/BaseEntity.cs) — read in full (19 lines). `QuickReplyTemplate` extends this: `Id` (client-assigned `Guid.CreateVersion7()`), `CreatedBy`/`CreatedOn`/`UpdatedBy`/`UpdatedOn` (auto-stamped), `IsDeleted`/`DeletedBy`/`DeletedOn` (soft delete).
2. [src/AzmCrm.Domain/Features/Customers/CustomerNote.cs](../../../src/AzmCrm.Domain/Features/Customers/CustomerNote.cs) — read in full (11 lines). The simplest existing entity in this codebase (one required FK, one required string) — `QuickReplyTemplate` is even simpler (no FK at all, two required strings: `Title`, `Body`).
3. [src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandHandler.cs](../../../src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandHandler.cs), [UpdateCustomer/UpdateCustomerCommandHandler.cs](../../../src/AzmCrm.Application/Features/Customers/Commands/UpdateCustomer/UpdateCustomerCommandHandler.cs), and [DeleteCustomer/DeleteCustomerCommandHandler.cs](../../../src/AzmCrm.Application/Features/Customers/Commands/DeleteCustomer/DeleteCustomerCommandHandler.cs) — read all three in full. Exact "construct-and-save" / "load-or-404, mutate, save" / "load-or-404, soft-delete, save" handler shapes this story's four command handlers follow — **without** the ownership filter KAN-4 Story 14 (`AgentTask`) added, since templates are shared, not per-agent.
4. [src/AzmCrm.Application/Features/Customers/Queries/GetCustomersList/GetCustomersListQueryHandler.cs](../../../src/AzmCrm.Application/Features/Customers/Queries/GetCustomersList/GetCustomersListQueryHandler.cs) — read in full (47 lines). Exact filter→count→page→project shape `GetQuickReplyTemplatesListQueryHandler` follows, including the `.ToLower().Contains(term)` search pattern (lines 15-20) applied to `Title`/`Body` — but ordered `OrderBy(t => t.Title)` ascending instead of `OrderByDescending(c => c.CreatedOn)` (see Story Goal, outcome 3, and Edge Cases for why).
5. [src/AzmCrm.Application/Features/Customers/DTOs/CustomerListItemDto.cs](../../../src/AzmCrm.Application/Features/Customers/DTOs/CustomerListItemDto.cs) and [CustomerDto.cs](../../../src/AzmCrm.Application/Features/Customers/DTOs/CustomerDto.cs) — read both in full. Precedent for a full `...Dto` (with `UpdatedOn`) vs. a lighter `...ListItemDto` (without it) — `QuickReplyTemplateDto`/`QuickReplyTemplateListItemDto` follow the same split.
6. [src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandValidator.cs](../../../src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandValidator.cs) — read in full (38 lines). Exact `RuleFor(...).NotEmpty()...MaximumLength(...)` shape this story's validators reuse.
7. [src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs) — read in full (27 lines, current end-state after KAN-3). Add `using AzmCrm.Domain.Features.QuickReplies;` and, after the existing `Message` member: `DbSet<QuickReplyTemplate> QuickReplyTemplates { get; }`.
8. [src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs](../../../src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs) — read in full (66 lines). Lines 25-33 are the `DbSet<T>` properties to extend; line 35's comment marks where. `SaveChangesAsync` (lines 44-65) auto-stamps `CreatedBy`/`CreatedOn`/`UpdatedBy`/`UpdatedOn` — covers `QuickReplyTemplate` automatically.
9. [src/AzmCrm.Infrastructure/Data/Configurations/CustomerNoteConfiguration.cs](../../../src/AzmCrm.Infrastructure/Data/Configurations/CustomerNoteConfiguration.cs) — read in full (31 lines). Exact EF configuration shape `QuickReplyTemplateConfiguration` mirrors, minus the `HasOne`/`HasForeignKey` block (no FK on this entity).
10. [src/AzmCrm.Application/Shared/Exceptions/NotFoundException.cs](../../../src/AzmCrm.Application/Shared/Exceptions/NotFoundException.cs) (3 lines) and [src/AzmCrm.API/Middleware/ExceptionHandlingMiddleware.cs](../../../src/AzmCrm.API/Middleware/ExceptionHandlingMiddleware.cs) lines 26-43 (`NotFoundException` → HTTP 404 at lines 33-37).
11. [src/AzmCrm.API/Controllers/CustomersController.cs](../../../src/AzmCrm.API/Controllers/CustomersController.cs) — lines 22-80 (`Create`/`GetById`/`GetList`/`Update`/`Delete`). Exact controller-action shape `QuickReplyTemplatesController` mirrors.
12. [src/AzmCrm.Application/Localization/LocalizationKeys.cs](../../../src/AzmCrm.Application/Localization/LocalizationKeys.cs) — read in full (46 lines). This story reuses `Validation.Required` and `Validation.MaxLength` only.
13. [src/AzmCrm.Infrastructure/Data/Migrations/20260828165442_AddCommunications.cs](../../../src/AzmCrm.Infrastructure/Data/Migrations/20260828165442_AddCommunications.cs) — the most recent migration; the new migration for this story adds the `QuickReplyTemplates` table on top of this baseline (or on top of whichever KAN-4 story's migration is latest at implementation time — see Migration / Rollback).
14. [tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs](../../../tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs) — read in full (44 lines). Add `QuickReplyTemplate` `DbSet<T>` property and its query filter, following the exact pattern already used for every other aggregate.
15. [tests/AzmCrm.Application.Tests/Features/Customers/DeleteCustomerCommandHandlerTests.cs](../../../tests/AzmCrm.Application.Tests/Features/Customers/DeleteCustomerCommandHandlerTests.cs) — read in full (63 lines). Precedent for asserting soft-delete via `IgnoreQueryFilters()`.

## Implementation tasks

### 1 — Domain layer

**Create file: `src/AzmCrm.Domain/Features/QuickReplies/QuickReplyTemplate.cs`**

```csharp
using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.QuickReplies;

public sealed class QuickReplyTemplate : BaseEntity
{
    public required string Title { get; set; }
    public required string Body { get; set; }
}
```

### 2 — Application layer

**Create file: `src/AzmCrm.Application/Features/QuickReplies/DTOs/QuickReplyTemplateDto.cs`**

```csharp
namespace AzmCrm.Application.Features.QuickReplies.DTOs;

public sealed record QuickReplyTemplateDto(
    Guid Id, string Title, string Body, DateTime CreatedOn, DateTime? UpdatedOn);
```

**Create file: `src/AzmCrm.Application/Features/QuickReplies/DTOs/QuickReplyTemplateListItemDto.cs`**

```csharp
namespace AzmCrm.Application.Features.QuickReplies.DTOs;

public sealed record QuickReplyTemplateListItemDto(Guid Id, string Title, string Body, DateTime CreatedOn);
```

**Create file: `src/AzmCrm.Application/Features/QuickReplies/DTOs/CreateQuickReplyTemplateRequest.cs`**

```csharp
namespace AzmCrm.Application.Features.QuickReplies.DTOs;

public sealed record CreateQuickReplyTemplateRequest(string Title, string Body);
```

**Create file: `src/AzmCrm.Application/Features/QuickReplies/DTOs/UpdateQuickReplyTemplateRequest.cs`**

```csharp
namespace AzmCrm.Application.Features.QuickReplies.DTOs;

public sealed record UpdateQuickReplyTemplateRequest(string Title, string Body);
```

**Create file: `src/AzmCrm.Application/Features/QuickReplies/Commands/CreateQuickReplyTemplate/CreateQuickReplyTemplateCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.QuickReplies.Commands.CreateQuickReplyTemplate;

public sealed record CreateQuickReplyTemplateCommand(string Title, string Body) : IRequest<Result<Guid>>;
```

**Create file: `src/AzmCrm.Application/Features/QuickReplies/Commands/CreateQuickReplyTemplate/CreateQuickReplyTemplateCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.QuickReplies;
using MediatR;

namespace AzmCrm.Application.Features.QuickReplies.Commands.CreateQuickReplyTemplate;

internal sealed class CreateQuickReplyTemplateCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreateQuickReplyTemplateCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateQuickReplyTemplateCommand request, CancellationToken ct)
    {
        var template = new QuickReplyTemplate
        {
            Title = request.Title,
            Body = request.Body
        };

        dbContext.QuickReplyTemplates.Add(template);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(template.Id);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/QuickReplies/Commands/CreateQuickReplyTemplate/CreateQuickReplyTemplateCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.QuickReplies.Commands.CreateQuickReplyTemplate;

public sealed class CreateQuickReplyTemplateCommandValidator : AbstractValidator<CreateQuickReplyTemplateCommand>
{
    public CreateQuickReplyTemplateCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Title"])
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Title", 200]);

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Body"])
            .MaximumLength(4000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Body", 4000]);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/QuickReplies/Commands/UpdateQuickReplyTemplate/UpdateQuickReplyTemplateCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.QuickReplies.Commands.UpdateQuickReplyTemplate;

public sealed record UpdateQuickReplyTemplateCommand(Guid Id, string Title, string Body) : IRequest<Result>;
```

**Create file: `src/AzmCrm.Application/Features/QuickReplies/Commands/UpdateQuickReplyTemplate/UpdateQuickReplyTemplateCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.QuickReplies.Commands.UpdateQuickReplyTemplate;

internal sealed class UpdateQuickReplyTemplateCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UpdateQuickReplyTemplateCommand, Result>
{
    public async Task<Result> Handle(UpdateQuickReplyTemplateCommand request, CancellationToken ct)
    {
        var template = await dbContext.QuickReplyTemplates.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException($"Quick reply template '{request.Id}' was not found.");

        template.Title = request.Title;
        template.Body = request.Body;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

**Create file: `src/AzmCrm.Application/Features/QuickReplies/Commands/UpdateQuickReplyTemplate/UpdateQuickReplyTemplateCommandValidator.cs`** — same rules as `CreateQuickReplyTemplateCommandValidator` plus `RuleFor(x => x.Id).NotEmpty()...`.

**Create file: `src/AzmCrm.Application/Features/QuickReplies/Commands/DeleteQuickReplyTemplate/DeleteQuickReplyTemplateCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.QuickReplies.Commands.DeleteQuickReplyTemplate;

public sealed record DeleteQuickReplyTemplateCommand(Guid Id) : IRequest<Result>;
```

**Create file: `src/AzmCrm.Application/Features/QuickReplies/Commands/DeleteQuickReplyTemplate/DeleteQuickReplyTemplateCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.QuickReplies.Commands.DeleteQuickReplyTemplate;

internal sealed class DeleteQuickReplyTemplateCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<DeleteQuickReplyTemplateCommand, Result>
{
    public async Task<Result> Handle(DeleteQuickReplyTemplateCommand request, CancellationToken ct)
    {
        var template = await dbContext.QuickReplyTemplates.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException($"Quick reply template '{request.Id}' was not found.");

        template.IsDeleted = true;
        template.DeletedBy = currentUserService.UserId ?? Guid.Empty;
        template.DeletedOn = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

**Create file: `src/AzmCrm.Application/Features/QuickReplies/Commands/DeleteQuickReplyTemplate/DeleteQuickReplyTemplateCommandValidator.cs`** — `RuleFor(x => x.Id).NotEmpty()...` only.

**Create file: `src/AzmCrm.Application/Features/QuickReplies/Queries/GetQuickReplyTemplateById/GetQuickReplyTemplateByIdQuery.cs`**

```csharp
using AzmCrm.Application.Features.QuickReplies.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.QuickReplies.Queries.GetQuickReplyTemplateById;

public sealed record GetQuickReplyTemplateByIdQuery(Guid Id) : IRequest<Result<QuickReplyTemplateDto>>;
```

**Create file: `src/AzmCrm.Application/Features/QuickReplies/Queries/GetQuickReplyTemplateById/GetQuickReplyTemplateByIdQueryHandler.cs`**

```csharp
using AzmCrm.Application.Features.QuickReplies.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.QuickReplies.Queries.GetQuickReplyTemplateById;

internal sealed class GetQuickReplyTemplateByIdQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetQuickReplyTemplateByIdQuery, Result<QuickReplyTemplateDto>>
{
    public async Task<Result<QuickReplyTemplateDto>> Handle(
        GetQuickReplyTemplateByIdQuery request, CancellationToken ct)
    {
        var template = await dbContext.QuickReplyTemplates.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException($"Quick reply template '{request.Id}' was not found.");

        var dto = new QuickReplyTemplateDto(
            template.Id, template.Title, template.Body, template.CreatedOn, template.UpdatedOn);

        return Result<QuickReplyTemplateDto>.Success(dto);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/QuickReplies/Queries/GetQuickReplyTemplatesList/GetQuickReplyTemplatesListQuery.cs`**

```csharp
using AzmCrm.Application.Features.QuickReplies.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.QuickReplies.Queries.GetQuickReplyTemplatesList;

public sealed record GetQuickReplyTemplatesListQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null
) : IRequest<Result<PaginatedResult<QuickReplyTemplateListItemDto>>>;
```

**Create file: `src/AzmCrm.Application/Features/QuickReplies/Queries/GetQuickReplyTemplatesList/GetQuickReplyTemplatesListQueryHandler.cs`**

```csharp
using AzmCrm.Application.Features.QuickReplies.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.QuickReplies.Queries.GetQuickReplyTemplatesList;

internal sealed class GetQuickReplyTemplatesListQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetQuickReplyTemplatesListQuery, Result<PaginatedResult<QuickReplyTemplateListItemDto>>>
{
    public async Task<Result<PaginatedResult<QuickReplyTemplateListItemDto>>> Handle(
        GetQuickReplyTemplatesListQuery request, CancellationToken ct)
    {
        var query = dbContext.QuickReplyTemplates.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(t => t.Title.ToLower().Contains(term) || t.Body.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            // Alphabetical by Title, not newest-first — templates are picked from a dropdown/
            // picker, not read as a chronological feed. See Story Goal, outcome 3.
            .OrderBy(t => t.Title)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new QuickReplyTemplateListItemDto(t.Id, t.Title, t.Body, t.CreatedOn))
            .ToListAsync(ct);

        var result = new PaginatedResult<QuickReplyTemplateListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<QuickReplyTemplateListItemDto>>.Success(result);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/QuickReplies/Queries/GetQuickReplyTemplatesList/GetQuickReplyTemplatesListQueryValidator.cs`** — same paging-range rules as `GetTicketsListQueryValidator` (`PageNumber >= 1`, `PageSize` between 1 and 100).

**Edit file: `src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs`** — add `using AzmCrm.Domain.Features.QuickReplies;` and, after `DbSet<Message> Messages { get; }` (or after `DbSet<AgentTask> AgentTasks { get; }` if Story 14 already landed):

```csharp
DbSet<QuickReplyTemplate> QuickReplyTemplates { get; }
```

### 3 — Infrastructure layer

**Create file: `src/AzmCrm.Infrastructure/Data/Configurations/QuickReplyTemplateConfiguration.cs`**

```csharp
using AzmCrm.Domain.Features.QuickReplies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class QuickReplyTemplateConfiguration : IEntityTypeConfiguration<QuickReplyTemplate>
{
    public void Configure(EntityTypeBuilder<QuickReplyTemplate> builder)
    {
        builder.ToTable("QuickReplyTemplates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedNever();

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Body)
            .IsRequired()
            .HasMaxLength(4000);

        builder.HasQueryFilter(t => !t.IsDeleted);

        builder.HasIndex(t => t.Title);
    }
}
```

**Edit file: `src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs`** — add `using AzmCrm.Domain.Features.QuickReplies;` and, replacing the placeholder comment:

```csharp
public DbSet<QuickReplyTemplate> QuickReplyTemplates => Set<QuickReplyTemplate>();
```

**Generate migration:**

```bash
dotnet ef migrations add AddQuickReplyTemplates --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API
```

### 4 — API layer

**Create file: `src/AzmCrm.API/Controllers/QuickReplyTemplatesController.cs`**

```csharp
using AzmCrm.API.Controllers.Base;
using AzmCrm.Application.Features.QuickReplies.Commands.CreateQuickReplyTemplate;
using AzmCrm.Application.Features.QuickReplies.Commands.DeleteQuickReplyTemplate;
using AzmCrm.Application.Features.QuickReplies.Commands.UpdateQuickReplyTemplate;
using AzmCrm.Application.Features.QuickReplies.DTOs;
using AzmCrm.Application.Features.QuickReplies.Queries.GetQuickReplyTemplateById;
using AzmCrm.Application.Features.QuickReplies.Queries.GetQuickReplyTemplatesList;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AzmCrm.API.Controllers;

public sealed class QuickReplyTemplatesController(IMediator mediator) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateQuickReplyTemplateRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateQuickReplyTemplateCommand(request.Title, request.Body), ct);

        return ToCreatedResult(result, id => $"/api/quick-reply-templates/{id}");
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Result<QuickReplyTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetQuickReplyTemplateByIdQuery(id), ct);
        return ToResult(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(Result<PaginatedResult<QuickReplyTemplateListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetQuickReplyTemplatesListQuery(pageNumber, pageSize, search), ct);
        return ToResult(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateQuickReplyTemplateRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateQuickReplyTemplateCommand(id, request.Title, request.Body), ct);
        return ToResult(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteQuickReplyTemplateCommand(id), ct);
        return ToNoContentResult(result);
    }
}
```

Every action relies on the base class's default `[Authorize]` — templates require an authenticated agent to read or write, but no per-agent ownership check restricts which agent may edit or delete which template (see Story Goal, "Not in scope").

## Edge Cases & Failure Modes

- **Any authenticated agent can edit or delete any other agent's template** — `UpdateQuickReplyTemplateCommandHandler`/`DeleteQuickReplyTemplateCommandHandler` load by `Id` only, with no `CreatedBy` ownership filter (unlike KAN-4 Story 14's `AgentTask`, which is per-agent). This is a deliberate scope choice for a shared team resource; document it clearly since it differs from Story 14's ownership-scoped pattern in the same feature set.
- **Two agents edit the same template concurrently** — no optimistic concurrency token exists on `QuickReplyTemplate` (matching every other entity in this codebase); the second `SaveChangesAsync` simply overwrites the first agent's change with a "last write wins" outcome. Flag as a known gap if concurrent template editing becomes a real workflow problem.
- **`Search` matches on both `Title` and `Body`** — a template can be found by searching for a phrase that only appears in its body text, not just its title; this mirrors `GetCustomersListQueryHandler`'s multi-field search (Context item 4).
- **An empty `QuickReplyTemplates` table** — `GetQuickReplyTemplatesListQueryHandler` returns a `PaginatedResult` with `TotalCount = 0` and an empty `Items`, not an error; this is the expected state before any agent has created a template.
- **`PageNumber`/`PageSize` out of range** — enforced by `GetQuickReplyTemplatesListQueryValidator` via the existing `ValidationBehavior` pipeline, turned into a 400 before the handler runs.
- **A template's `Body` is used to send a message** — happens entirely client-side (the frontend reads `GetQuickReplyTemplateByIdQuery`'s `Body` and pastes it into `POST /api/conversations/{id}/messages`'s existing `SendMessageRequest.Body` field); no server-side link or usage-count is recorded between the two, since `SendMessageCommand` is not modified by this story (see Story Goal, "Not in scope").

## Test Plan

All new tests live in `tests/AzmCrm.Application.Tests/`, following the existing `TestApplicationDbContext`/`StubCurrentUserService`/`StubLocalizationService` infrastructure.

1. **Edit `tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs`** — add `public DbSet<QuickReplyTemplate> QuickReplyTemplates => Set<QuickReplyTemplate>();` and `modelBuilder.Entity<QuickReplyTemplate>().HasQueryFilter(t => !t.IsDeleted);` to `OnModelCreating`.
2. **Create file: `tests/AzmCrm.Application.Tests/Features/QuickReplies/CreateQuickReplyTemplateCommandHandlerTests.cs`** — `Create_persists_template`.
3. **Create file: `tests/AzmCrm.Application.Tests/Features/QuickReplies/UpdateQuickReplyTemplateCommandHandlerTests.cs`** — `Update_modifies_title_and_body`; `Update_missing_template_throws_NotFoundException`.
4. **Create file: `tests/AzmCrm.Application.Tests/Features/QuickReplies/DeleteQuickReplyTemplateCommandHandlerTests.cs`** — `Delete_sets_IsDeleted_and_DeletedBy_DeletedOn` (assert via `IgnoreQueryFilters()`); `Delete_missing_template_throws_NotFoundException`; `Deleted_template_is_excluded_from_GetById`.
5. **Create file: `tests/AzmCrm.Application.Tests/Features/QuickReplies/GetQuickReplyTemplatesListQueryHandlerTests.cs`** — `List_returns_results_ordered_alphabetically_by_title` (seed templates titled "Zebra", "Apple", "Mango"; assert returned order is Apple, Mango, Zebra); `List_filters_by_search_term_matching_title_or_body`.
6. **Create file: `tests/AzmCrm.Application.Tests/Features/QuickReplies/CreateQuickReplyTemplateCommandValidatorTests.cs`** — `Empty_Title_fails`; `Empty_Body_fails`; `Valid_command_passes` — construct the validator with `StubLocalizationService`.

## Migration / Rollback

- The EF Core migration generated in Task 3 only **adds** the `QuickReplyTemplates` table — additive, safe to apply on top of whichever migration is latest at implementation time (`20260828165442_AddCommunications`, or a later KAN-4 migration if Stories 13/14/16 land first).
- **Rollback**: `dotnet ef database update <previous-migration-name> --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` drops the `QuickReplyTemplates` table. No other table has a foreign key into it, so this is a clean rollback with no orphaned data.
- **Half-applied state**: same existing behavior as every prior migration — `DatabaseInitializer.InitializeAsync` logs and rethrows on failure, so the app fails to start rather than running against a partially-migrated schema.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Migration applies cleanly:** `dotnet ef database update --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` (or let the API apply it automatically on startup).
4. **Manual smoke test:** obtain a bearer token via `POST /api/identity/login`, then `POST /api/quick-reply-templates` with `{"title":"Order Delay","body":"We're sorry for the delay, your order is on its way."}`, confirm 201; `GET /api/quick-reply-templates` shows it; `GET /api/quick-reply-templates?search=delay` still finds it; `PUT /api/quick-reply-templates/{id}` with updated `body`, confirm `GET /api/quick-reply-templates/{id}` reflects it; `DELETE /api/quick-reply-templates/{id}`, confirm subsequent `GET` returns 404; log in as a second user and confirm they can still see and edit the first user's remaining templates (shared, not per-agent).

## Done Criteria

- [ ] `QuickReplyTemplate` entity, EF configuration, and migration exist and `dotnet ef database update` applies cleanly.
- [ ] `POST /api/quick-reply-templates`, `GET /api/quick-reply-templates/{id}`, `GET /api/quick-reply-templates`, `PUT /api/quick-reply-templates/{id}`, `DELETE /api/quick-reply-templates/{id}` all work end-to-end for any authenticated agent.
- [ ] `GET /api/quick-reply-templates` sorts alphabetically by title and supports a `search` filter over title and body.
- [ ] All new handler and validator unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 16.**
