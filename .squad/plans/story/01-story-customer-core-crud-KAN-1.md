# Story 01 — Customer Profile CRUD & Contact Details (Story: KAN-1)

## Prerequisites

- None. This is the first story in the `story` feature and establishes the `Customer` aggregate that [02-story-customer-interactions-KAN-1.md](02-story-customer-interactions-KAN-1.md), [03-story-customer-notes-KAN-1.md](03-story-customer-notes-KAN-1.md), and [04-story-customer-attachments-KAN-1.md](04-story-customer-attachments-KAN-1.md) attach to via `CustomerId`.

## Story Goal

Give support agents REST endpoints to create, view, edit, and delete customer profiles, storing the core contact details required by KAN-1: phone, email, and postal address. This follows the same Clean Architecture + CQRS (MediatR) + FluentValidation pattern already used by the `Identity` feature (`src/AzmCrm.Application/Features/Identity/`).

Outcomes:
1. `POST /api/customers` creates a customer profile.
2. `GET /api/customers/{id}` returns a single customer profile.
3. `GET /api/customers` returns a paginated, optionally-searched list of customer profiles.
4. `PUT /api/customers/{id}` edits a customer profile.
5. `DELETE /api/customers/{id}` soft-deletes a customer profile.

**Not in scope for this story**: interaction history, notes, and attachments (Stories 02–04 add these against the `Customer` entity created here). Hard/permanent delete, customer merge/de-duplication, and CSV/Excel export are not covered by KAN-1's acceptance criteria and are not implemented.

## Context — Read These Files First

