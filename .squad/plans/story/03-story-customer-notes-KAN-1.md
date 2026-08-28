# Story 03 — Customer Notes (Story: KAN-1)

## Prerequisites

- [01-story-customer-core-crud-KAN-1.md](01-story-customer-core-crud-KAN-1.md) completed: requires the `Customer` entity, `IApplicationDbContext.Customers`, and `CustomersController`.
- Independent of [02-story-customer-interactions-KAN-1.md](02-story-customer-interactions-KAN-1.md) — notes and interactions are separate child aggregates of `Customer` and can be implemented in either order, but this plan assumes Story 02 landed first only for the shared `JsonStringEnumConverter` change (Task 4 of Story 02); this story adds no enums and does not depend on that change.

## Story Goal

Let support agents attach free-text notes to a customer profile and view a customer's note history, satisfying KAN-1's "Add notes ... to customer records" acceptance criterion.

Outcomes:
1. `POST /api/customers/{customerId}/notes` adds a note to a customer.
2. `GET /api/customers/{customerId}/notes` returns that customer's notes, paginated, newest first.

**Not in scope**: editing or deleting an existing note. The acceptance criteria only require the ability to *add* notes and (per the story description) have "a complete view" of the customer, which the list endpoint satisfies; note editing/removal is a reasonable follow-up but is not implemented here to keep this story's surface minimal and testable.

## Context — Read These Files First

1. [01-story-customer-core-crud-KAN-1.md](01-story-customer-core-crud-KAN-1.md) — read in full. Reuses the same command/query/handler/validator/EF-configuration/controller-action shape.
2. [02-story-customer-interactions-KAN-1.md](02-story-customer-interactions-KAN-1.md) — read in full. `CustomerNote` is structurally simpler than `CustomerInteraction` (no enum, one field) but follows the identical "child of `Customer`, `HasOne(...).WithMany()`, `HasQueryFilter`" EF shape from that story's Task 3.
3. [src/AzmCrm.Domain/Features/Customers/Customer.cs](../../../src/AzmCrm.Domain/Features/Customers/Customer.cs) — created by Story 01. `CustomerNote` references it only via `CustomerId`; `Customer` itself is not edited by this story.
4. [src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommand.cs](../../../src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommand.cs), `CreateCustomerCommandHandler.cs`, `CreateCustomerCommandValidator.cs` (Story 01) — the command/handler/validator triad this story's `CreateCustomerNoteCommand` mirrors.
5. [src/AzmCrm.Application/Features/Customers/Commands/CreateCustomerInteraction/CreateCustomerInteractionCommandHandler.cs](../../../src/AzmCrm.Application/Features/Customers/Commands/CreateCustomerInteraction/CreateCustomerInteractionCommandHandler.cs) (Story 02) — precedent for the "verify the parent customer exists via `AnyAsync`, else throw `NotFoundException`" guard this story's handler reuses verbatim.
6. [src/AzmCrm.Application/Features/Customers/Queries/GetCustomerInteractions/GetCustomerInteractionsQuery.cs](../../../src/AzmCrm.Application/Features/Customers/Queries/GetCustomerInteractions/GetCustomerInteractionsQuery.cs) and its handler (Story 02) — the paginated child-list query shape this story's `GetCustomerNotesQuery` mirrors.
7. [src/AzmCrm.Application/Shared/Exceptions/NotFoundException.cs](../../../src/AzmCrm.Application/Shared/Exceptions/NotFoundException.cs) and [src/AzmCrm.API/Middleware/ExceptionHandlingMiddleware.cs](../../../src/AzmCrm.API/Middleware/ExceptionHandlingMiddleware.cs) lines 26-43 — same 404 mapping used for a missing `customerId`.
8. [src/AzmCrm.API/Controllers/CustomersController.cs](../../../src/AzmCrm.API/Controllers/CustomersController.cs) — edited by Story 01 and Story 02. This story adds two more actions to the same file.

## Implementation tasks

### 1 — Domain layer

**Create file: `src/AzmCrm.Domain/Features/Customers/CustomerNote.cs`**

```csharp
using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.Customers;

public sealed class CustomerNote : BaseEntity
{
    public required Guid CustomerId { get; init; }
    public required string Content { get; set; }

    public Customer Customer { get; init; } = null!;
}
```

### 2 — Application layer

**Create file: `src/AzmCrm.Application/Features/Customers/DTOs/CustomerNoteDto.cs`**

