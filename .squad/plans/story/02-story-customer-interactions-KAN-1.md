# Story 02 — Customer Interaction History (Story: KAN-1)

## Prerequisites

- [01-story-customer-core-crud-KAN-1.md](01-story-customer-core-crud-KAN-1.md) completed: the `Customer` entity, `IApplicationDbContext.Customers`, `CustomersController`, and the `TestApplicationDbContext`/`StubLocalizationService` test doubles must exist before this story attaches interactions to a customer.

## Story Goal

Let support agents log and view a chronological interaction history (calls, emails, meetings, WhatsApp, SMS, or other touchpoints) against a customer profile, satisfying KAN-1's "View full interaction history per customer" acceptance criterion.

Outcomes:
1. `POST /api/customers/{customerId}/interactions` logs a new interaction against a customer.
2. `GET /api/customers/{customerId}/interactions` returns that customer's interaction history, paginated, newest first.

**Design note (not a hallucinated dependency):** KAN-2 ("Ticket Management System") and KAN-3 ("Communication Channels Integration") — see `.squad/stories/story/KAN-2/intake.md` and `.squad/stories/story/KAN-3/intake.md` — will likely become additional sources of interaction records once built (e.g. auto-logging a ticket or an email/WhatsApp message as an interaction). This story only implements the **manual** logging path support agents can use today; it does not implement or assume any integration with those not-yet-built features. **Not in scope**: editing or deleting a logged interaction, and automatic interaction creation from other modules.

## Context — Read These Files First

1. [01-story-customer-core-crud-KAN-1.md](01-story-customer-core-crud-KAN-1.md) — read in full. This story repeats the same command/query/handler/validator/EF-configuration/controller-action shape it establishes; do not re-derive the pattern from scratch.
2. [src/AzmCrm.Domain/Features/Customers/Customer.cs](../../../src/AzmCrm.Domain/Features/Customers/Customer.cs) — created by Story 01. `CustomerInteraction` references it only via `CustomerId` (a plain `Guid` foreign key) and an optional one-directional navigation property — `Customer` itself is **not** edited by this story (no collection navigation is added to it), keeping Story 01's file untouched.
3. [src/AzmCrm.Infrastructure/Data/Configurations/ApplicationUserConfiguration.cs](../../../src/AzmCrm.Infrastructure/Data/Configurations/ApplicationUserConfiguration.cs) lines 22-25 — the `HasMany(...).WithOne(...).HasForeignKey(...).OnDelete(DeleteBehavior.Cascade)` pattern this story's `CustomerInteractionConfiguration` reuses in the opposite direction (`HasOne(...).WithMany()` with no inverse collection).
4. [src/AzmCrm.API/Extensions/ApplicationExtensions.cs](../../../src/AzmCrm.API/Extensions/ApplicationExtensions.cs) line 14 (`services.AddControllers();`) — this story changes this call to register a `JsonStringEnumConverter` (see Task 4) so `InteractionType` serializes as its name (`"Call"`) rather than an integer over the wire.
5. [src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommand.cs](../../../src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommand.cs), `CreateCustomerCommandHandler.cs`, `CreateCustomerCommandValidator.cs` (all created in Story 01) — the exact command/handler/validator triad to mirror for `CreateCustomerInteractionCommand`.
6. [src/AzmCrm.Application/Features/Customers/Queries/GetCustomersList/GetCustomersListQuery.cs](../../../src/AzmCrm.Application/Features/Customers/Queries/GetCustomersList/GetCustomersListQuery.cs) and its handler (created in Story 01) — the paginated-query shape to mirror for `GetCustomerInteractionsQuery`.
7. [src/AzmCrm.Application/Shared/Exceptions/NotFoundException.cs](../../../src/AzmCrm.Application/Shared/Exceptions/NotFoundException.cs) and [src/AzmCrm.API/Middleware/ExceptionHandlingMiddleware.cs](../../../src/AzmCrm.API/Middleware/ExceptionHandlingMiddleware.cs) lines 26-43 — throw `NotFoundException` when `customerId` doesn't resolve to an existing, non-deleted customer.
8. [src/AzmCrm.API/Controllers/CustomersController.cs](../../../src/AzmCrm.API/Controllers/CustomersController.cs) — created by Story 01. This story **edits** this file to add two new actions rather than creating a new controller, keeping all customer-scoped endpoints under `api/customers`.
9. [src/AzmCrm.Application/Localization/LocalizationKeys.cs](../../../src/AzmCrm.Application/Localization/LocalizationKeys.cs) lines 6-18 (`Validation` nested class) — add one new key, `InvalidValue`, following the existing naming convention.
10. [src/AzmCrm.Infrastructure/Localization/Resources/Messages.en.json](../../../src/AzmCrm.Infrastructure/Localization/Resources/Messages.en.json) and [Messages.ar.json](../../../src/AzmCrm.Infrastructure/Localization/Resources/Messages.ar.json) — add the matching English/Arabic text for `Validation.InvalidValue` under the existing `"Validation"` object (`Messages.en.json:2-14`, `Messages.ar.json:2-14`).