1. [src/AzmCrm.Domain/Common/BaseEntity.cs](../../../src/AzmCrm.Domain/Common/BaseEntity.cs) — read in full (19 lines). The new `Customer` entity extends this to get `Id` (client-assigned `Guid.CreateVersion7()`), `CreatedBy`/`CreatedOn`, `UpdatedBy`/`UpdatedOn`, `IsDeleted`, `DeletedBy`/`DeletedOn`, and the domain-event list. `CreatedBy`/`CreatedOn`/`UpdatedBy`/`UpdatedOn` are auto-stamped by `ApplicationDbContext.SaveChangesAsync` (item 6 below) — never set them by hand in a handler.
2. [src/AzmCrm.Domain/Features/Identity/RefreshToken.cs](../../../src/AzmCrm.Domain/Features/Identity/RefreshToken.cs) — read in full (20 lines). Precedent for a plain (non-`IdentityUser`) `BaseEntity`-adjacent style: `required` properties, computed `bool` properties, a one-directional navigation property (`RefreshToken.User`).
3. [src/AzmCrm.Application/Features/Identity/Commands/Register/RegisterCommand.cs](../../../src/AzmCrm.Application/Features/Identity/Commands/Register/RegisterCommand.cs) (12 lines), [RegisterCommandHandler.cs](../../../src/AzmCrm.Application/Features/Identity/Commands/Register/RegisterCommandHandler.cs) (37 lines), [RegisterCommandValidator.cs](../../../src/AzmCrm.Application/Features/Identity/Commands/Register/RegisterCommandValidator.cs) (36 lines) — the command/handler/validator triad: `sealed record ... : IRequest<Result<T>>`; `internal sealed class ...Handler(...) : IRequestHandler<...>` using primary-constructor DI; `AbstractValidator<T>` built from `ILocalizationService` indexer messages (`RegisterCommandValidator.cs:8-34`).
4. [src/AzmCrm.Application/Features/Identity/Queries/GetCurrentUser/GetCurrentUserQuery.cs](../../../src/AzmCrm.Application/Features/Identity/Queries/GetCurrentUser/GetCurrentUserQuery.cs) (7 lines) and [GetCurrentUserQueryHandler.cs](../../../src/AzmCrm.Application/Features/Identity/Queries/GetCurrentUser/GetCurrentUserQueryHandler.cs) (46 lines) — the query/handler pattern: `sealed record ... : IRequest<Result<TDto>>`.
5. [src/AzmCrm.Domain/Common/Result.cs](../../../src/AzmCrm.Domain/Common/Result.cs) — read in full (38 lines). Every command/query returns `Result` or `Result<T>` — use `Result<T>.Success(data)` / `Result<T>.Failure(...)` / `Result.Success()`.
6. [src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs](../../../src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs) — lines 10-55. Line 22 (`public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();`) and the comment on line 24 (`// Add DbSet properties here for new CRM aggregates (Customers, Leads, Deals, ...).`) mark exactly where to add `public DbSet<Customer> Customers => Set<Customer>();`. Lines 33-54 (`SaveChangesAsync`) auto-stamp `CreatedBy`/`CreatedOn` on `Added` entries and `UpdatedBy`/`UpdatedOn` on `Modified` entries for every tracked `BaseEntity` — it does **not** touch `DeletedBy`/`DeletedOn`, so the delete handler must set those two fields itself (see Edge Cases).
7. [src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs](../../../src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs) — read in full (16 lines). Its doc-comment (lines 7-10) explicitly anticipates new `DbSet<T>` members for new aggregates — add `DbSet<Customer> Customers { get; }` here alongside the existing `DbSet<RefreshToken> RefreshTokens { get; }` (line 14).
8. [src/AzmCrm.Infrastructure/Data/Configurations/RefreshTokenConfiguration.cs](../../../src/AzmCrm.Infrastructure/Data/Configurations/RefreshTokenConfiguration.cs) — read in full (46 lines). Precedent EF configuration shape: `ToTable`, `HasKey`, `Property(...).ValueGeneratedNever()` (id is client-assigned, not DB-generated), `HasMaxLength`, `HasIndex(...).IsUnique()`.
9. [src/AzmCrm.Infrastructure/Data/Configurations/ApplicationUserConfiguration.cs](../../../src/AzmCrm.Infrastructure/Data/Configurations/ApplicationUserConfiguration.cs) — read in full (27 lines). Precedent for a one-to-many relationship: `HasMany(...).WithOne(...).HasForeignKey(...).OnDelete(DeleteBehavior.Cascade)` (lines 22-25) — the pattern Stories 02-04 will reuse against `Customer`.
10. [src/AzmCrm.API/Controllers/Base/ApiControllerBase.cs](../../../src/AzmCrm.API/Controllers/Base/ApiControllerBase.cs) — read in full (29 lines). Controllers inherit `[Authorize]` + `[Route("api/[controller]")]` from this base (lines 7-9). Use the `ToResult`/`ToCreatedResult`/`ToNoContentResult` helpers instead of building `IActionResult`s by hand.
11. [src/AzmCrm.API/Controllers/IdentityController.cs](../../../src/AzmCrm.API/Controllers/IdentityController.cs) — lines 1-35 and 74-89. Precedent controller-action shape: build a command from the request DTO, `await mediator.Send(command, ct)`, return via a `ToXResult` helper. Note the class has **no** `[Route]` of its own (line 16) — the base class's `api/[controller]` route lower-cases the class name (`IdentityController` → `api/identity`), so `CustomersController` will resolve to `api/customers` automatically.
12. [src/AzmCrm.Application/DependencyInjection.cs](../../../src/AzmCrm.Application/DependencyInjection.cs) — read in full (21 lines). `AddMediatR` (line 13) and `AddValidatorsFromAssembly` (line 16) scan `Assembly.GetExecutingAssembly()` — every new command, query, and validator added under `AzmCrm.Application` in this story is auto-registered. **No DI wiring changes are needed in this file.**
13. [src/AzmCrm.Application/Localization/LocalizationKeys.cs](../../../src/AzmCrm.Application/Localization/LocalizationKeys.cs) — read in full (44 lines). Follow the `Validation`/`Identity`/`Common` nested-static-class pattern (lines 5-43) — this story reuses existing `Validation.*` keys and does not need a new nested class yet (Story 04 adds one for file-size errors).
14. [src/AzmCrm.Infrastructure/Localization/Resources/Messages.en.json](../../../src/AzmCrm.Infrastructure/Localization/Resources/Messages.en.json) and [Messages.ar.json](../../../src/AzmCrm.Infrastructure/Localization/Resources/Messages.ar.json) — read both in full (36-37 lines each). No new keys needed for this story (all validators reuse existing `Validation.*` messages).
15. [src/AzmCrm.Application/Shared/Exceptions/NotFoundException.cs](../../../src/AzmCrm.Application/Shared/Exceptions/NotFoundException.cs) (3 lines) and [src/AzmCrm.API/Middleware/ExceptionHandlingMiddleware.cs](../../../src/AzmCrm.API/Middleware/ExceptionHandlingMiddleware.cs) lines 26-43 — `NotFoundException` is already mapped to HTTP 404 by the global exception handler (lines 33-37). Throw it from `GetCustomerById`/`UpdateCustomer`/`DeleteCustomer` handlers when the id doesn't resolve to a non-deleted customer, instead of returning a `Result.Failure`.
16. [src/AzmCrm.Application/Shared/Models/PaginatedResult.cs](../../../src/AzmCrm.Application/Shared/Models/PaginatedResult.cs) — read in full (12 lines). Use this for the customers list query response.
17. [src/AzmCrm.Infrastructure/Data/Migrations/20260826160238_InitialCreate.cs](../../../src/AzmCrm.Infrastructure/Data/Migrations/20260826160238_InitialCreate.cs) — lines 1-59 — current schema baseline (Identity tables + `RefreshTokens`). The new migration for this story adds the `Customers` table alongside these without modifying them.
18. Grep for `ApplyConfigurationsFromAssembly` in `ApplicationDbContext.cs` (line 30) — confirms new `IEntityTypeConfiguration<T>` classes are discovered automatically; no manual registration call is needed for `CustomerConfiguration`.
19. [src/AzmCrm.Infrastructure/Data/DatabaseInitializer.cs](../../../src/AzmCrm.Infrastructure/Data/DatabaseInitializer.cs) — read in full (52 lines). Line 26 (`await context.Database.MigrateAsync(ct);`) runs automatically on app startup (called from `Program.cs`, see item 20) — a locally running API applies the new migration itself; no manual `dotnet ef database update` is required in dev.
20. [src/AzmCrm.API/Program.cs](../../../src/AzmCrm.API/Program.cs) — lines 78-90 (`InitializeDatabaseAsync`). Confirms migrations run at startup before `app.Run()`.

