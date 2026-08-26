# Azm CRM — Backend API

.NET 10 Clean Architecture backend for the Azm CRM product (customers, leads, deals, ...).

This is a fresh scaffold: it ships the cross-cutting infrastructure (CQRS via MediatR,
FluentValidation, the `Result` pattern, JWT auth, Serilog, health checks, Swagger) plus one ported
feature — Identity (register/login/refresh/revoke/current-user) — and no CRM business features
yet. See [`docs/architecture.md`](docs/architecture.md) for the layer layout and how to add a new
feature.

## Projects

```
src/
  AzmCrm.Domain            Entities, Result pattern, domain events
  AzmCrm.Application        MediatR commands/queries, validators, application interfaces
  AzmCrm.Infrastructure      EF Core (PostgreSQL), ASP.NET Identity, JWT
  AzmCrm.API                 ASP.NET Core Web API host
tests/
  AzmCrm.Application.Tests   xUnit test project
```

## Prerequisites

- .NET 10 SDK
- PostgreSQL (local instance or via `Docker/docker-compose.yml`)

## Configuration

Before running, set the JWT secret and database connection string via user-secrets or environment
variables — **do not** commit real values into `appsettings.json` or
`appsettings.Development.json`, which only contain placeholders.

```bash
cd src/AzmCrm.API
dotnet user-secrets init
dotnet user-secrets set "JwtSettings:Secret" "<a long random string, 32+ chars>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=AzmCrm;Username=postgres;Password=<your-password>"
```

Or via environment variables (e.g. in `Docker/docker-compose.yml` or your shell):

```bash
export JwtSettings__Secret="<a long random string, 32+ chars>"
export ConnectionStrings__DefaultConnection="Host=localhost;Database=AzmCrm;Username=postgres;Password=<your-password>"
```

## Running

```bash
dotnet restore
dotnet build
dotnet run --project src/AzmCrm.API
```

The API listens on `http://localhost:5100` (see `src/AzmCrm.API/Properties/launchSettings.json`).
Swagger UI is at `/swagger`, health checks at `/health`.

## Migrations

Once PostgreSQL is reachable and the connection string is configured:

```bash
dotnet ef migrations add InitialCreate -p src/AzmCrm.Infrastructure -s src/AzmCrm.API
dotnet ef database update -p src/AzmCrm.Infrastructure -s src/AzmCrm.API
```

The API also applies pending migrations automatically on startup (see
`AzmCrm.Infrastructure/Data/DatabaseInitializer.cs`) — no seed data is inserted.

## Tests

```bash
dotnet test
```

## Docker

```bash
cd Docker
cp .env.example .env   # fill in POSTGRES_*, JWT_SECRET, etc.
docker compose up --build
```