```csharp
namespace AzmCrm.Application.Features.Customers.DTOs;

public sealed record CustomerNoteDto(
    Guid Id,
    Guid CustomerId,
    string Content,
    Guid CreatedBy,
    DateTime CreatedOn
);
```

**Create file: `src/AzmCrm.Application/Features/Customers/DTOs/CreateNoteRequest.cs`**

```csharp
namespace AzmCrm.Application.Features.Customers.DTOs;

public sealed record CreateNoteRequest(string Content);
```

**Create file: `src/AzmCrm.Application/Features/Customers/Commands/CreateCustomerNote/CreateCustomerNoteCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Customers.Commands.CreateCustomerNote;

public sealed record CreateCustomerNoteCommand(Guid CustomerId, string Content) : IRequest<Result<Guid>>;
```

**Create file: `src/AzmCrm.Application/Features/Customers/Commands/CreateCustomerNote/CreateCustomerNoteCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Customers.Commands.CreateCustomerNote;

internal sealed class CreateCustomerNoteCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreateCustomerNoteCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateCustomerNoteCommand request, CancellationToken ct)
    {
        var customerExists = await dbContext.Customers.AnyAsync(c => c.Id == request.CustomerId, ct);
        if (!customerExists)
            throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        var note = new CustomerNote
        {
            CustomerId = request.CustomerId,
            Content = request.Content
        };

        dbContext.CustomerNotes.Add(note);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(note.Id);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Customers/Commands/CreateCustomerNote/CreateCustomerNoteCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Customers.Commands.CreateCustomerNote;

public sealed class CreateCustomerNoteCommandValidator : AbstractValidator<CreateCustomerNoteCommand>
{
    public CreateCustomerNoteCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Customer Id"]);

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Content"])
            .MaximumLength(4000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Content", 4000]);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Customers/Queries/GetCustomerNotes/GetCustomerNotesQuery.cs`**

```csharp
using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomerNotes;

public sealed record GetCustomerNotesQuery(
    Guid CustomerId,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<Result<PaginatedResult<CustomerNoteDto>>>;
```

**Create file: `src/AzmCrm.Application/Features/Customers/Queries/GetCustomerNotes/GetCustomerNotesQueryHandler.cs`**

```csharp
using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomerNotes;

internal sealed class GetCustomerNotesQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetCustomerNotesQuery, Result<PaginatedResult<CustomerNoteDto>>>
{
    public async Task<Result<PaginatedResult<CustomerNoteDto>>> Handle(
        GetCustomerNotesQuery request, CancellationToken ct)
    {
        var customerExists = await dbContext.Customers.AnyAsync(c => c.Id == request.CustomerId, ct);
        if (!customerExists)
            throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        var query = dbContext.CustomerNotes.Where(n => n.CustomerId == request.CustomerId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(n => n.CreatedOn)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => new CustomerNoteDto(n.Id, n.CustomerId, n.Content, n.CreatedBy, n.CreatedOn))
            .ToListAsync(ct);

        var result = new PaginatedResult<CustomerNoteDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<CustomerNoteDto>>.Success(result);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Customers/Queries/GetCustomerNotes/GetCustomerNotesQueryValidator.cs`** — same paging-range rules as `GetCustomersListQueryValidator` (Story 01), plus `RuleFor(x => x.CustomerId).NotEmpty()...`.

**Edit file: `src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs`** — add `DbSet<CustomerNote> CustomerNotes { get; }` next to the members added in Stories 01-02.

### 3 — Infrastructure layer

**Create file: `src/AzmCrm.Infrastructure/Data/Configurations/CustomerNoteConfiguration.cs`**

```csharp
using AzmCrm.Domain.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class CustomerNoteConfiguration : IEntityTypeConfiguration<CustomerNote>
{
    public void Configure(EntityTypeBuilder<CustomerNote> builder)
    {
        builder.ToTable("CustomerNotes");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id)
            .ValueGeneratedNever();

        builder.Property(n => n.Content)
            .IsRequired()
            .HasMaxLength(4000);

        builder.HasOne(n => n.Customer)
            .WithMany()
            .HasForeignKey(n => n.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(n => !n.IsDeleted);

        builder.HasIndex(n => n.CustomerId);
    }
}
```

**Edit file: `src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs`** — add `public DbSet<CustomerNote> CustomerNotes => Set<CustomerNote>();` next to the properties added in Stories 01-02.

**Generate migration:**

```bash
dotnet ef migrations add AddCustomerNotes --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API
```

### 4 — API layer