## Implementation tasks

### 1 — Domain layer

**Create file: `src/AzmCrm.Domain/Features/Customers/Customer.cs`**

```csharp
using AzmCrm.Domain.Common;

namespace AzmCrm.Domain.Features.Customers;

public sealed class Customer : BaseEntity
{
    public required string FullName { get; set; }
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}
```

### 2 — Application layer

**Create file: `src/AzmCrm.Application/Features/Customers/DTOs/CustomerDto.cs`**

```csharp
namespace AzmCrm.Application.Features.Customers.DTOs;

public sealed record CustomerDto(
    Guid Id,
    string FullName,
    string? CompanyName,
    string? Email,
    string? PhoneNumber,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    DateTime CreatedOn,
    DateTime? UpdatedOn
);
```

**Create file: `src/AzmCrm.Application/Features/Customers/DTOs/CustomerListItemDto.cs`**

```csharp
namespace AzmCrm.Application.Features.Customers.DTOs;

public sealed record CustomerListItemDto(
    Guid Id,
    string FullName,
    string? CompanyName,
    string? Email,
    string? PhoneNumber,
    DateTime CreatedOn
);
```

**Create file: `src/AzmCrm.Application/Features/Customers/DTOs/CreateCustomerRequest.cs`**

```csharp
namespace AzmCrm.Application.Features.Customers.DTOs;

public sealed record CreateCustomerRequest(
    string FullName,
    string? CompanyName,
    string? Email,
    string? PhoneNumber,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country
);
```

**Create file: `src/AzmCrm.Application/Features/Customers/DTOs/UpdateCustomerRequest.cs`** — identical shape to `CreateCustomerRequest` (no `Id`; it comes from the route).