## Implementation tasks

### 1 — Domain layer

**Create file: `src/AzmCrm.Domain/Features/Customers/InteractionType.cs`**

```csharp
namespace AzmCrm.Domain.Features.Customers;

public enum InteractionType
{
    Call,
    Email,
    Meeting,
    WhatsApp,
    Sms,
    Other
}
```

**Create file: `src/AzmCrm.Domain/Features/Customers/CustomerInteraction.cs`**

```csharp
using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.Customers;

public sealed class CustomerInteraction : BaseEntity
{
    public required Guid CustomerId { get; init; }
    public required InteractionType Type { get; set; }
    public required string Subject { get; set; }
    public string? Description { get; set; }
    public required DateTime OccurredOn { get; set; }

    public Customer Customer { get; init; } = null!;
}
```

### 2 — Application layer

**Create file: `src/AzmCrm.Application/Features/Customers/DTOs/CustomerInteractionDto.cs`**

```csharp
using AzmCrm.Domain.Features.Customers;

namespace AzmCrm.Application.Features.Customers.DTOs;

public sealed record CustomerInteractionDto(
    Guid Id,
    Guid CustomerId,
    InteractionType Type,
    string Subject,
    string? Description,
    DateTime OccurredOn,
    DateTime CreatedOn
);
```

**Create file: `src/AzmCrm.Application/Features/Customers/DTOs/CreateInteractionRequest.cs`**

```csharp
using AzmCrm.Domain.Features.Customers;

namespace AzmCrm.Application.Features.Customers.DTOs;

public sealed record CreateInteractionRequest(
    InteractionType Type,
    string Subject,
    string? Description,
    DateTime OccurredOn
);
```

**Create file: `src/AzmCrm.Application/Features/Customers/Commands/CreateCustomerInteraction/CreateCustomerInteractionCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Customers;
using MediatR;

namespace AzmCrm.Application.Features.Customers.Commands.CreateCustomerInteraction;

public sealed record CreateCustomerInteractionCommand(
    Guid CustomerId,
    InteractionType Type,
    string Subject,
    string? Description,
    DateTime OccurredOn
) : IRequest<Result<Guid>>;
```

**Create file: `src/AzmCrm.Application/Features/Customers/Commands/CreateCustomerInteraction/CreateCustomerInteractionCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Customers.Commands.CreateCustomerInteraction;

internal sealed class CreateCustomerInteractionCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreateCustomerInteractionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateCustomerInteractionCommand request, CancellationToken ct)
    {
        var customerExists = await dbContext.Customers.AnyAsync(c => c.Id == request.CustomerId, ct);
        if (!customerExists)
            throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        var interaction = new CustomerInteraction
        {
            CustomerId = request.CustomerId,
            Type = request.Type,
            Subject = request.Subject,
            Description = request.Description,
            OccurredOn = request.OccurredOn
        };

        dbContext.CustomerInteractions.Add(interaction);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(interaction.Id);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Customers/Commands/CreateCustomerInteraction/CreateCustomerInteractionCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Customers.Commands.CreateCustomerInteraction;

public sealed class CreateCustomerInteractionCommandValidator : AbstractValidator<CreateCustomerInteractionCommand>
{
    public CreateCustomerInteractionCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Customer Id"]);

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage(localization[LocalizationKeys.Validation.InvalidValue, "Type"]);

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Subject"])
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Subject", 200]);

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Description", 2000]);

        RuleFor(x => x.OccurredOn)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Occurred On"]);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Customers/Queries/GetCustomerInteractions/GetCustomerInteractionsQuery.cs`**

```csharp
using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomerInteractions;

public sealed record GetCustomerInteractionsQuery(
    Guid CustomerId,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<Result<PaginatedResult<CustomerInteractionDto>>>;
```

**Create file: `src/AzmCrm.Application/Features/Customers/Queries/GetCustomerInteractions/GetCustomerInteractionsQueryHandler.cs`**