**Edit file: `src/AzmCrm.API/Controllers/CustomersController.cs`** — add two actions (with corresponding `using` statements for the `CreateCustomerNote`/`GetCustomerNotes` namespaces and `CustomerNoteDto`):

```csharp
[HttpPost("{customerId:guid}/notes")]
[ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> AddNote(Guid customerId, [FromBody] CreateNoteRequest request, CancellationToken ct)
{
    var command = new CreateCustomerNoteCommand(customerId, request.Content);

    var result = await mediator.Send(command, ct);

    return ToCreatedResult(result, id => $"/api/customers/{customerId}/notes/{id}");
}

[HttpGet("{customerId:guid}/notes")]
[ProducesResponseType(typeof(Result<PaginatedResult<CustomerNoteDto>>), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetNotes(
    Guid customerId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
{
    var result = await mediator.Send(new GetCustomerNotesQuery(customerId, pageNumber, pageSize), ct);
    return ToResult(result);
}
```

## Edge Cases & Failure Modes

- **`customerId` in the route does not match an existing, non-deleted customer** — both handlers check `dbContext.Customers.AnyAsync(...)` and throw `NotFoundException` → HTTP 404, identical to the guard added for interactions in Story 02.
- **Empty or whitespace-only `Content`** — rejected by `CreateCustomerNoteCommandValidator`'s `NotEmpty()` rule (FluentValidation's `NotEmpty()` also rejects whitespace-only strings by default).
- **`Content` longer than 4000 characters** — rejected by the validator before the command reaches the handler; also enforced at the database level by `CustomerNoteConfiguration.Property(n => n.Content).HasMaxLength(4000)` as a defense-in-depth constraint.
- **Deleting a customer does not remove or hide its notes** — same caveat as Story 02's interactions: `OnDelete(DeleteBehavior.Cascade)` only applies to a hard delete of `Customer`, which this codebase never performs; a soft-deleted customer's notes remain in the table but become unreachable through the API because `GetCustomerNotesQueryHandler` 404s on the missing/deleted parent.
- **`CreatedBy` on `CustomerNoteDto`** — populated from `BaseEntity.CreatedBy`, which `ApplicationDbContext.SaveChangesAsync` (lines 42-45) stamps from `ICurrentUserService.UserId` at save time; if a note is ever created by an unauthenticated context (should be impossible given `[Authorize]` on `ApiControllerBase`), `CreatedBy` would fall back to `Guid.Empty` per the existing `_currentUserService.UserId ?? Guid.Empty` fallback (`ApplicationDbContext.cs:35`) — not specific to this story, but relevant to interpreting the field.

## Test Plan

1. **Edit `tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs`** (created in Story 01) — add `public DbSet<CustomerNote> CustomerNotes => Set<CustomerNote>();`, and mirror `CustomerNoteConfiguration.HasQueryFilter(n => !n.IsDeleted)` in the context's `OnModelCreating` override (see Stories 01-02 for the same pattern).
2. **Create file: `tests/AzmCrm.Application.Tests/Features/Customers/CreateCustomerNoteCommandHandlerTests.cs`** — `Create_note_for_existing_customer_persists_row`; `Create_note_for_missing_customer_throws_NotFoundException`.
3. **Create file: `tests/AzmCrm.Application.Tests/Features/Customers/GetCustomerNotesQueryHandlerTests.cs`** — `List_returns_notes_ordered_by_CreatedOn_desc`; `List_for_missing_customer_throws_NotFoundException`; `List_is_paginated`.
4. **Create file: `tests/AzmCrm.Application.Tests/Features/Customers/CreateCustomerNoteCommandValidatorTests.cs`** — `Empty_Content_fails`; `Content_over_4000_chars_fails`; `Valid_command_passes` — use `StubLocalizationService` from Story 01.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Migration applies cleanly:** `dotnet ef database update --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` (or let the API apply it automatically on startup).
4. **Manual smoke test:** create a customer (Story 01), then `POST /api/customers/{customerId}/notes` with `{"content":"Called about renewal"}`, confirm 201, then `GET /api/customers/{customerId}/notes` returns it; repeat against a random, non-existent `customerId` and confirm 404.

## Done Criteria

- [ ] `CustomerNote` entity, EF configuration, and migration exist and apply cleanly on top of Stories 01-02's schema.
- [ ] `POST /api/customers/{customerId}/notes` and `GET /api/customers/{customerId}/notes` work end-to-end, both returning 404 for a non-existent/soft-deleted `customerId`.
- [ ] Empty content and over-length content are both rejected with a 400.
- [ ] All new handler and validator unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 04.**