**Create file: `src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Customers.Commands.CreateCustomer;

public sealed record CreateCustomerCommand(
    string FullName,
    string? CompanyName,
    string? Email,
    string? PhoneNumber,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country
) : IRequest<Result<Guid>>;
```

**Create file: `src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Customers;
using MediatR;

namespace AzmCrm.Application.Features.Customers.Commands.CreateCustomer;

internal sealed class CreateCustomerCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreateCustomerCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateCustomerCommand request, CancellationToken ct)
    {
        var customer = new Customer
        {
            FullName = request.FullName,
            CompanyName = request.CompanyName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            AddressLine1 = request.AddressLine1,
            AddressLine2 = request.AddressLine2,
            City = request.City,
            State = request.State,
            PostalCode = request.PostalCode,
            Country = request.Country
        };

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(customer.Id);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandValidator.cs`**

```csharp
using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Customers.Commands.CreateCustomer;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Full Name"])
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Full Name", 200]);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage(localization[LocalizationKeys.Validation.EmailInvalid])
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^(05\d{8}|\+9665\d{8}|\+?\d{10,15})$")
            .WithMessage(localization[LocalizationKeys.Validation.InvalidPhoneNumber])
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

        RuleFor(x => x.CompanyName).MaximumLength(200)
            .WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Company Name", 200]);
        RuleFor(x => x.AddressLine1).MaximumLength(250)
            .WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Address Line 1", 250]);
        RuleFor(x => x.AddressLine2).MaximumLength(250)
            .WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Address Line 2", 250]);
        RuleFor(x => x.City).MaximumLength(100)
            .WithMessage(localization[LocalizationKeys.Validation.MaxLength, "City", 100]);
        RuleFor(x => x.State).MaximumLength(100)
            .WithMessage(localization[LocalizationKeys.Validation.MaxLength, "State", 100]);
        RuleFor(x => x.PostalCode).MaximumLength(20)
            .WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Postal Code", 20]);
        RuleFor(x => x.Country).MaximumLength(100)
            .WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Country", 100]);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Customers/Commands/UpdateCustomer/UpdateCustomerCommand.cs`** — same fields as `CreateCustomerCommand` plus a leading `Guid Id`, returns `IRequest<Result>` (no data payload needed on success).

**Create file: `src/AzmCrm.Application/Features/Customers/Commands/UpdateCustomer/UpdateCustomerCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Customers.Commands.UpdateCustomer;

internal sealed class UpdateCustomerCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UpdateCustomerCommand, Result>
{
    public async Task<Result> Handle(UpdateCustomerCommand request, CancellationToken ct)
    {
        var customer = await dbContext.Customers.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new NotFoundException($"Customer '{request.Id}' was not found.");

        customer.FullName = request.FullName;
        customer.CompanyName = request.CompanyName;
        customer.Email = request.Email;
        customer.PhoneNumber = request.PhoneNumber;
        customer.AddressLine1 = request.AddressLine1;
        customer.AddressLine2 = request.AddressLine2;
        customer.City = request.City;
        customer.State = request.State;
        customer.PostalCode = request.PostalCode;
        customer.Country = request.Country;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Customers/Commands/UpdateCustomer/UpdateCustomerCommandValidator.cs`** — same rules as `CreateCustomerCommandValidator`, plus `RuleFor(x => x.Id).NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Id"]);`.

**Create file: `src/AzmCrm.Application/Features/Customers/Commands/DeleteCustomer/DeleteCustomerCommand.cs`**

```csharp
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Customers.Commands.DeleteCustomer;

public sealed record DeleteCustomerCommand(Guid Id) : IRequest<Result>;
```

**Create file: `src/AzmCrm.Application/Features/Customers/Commands/DeleteCustomer/DeleteCustomerCommandHandler.cs`**

