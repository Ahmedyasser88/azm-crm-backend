# Architecture

Azm CRM Backend follows a Clean Architecture layout, mirroring the pattern used across Azm Tech
.NET backends:

```
AzmCrm.Domain          → entities, value objects, domain events, Result pattern. No dependencies
                          on other layers.
AzmCrm.Application      → CQRS use cases (MediatR commands/queries + handlers), FluentValidation
                          validators, application-facing interfaces (IApplicationDbContext,
                          ICurrentUserService, ...), the Result-returning contract every handler
                          returns. Depends only on Domain.
AzmCrm.Infrastructure   → EF Core (Npgsql), ASP.NET Identity, JWT issuing/validation, and other
                          concrete implementations of Application interfaces. Depends on
                          Application + Domain.
AzmCrm.API              → ASP.NET Core Web API host: controllers, middleware, Swagger, Serilog,
                          rate limiting, CORS. Depends on Application + Infrastructure.
```

## CQRS conventions

- Every use case is a MediatR `IRequest<Result>` / `IRequest<Result<T>>` command or query living
  under `Features/<FeatureName>/Commands|Queries/<UseCaseName>/`.
- Each command/query has a matching `FluentValidation` validator picked up automatically by
  `ValidationBehavior`, which short-circuits to `Result.Failure` on validation errors instead of
  throwing.
- Handlers depend on `IApplicationDbContext` (never the concrete `ApplicationDbContext`) so the
  Application layer never references EF Core or Npgsql directly.
- Controllers stay thin: map the HTTP request to a command/query, call `mediator.Send`, and
  translate the `Result` to an `IActionResult` via `ApiControllerBase` helpers (`ToResult`,
  `ToCreatedResult`, `ToNoContentResult`, ...).

## Adding a new CRM feature (e.g. Customers)

1. **Domain**: add the entity under `AzmCrm.Domain/Features/Customers/`, inheriting `BaseEntity`
   where soft-delete/audit fields are needed.
2. **Application**: add `IApplicationDbContext.Customers` (a `DbSet<Customer>`), then add
   `Features/Customers/Commands/CreateCustomer/...` and `Queries/GetCustomer/...` following the
   Identity feature as a template.
3. **Infrastructure**: add an `IEntityTypeConfiguration<Customer>` under `Data/Configurations/`,
   add the `DbSet<Customer>` to `ApplicationDbContext`, then run
   `dotnet ef migrations add AddCustomers -p src/AzmCrm.Infrastructure -s src/AzmCrm.API`.
4. **API**: add a controller under `Controllers/` inheriting `ApiControllerBase`.

## Identity

The scaffold ships one ported feature — Identity (register/login/refresh/revoke/me) — backed by
ASP.NET Identity (`ApplicationUser : IdentityUser<Guid>`) and JWT bearer auth. It is intentionally
generic (no legal-library-specific fields) and ready to extend with CRM-specific roles/claims.