```csharp
using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomerInteractions;

internal sealed class GetCustomerInteractionsQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetCustomerInteractionsQuery, Result<PaginatedResult<CustomerInteractionDto>>>
{
    public async Task<Result<PaginatedResult<CustomerInteractionDto>>> Handle(
        GetCustomerInteractionsQuery request, CancellationToken ct)
    {
        var customerExists = await dbContext.Customers.AnyAsync(c => c.Id == request.CustomerId, ct);
        if (!customerExists)
            throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        var query = dbContext.CustomerInteractions.Where(i => i.CustomerId == request.CustomerId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(i => i.OccurredOn)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(i => new CustomerInteractionDto(
                i.Id, i.CustomerId, i.Type, i.Subject, i.Description, i.OccurredOn, i.CreatedOn))
            .ToListAsync(ct);

        var result = new PaginatedResult<CustomerInteractionDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<CustomerInteractionDto>>.Success(result);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Customers/Queries/GetCustomerInteractions/GetCustomerInteractionsQueryValidator.cs`** — same paging-range rules as `GetCustomersListQueryValidator` (Story 01), plus `RuleFor(x => x.CustomerId).NotEmpty()...`.

**Edit file: `src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs`** — add `DbSet<CustomerInteraction> CustomerInteractions { get; }` next to the `Customers` member added in Story 01.

**Edit file: `src/AzmCrm.Application/Localization/LocalizationKeys.cs`** — add to the `Validation` nested class (after line 17, `IdMismatch`):
```csharp
public const string InvalidValue = "Validation.InvalidValue";
```

**Edit file: `src/AzmCrm.Infrastructure/Localization/Resources/Messages.en.json`** — add to the `"Validation"` object: `"InvalidValue": "{0} is not a valid value."`.

**Edit file: `src/AzmCrm.Infrastructure/Localization/Resources/Messages.ar.json`** — add to the `"Validation"` object: `"InvalidValue": "{0} قيمة غير صالحة."`.

### 3 — Infrastructure layer

**Create file: `src/AzmCrm.Infrastructure/Data/Configurations/CustomerInteractionConfiguration.cs`**

```csharp
using AzmCrm.Domain.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class CustomerInteractionConfiguration : IEntityTypeConfiguration<CustomerInteraction>
{
    public void Configure(EntityTypeBuilder<CustomerInteraction> builder)
    {
        builder.ToTable("CustomerInteractions");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .ValueGeneratedNever();

        builder.Property(i => i.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.Subject)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.Description)
            .HasMaxLength(2000);

        builder.Property(i => i.OccurredOn)
            .IsRequired();

        builder.HasOne(i => i.Customer)
            .WithMany()
            .HasForeignKey(i => i.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(i => !i.IsDeleted);

        builder.HasIndex(i => i.CustomerId);
    }
}
```

**Edit file: `src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs`** — add `public DbSet<CustomerInteraction> CustomerInteractions => Set<CustomerInteraction>();` next to the `Customers` property added in Story 01.

**Generate migration:**

```bash
dotnet ef migrations add AddCustomerInteractions --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API
```

### 4 — API layer

**Edit file: `src/AzmCrm.API/Extensions/ApplicationExtensions.cs`** — replace line 14 with:

```csharp
services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```

Add `using System.Text.Json.Serialization;` to the file's `using` block. This makes `InteractionType` (and any future enum in the API surface) serialize/deserialize as its name (e.g. `"Call"`) instead of its underlying `int` — required so `[FromBody] CreateInteractionRequest.Type` accepts `"Call"` rather than `0`.

**Edit file: `src/AzmCrm.API/Controllers/CustomersController.cs`** — add two actions (add the corresponding `using` statements for `CreateCustomerInteraction` and `GetCustomerInteractions` namespaces, and for `CustomerInteractionDto`):

```csharp
[HttpPost("{customerId:guid}/interactions")]
[ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> AddInteraction(
    Guid customerId, [FromBody] CreateInteractionRequest request, CancellationToken ct)
{
    var command = new CreateCustomerInteractionCommand(
        customerId, request.Type, request.Subject, request.Description, request.OccurredOn);

    var result = await mediator.Send(command, ct);

    return ToCreatedResult(result, id => $"/api/customers/{customerId}/interactions/{id}");
}

[HttpGet("{customerId:guid}/interactions")]
[ProducesResponseType(typeof(Result<PaginatedResult<CustomerInteractionDto>>), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetInteractions(
    Guid customerId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
{
    var result = await mediator.Send(new GetCustomerInteractionsQuery(customerId, pageNumber, pageSize), ct);
    return ToResult(result);
}
```

## Edge Cases & Failure Modes