```csharp
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Customers.Commands.DeleteCustomer;

internal sealed class DeleteCustomerCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<DeleteCustomerCommand, Result>
{
    public async Task<Result> Handle(DeleteCustomerCommand request, CancellationToken ct)
    {
        var customer = await dbContext.Customers.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new NotFoundException($"Customer '{request.Id}' was not found.");

        customer.IsDeleted = true;
        customer.DeletedBy = currentUserService.UserId ?? Guid.Empty;
        customer.DeletedOn = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

Add `using AzmCrm.Application.Shared.Interfaces;` for `ICurrentUserService` (already defined at [ICurrentUserService.cs](../../../src/AzmCrm.Application/Shared/Interfaces/ICurrentUserService.cs), `UserId` property at line 5).

**Create file: `src/AzmCrm.Application/Features/Customers/Queries/GetCustomerById/GetCustomerByIdQuery.cs`**

```csharp
using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomerById;

public sealed record GetCustomerByIdQuery(Guid Id) : IRequest<Result<CustomerDto>>;
```

**Create file: `src/AzmCrm.Application/Features/Customers/Queries/GetCustomerById/GetCustomerByIdQueryHandler.cs`**

```csharp
using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomerById;

internal sealed class GetCustomerByIdQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetCustomerByIdQuery, Result<CustomerDto>>
{
    public async Task<Result<CustomerDto>> Handle(GetCustomerByIdQuery request, CancellationToken ct)
    {
        var customer = await dbContext.Customers.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new NotFoundException($"Customer '{request.Id}' was not found.");

        var dto = new CustomerDto(
            customer.Id, customer.FullName, customer.CompanyName, customer.Email, customer.PhoneNumber,
            customer.AddressLine1, customer.AddressLine2, customer.City, customer.State,
            customer.PostalCode, customer.Country, customer.CreatedOn, customer.UpdatedOn);

        return Result<CustomerDto>.Success(dto);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Customers/Queries/GetCustomersList/GetCustomersListQuery.cs`**

```csharp
using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomersList;

public sealed record GetCustomersListQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null
) : IRequest<Result<PaginatedResult<CustomerListItemDto>>>;
```

**Create file: `src/AzmCrm.Application/Features/Customers/Queries/GetCustomersList/GetCustomersListQueryHandler.cs`**

```csharp
using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomersList;

internal sealed class GetCustomersListQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetCustomersListQuery, Result<PaginatedResult<CustomerListItemDto>>>
{
    public async Task<Result<PaginatedResult<CustomerListItemDto>>> Handle(
        GetCustomersListQuery request, CancellationToken ct)
    {
        var query = dbContext.Customers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(c =>
                c.FullName.ToLower().Contains(term) ||
                (c.Email != null && c.Email.ToLower().Contains(term)) ||
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.CreatedOn)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CustomerListItemDto(
                c.Id, c.FullName, c.CompanyName, c.Email, c.PhoneNumber, c.CreatedOn))
            .ToListAsync(ct);

        var result = new PaginatedResult<CustomerListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<CustomerListItemDto>>.Success(result);
    }
}
```

**Create file: `src/AzmCrm.Application/Features/Customers/Queries/GetCustomersList/GetCustomersListQueryValidator.cs`** — `RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1)...`; `RuleFor(x => x.PageSize).InclusiveBetween(1, 100)...`, messages via `LocalizationKeys.Validation.MustBeGreaterThan` / a literal range message.

**Edit file: `src/AzmCrm.Application/Shared/Interfaces/IApplicationDbContext.cs`** — add `using AzmCrm.Domain.Features.Customers;` and a new member `DbSet<Customer> Customers { get; }` next to line 14 (`DbSet<RefreshToken> RefreshTokens { get; }`).

**Create file: `src/AzmCrm.Application/AssemblyInfo.cs`** — every handler above is `internal sealed class` (matching `RegisterCommandHandler`'s existing convention), so the test project (Task 2 of the Test Plan) cannot construct them without this:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AzmCrm.Application.Tests")]
```

This is assembly-wide — Stories 02-04 do not need to repeat it.

### 3 — Infrastructure layer

**Create file: `src/AzmCrm.Infrastructure/Data/Configurations/CustomerConfiguration.cs`**

```csharp
using AzmCrm.Domain.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzmCrm.Infrastructure.Data.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.CompanyName).HasMaxLength(200);
        builder.Property(c => c.Email).HasMaxLength(256);
        builder.Property(c => c.PhoneNumber).HasMaxLength(20);
        builder.Property(c => c.AddressLine1).HasMaxLength(250);
        builder.Property(c => c.AddressLine2).HasMaxLength(250);
        builder.Property(c => c.City).HasMaxLength(100);
        builder.Property(c => c.State).HasMaxLength(100);
        builder.Property(c => c.PostalCode).HasMaxLength(20);
        builder.Property(c => c.Country).HasMaxLength(100);

        builder.HasQueryFilter(c => !c.IsDeleted);

        builder.HasIndex(c => c.Email);
        builder.HasIndex(c => c.PhoneNumber);
    }
}
```

**Edit file: `src/AzmCrm.Infrastructure/Data/ApplicationDbContext.cs`** — add `using AzmCrm.Domain.Features.Customers;` and, immediately below line 22 (replacing the placeholder comment on line 24), add:

```csharp
public DbSet<Customer> Customers => Set<Customer>();
```

**Generate migration** (do not hand-write the migration file): from the repository root,

```bash
dotnet ef migrations add AddCustomers --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API
```

This produces a new file under `src/AzmCrm.Infrastructure/Data/Migrations/` that creates the `Customers` table alongside the tables already defined in `20260826160238_InitialCreate.cs`. Do not edit that existing migration.

### 4 — API layer

**Create file: `src/AzmCrm.API/Controllers/CustomersController.cs`**

```csharp
using AzmCrm.API.Controllers.Base;
using AzmCrm.Application.Features.Customers.Commands.CreateCustomer;
using AzmCrm.Application.Features.Customers.Commands.DeleteCustomer;
using AzmCrm.Application.Features.Customers.Commands.UpdateCustomer;
using AzmCrm.Application.Features.Customers.DTOs;
using AzmCrm.Application.Features.Customers.Queries.GetCustomerById;
using AzmCrm.Application.Features.Customers.Queries.GetCustomersList;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AzmCrm.API.Controllers;

public sealed class CustomersController(IMediator mediator) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        var command = new CreateCustomerCommand(
            request.FullName, request.CompanyName, request.Email, request.PhoneNumber,
            request.AddressLine1, request.AddressLine2, request.City, request.State,
            request.PostalCode, request.Country);

        var result = await mediator.Send(command, ct);

        return ToCreatedResult(result, id => $"/api/customers/{id}");
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Result<CustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetCustomerByIdQuery(id), ct);
        return ToResult(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(Result<PaginatedResult<CustomerListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetCustomersListQuery(pageNumber, pageSize, search), ct);
        return ToResult(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken ct)
    {
        var command = new UpdateCustomerCommand(
            id, request.FullName, request.CompanyName, request.Email, request.PhoneNumber,
            request.AddressLine1, request.AddressLine2, request.City, request.State,
            request.PostalCode, request.Country);

        var result = await mediator.Send(command, ct);
        return ToResult(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteCustomerCommand(id), ct);
        return ToNoContentResult(result);
    }
}
```

Note: unlike `IdentityController`, every action here relies on the base class's default `[Authorize]` (`ApiControllerBase.cs:8`) — no `[AllowAnonymous]` anywhere, since customer data must always require an authenticated agent.

## Edge Cases & Failure Modes