- **`customerId` in the route does not match an existing, non-deleted customer** — both the create and list handlers explicitly check `dbContext.Customers.AnyAsync(c => c.Id == request.CustomerId, ct)` (the `Customer` query filter from Story 01 excludes soft-deleted rows) and throw `NotFoundException`, mapped to 404 by `ExceptionHandlingMiddleware.cs:33-37`. Logging an interaction against a soft-deleted customer is explicitly rejected rather than silently succeeding.
- **`Type` sent as an unrecognized string** (e.g. `"Fax"`) — with `JsonStringEnumConverter` added in Task 4, ASP.NET Core's model binder fails to deserialize the request body before the command even reaches MediatR, producing a 400 from the framework's own JSON deserialization error — not from `CreateCustomerInteractionCommandValidator`. Document this in the API contract; the validator's `IsInEnum()` rule is a defense-in-depth check that only fires if an out-of-range **integer** ever reaches the command directly (e.g. from a future non-JSON caller), since a malformed enum name never reaches `Handle`.
- **`OccurredOn` in the far future or far past** — no upper/lower bound is enforced; agents may need to log a historical interaction that predates this system, so only `NotEmpty` is validated. Flag as a follow-up if backdating abuse becomes a concern.
- **Very large `Description`** — capped at 2000 characters by both `CreateCustomerInteractionCommandValidator` and `CustomerInteractionConfiguration.Property(i => i.Description).HasMaxLength(2000)` — a value from a future bulk-import path that exceeds this fails validation before it ever reaches EF Core's `HasMaxLength` truncation/exception behavior.
- **Deleting a customer (Story 01's `DeleteCustomerCommandHandler`) does not cascade-delete or soft-delete its interactions** — `CustomerInteractionConfiguration.OnDelete(DeleteBehavior.Cascade)` (line configuring the FK) only fires on a **hard** delete of the `Customer` row, which this codebase never performs (Story 01 only sets `IsDeleted = true`). A soft-deleted customer's interactions remain in the `CustomerInteractions` table and are only unreachable via `GET /api/customers/{customerId}/interactions` because that handler itself 404s (per the first bullet above) — the rows are not orphaned or cleaned up. This is intentional (preserves history) but should be called out to reviewers.

## Test Plan

1. **Edit `tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs`** (created in Story 01) — add `public DbSet<CustomerInteraction> CustomerInteractions => Set<CustomerInteraction>();`, and mirror `CustomerInteractionConfiguration.HasQueryFilter(i => !i.IsDeleted)` in the context's `OnModelCreating` override (added in Story 01 for `Customer`) for the same reason: `TestApplicationDbContext` never runs `ApplyConfigurationsFromAssembly`.
2. **Create file: `tests/AzmCrm.Application.Tests/Features/Customers/CreateCustomerInteractionCommandHandlerTests.cs`** — `Create_interaction_for_existing_customer_persists_row`; `Create_interaction_for_missing_customer_throws_NotFoundException`.
3. **Create file: `tests/AzmCrm.Application.Tests/Features/Customers/GetCustomerInteractionsQueryHandlerTests.cs`** — `List_returns_interactions_ordered_by_OccurredOn_desc`; `List_for_missing_customer_throws_NotFoundException`; `List_is_paginated`.
4. **Create file: `tests/AzmCrm.Application.Tests/Features/Customers/CreateCustomerInteractionCommandValidatorTests.cs`** — `Empty_Subject_fails`; `Undefined_enum_value_fails` (cast an out-of-range `int` to `InteractionType` to exercise `IsInEnum()` directly, since the JSON layer would otherwise intercept an invalid string before validation runs); `Valid_command_passes` — use `StubLocalizationService` from Story 01.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Migration applies cleanly:** `dotnet ef database update --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` (or let the API apply it automatically on startup).
4. **Manual smoke test:** create a customer (Story 01), then `POST /api/customers/{customerId}/interactions` with `{"type":"Call","subject":"Follow-up","occurredOn":"2026-08-26T10:00:00Z"}`, confirm 201, then `GET /api/customers/{customerId}/interactions` returns it; repeat against a random, non-existent `customerId` and confirm 404.

## Done Criteria

- [ ] `CustomerInteraction` entity, EF configuration, and migration exist and apply cleanly on top of Story 01's schema.
- [ ] `POST /api/customers/{customerId}/interactions` and `GET /api/customers/{customerId}/interactions` work end-to-end, both returning 404 for a non-existent/soft-deleted `customerId`.
- [ ] `InteractionType` round-trips as a JSON string (e.g. `"Call"`), not an integer, in both request and response bodies.
- [ ] All new handler and validator unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 03.**