- **`GetById`/`Update`/`Delete` on a non-existent or already soft-deleted id** — `Customers.FirstOrDefaultAsync` returns `null` because `CustomerConfiguration.HasQueryFilter(c => !c.IsDeleted)` excludes soft-deleted rows from every query by default; the handler throws `NotFoundException`, which `ExceptionHandlingMiddleware.cs:33-37` converts to HTTP 404. Verify a deleted customer's id genuinely 404s on a subsequent `GetById`.
- **`DeleteCustomerCommandHandler` also triggers `ApplicationDbContext.SaveChangesAsync`'s `Modified` branch** (`ApplicationDbContext.cs:46-49`) because setting `IsDeleted`/`DeletedBy`/`DeletedOn` puts the entity in `EntityState.Modified` — `UpdatedBy`/`UpdatedOn` get stamped too. This is expected and harmless; do not special-case it.
- **Empty/whitespace `Search` term** — `GetCustomersListQueryHandler` only applies the `Where` filter `when (!string.IsNullOrWhiteSpace(request.Search))`; an empty or all-whitespace search string returns the unfiltered, paginated list rather than zero rows.
- **`PageNumber` or `PageSize` out of range** — enforced by `GetCustomersListQueryValidator` (`PageNumber >= 1`, `PageSize` between 1 and 100) via the existing `ValidationBehavior` pipeline (`src/AzmCrm.Application/Shared/Behaviors/ValidationBehavior.cs`), which turns failures into a `Result<T>.Failure(...)` returned as 400 — the query handler itself never runs with invalid paging values.
- **Case-insensitive search is done in .NET (`.ToLower().Contains(...)`), not translated to a Postgres index** — acceptable for the current small data volumes; flag as a follow-up if a customer table grows large enough to need a trigram/`ILIKE` index.
- **Concurrent updates to the same customer** — no optimistic concurrency token (`RowVersion`) exists on `Customer`; a last-write-wins semantics applies, consistent with how `ApplicationUser`/`RefreshToken` are handled elsewhere in this codebase. Not addressed by this story.
- **`Email`/`PhoneNumber` uniqueness** — deliberately **not** enforced (no unique index, no duplicate-check validation) because the acceptance criteria only requires storing contact details, not deduplication; two customers may share an email or phone number.

## Test Plan

All new tests live in `tests/AzmCrm.Application.Tests/`, which currently only contains [PlaceholderTests.cs](../../../tests/AzmCrm.Application.Tests/PlaceholderTests.cs) (xUnit, no mocking library referenced — see `tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`).

1. **Edit `tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`** — add `<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.3" />` (matching the `Microsoft.EntityFrameworkCore` version already pinned in `src/AzmCrm.Application/AzmCrm.Application.csproj`). This backs a real, disposable `IApplicationDbContext` for handler tests without hand-mocking `DbSet<T>`.
2. **Create file: `tests/AzmCrm.Application.Tests/TestApplicationDbContext.cs`** — a minimal `DbContext` implementing `IApplicationDbContext` for tests:
   ```csharp
   using AzmCrm.Application.Shared.Interfaces;
   using AzmCrm.Domain.Features.Customers;
   using AzmCrm.Domain.Features.Identity;
   using Microsoft.EntityFrameworkCore;

   namespace AzmCrm.Application.Tests;

   public sealed class TestApplicationDbContext(DbContextOptions<TestApplicationDbContext> options)
       : DbContext(options), IApplicationDbContext
   {
       public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
       public DbSet<Customer> Customers => Set<Customer>();
   }
   ```
   `DbContext.Entry<TEntity>` and `DbContext.SaveChangesAsync` already satisfy `IApplicationDbContext`'s matching members — no explicit interface implementation is needed. Add a `static Create()` helper that builds `new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options` so each test gets an isolated instance without repeating the boilerplate.

   **Also override `OnModelCreating`** to mirror `CustomerConfiguration.HasQueryFilter(c => !c.IsDeleted)` (`modelBuilder.Entity<Customer>().HasQueryFilter(c => !c.IsDeleted);`) — `TestApplicationDbContext` never runs `ApplyConfigurationsFromAssembly`, so without this override the in-memory provider has no soft-delete filter at all and a test asserting a deleted customer is invisible (Task 6 below) will fail with "no exception was thrown" even though the real, Infrastructure-backed `ApplicationDbContext` behaves correctly. This is the concrete case the original plan's contingency note anticipated.
3. **Create file: `tests/AzmCrm.Application.Tests/TestDoubles/StubLocalizationService.cs`** — a hand-written `ILocalizationService` stub (see [ILocalizationService.cs](../../../src/AzmCrm.Application/Localization/ILocalizationService.cs)) whose indexers/`GetString` overloads return the raw key (or `string.Format(key, args)` for the `params object[]` overloads) so validator tests can assert on the key rather than a localized string.
4. **Create file: `tests/AzmCrm.Application.Tests/Features/Customers/CreateCustomerCommandHandlerTests.cs`** — `Create_persists_customer_and_returns_new_id` (asserts `Result.IsSuccess`, `Customers.Count() == 1`, and the persisted row's fields match the command).
5. **Create file: `tests/AzmCrm.Application.Tests/Features/Customers/UpdateCustomerCommandHandlerTests.cs`** — `Update_existing_customer_persists_changes`; `Update_missing_customer_throws_NotFoundException`.
6. **Create file: `tests/AzmCrm.Application.Tests/Features/Customers/DeleteCustomerCommandHandlerTests.cs`** — `Delete_sets_IsDeleted_and_DeletedBy_DeletedOn` (inspect the soft-deleted row via `dbContext.Customers.IgnoreQueryFilters().SingleAsync(...)`, since the query filter from Task 2 above now hides it from a plain query); `Delete_missing_customer_throws_NotFoundException`; `Deleted_customer_is_excluded_from_GetById` (create, delete, then run `GetCustomerByIdQueryHandler` against the same context and assert it throws `NotFoundException`).
7. **Create file: `tests/AzmCrm.Application.Tests/Features/Customers/GetCustomersListQueryHandlerTests.cs`** — `List_returns_paginated_results_ordered_by_CreatedOn_desc`; `List_filters_by_search_term_case_insensitively`; `List_with_blank_search_returns_all`.
8. **Create file: `tests/AzmCrm.Application.Tests/Features/Customers/CreateCustomerCommandValidatorTests.cs`** — `Empty_FullName_fails`; `Invalid_email_fails`; `Invalid_phone_number_fails`; `Valid_command_passes` — construct the validator with `StubLocalizationService`.

## Migration / Rollback

- The EF Core migration generated in Task 3 only **adds** the new `Customers` table — it does not alter any existing table, so it is additive and safe to apply to a database that already has the `InitialCreate` migration applied.
- **Rollback**: `dotnet ef database update <previous-migration-name> --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` (i.e. roll back to `InitialCreate`) drops the `Customers` table. Since no other table has a foreign key into `Customers` yet in this story, this is a clean rollback with no orphaned data.
- **Half-applied state**: if the migration fails partway through, `DatabaseInitializer.InitializeAsync` (`DatabaseInitializer.cs:21-38`) logs and rethrows (line 36), so the app fails to start rather than running against a partially-migrated schema — this is existing behavior, not new to this story.

## Verification Steps

1. **Backend builds:** `dotnet build` from the repository root.
2. **Unit tests:** `dotnet test tests/AzmCrm.Application.Tests/AzmCrm.Application.Tests.csproj`.
3. **Migration applies cleanly:** `dotnet ef database update --project src/AzmCrm.Infrastructure --startup-project src/AzmCrm.API` against a local Postgres instance (or let `dotnet run --project src/AzmCrm.API` apply it automatically on startup per `DatabaseInitializer.cs:26`).
4. **Manual smoke test:** with the API running, `POST /api/customers` with a bearer token from `POST /api/identity/login`, then `GET /api/customers/{id}` returns the created profile, `PUT /api/customers/{id}` edits it, `GET /api/customers` lists it, and `DELETE /api/customers/{id}` followed by `GET /api/customers/{id}` returns 404.

## Done Criteria

- [ ] `Customer` entity, EF configuration, and migration exist and `dotnet ef database update` applies cleanly.
- [ ] `POST /api/customers`, `GET /api/customers/{id}`, `GET /api/customers`, `PUT /api/customers/{id}`, `DELETE /api/customers/{id}` all work end-to-end against a real Postgres database.
- [ ] Deleting a customer is a soft delete (`IsDeleted = true`) and the customer no longer appears in `GetById` or the list query.
- [ ] All new handler and validator unit tests pass (`dotnet test`).
- [ ] `dotnet build` succeeds with no new warnings introduced by this story's code.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 02.**
