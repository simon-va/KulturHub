# KulturHub Copilot Instructions

## Build, Test, and Run Commands

```bash
# Build the entire solution
dotnet build KulturHub.sln

# Run the API (http://localhost:5159)
dotnet run --project KulturHub.Api

# Run the background worker
dotnet run --project KulturHub.Worker

# Run all unit tests
dotnet test KulturHub.UnitTests

# Run a single test class
dotnet test KulturHub.UnitTests --filter "FullyQualifiedName~WeeklyPostServiceTests"

# Run a single test method
dotnet test KulturHub.UnitTests --filter "FullyQualifiedName~Handle_WhenNoEventsFound_ShouldReturnEmptyGuid"
```

## Architecture Overview

Clean Architecture with 6 projects:

| Project | Responsibility | Dependencies |
|---------|---------------|--------------|
| `KulturHub.Domain` | Entities, enums, repository interfaces, domain exceptions | None |
| `KulturHub.Application` | Use cases / services, FluentValidation validators, application errors, ports (abstractions for infrastructure) | Domain, ErrorOr, FluentValidation |
| `KulturHub.Infrastructure` | Repository implementations, external API clients, image generation, file storage, persistence | Application + Domain |
| `KulturHub.Api` | Minimal API endpoints, auth, CORS, OpenAPI document | Application + Infrastructure |
| `KulturHub.Worker` | Background services (hosted services) for scheduled jobs | Application + Infrastructure |
| `KulturHub.UnitTests` | Unit tests for application services | Application + Domain |

**Two entry points:**
- `KulturHub.Api` — ASP.NET Core API with JWT authentication backed by Supabase.
- `KulturHub.Worker` — Background service host running `WeeklyPostJob` and `TokenRefreshJob`.

**Key external integrations:**
- **PostgreSQL** via Dapper (raw SQL; no EF Core).
- **Supabase** for JWT authentication and file storage.
- **OpenAI** for AI chat services.
- **Chayns API** for aggregating cultural events.
- **Instagram Graph API** for publishing carousel posts.
- **SkiaSharp** for server-side image generation.

**Database migrations** are manual SQL files in `KulturHub.Infrastructure/migrations/` and must be run sequentially in filename order.

## Key Conventions

### Error Handling
- Application services return `ErrorOr<T>` from the ErrorOr library. Never throw exceptions for business errors.
- Domain entities throw `DomainException` for invariant violations.
- Application errors are defined in static classes under `KulturHub.Application.Errors` (e.g., `EventErrors.NotFound(id)`).
- The API layer maps `ErrorOr` errors to HTTP results via `ErrorExtensions.ToResult()`.

### Domain Entities
- Entities live in `KulturHub.Domain.Entities`.
- Use factory methods (`CreateDraft(...)`, `Create(...)`, `Reconstitute(...)`) instead of public constructors for complex objects.
- `Reconstitute` is used exclusively by repositories to hydrate entities from database rows.
- Business rules and state transitions are enforced inside entity methods, not in application services.

### Repositories & Persistence
- Repositories implement interfaces defined in `KulturHub.Domain.Interfaces`.
- Located in `KulturHub.Infrastructure.Persistence.Repositories`.
- Use Dapper with raw SQL (no EF Core).
- **Reads** use `IDbConnectionFactory` directly and create/own the connection.
- **Writes** use `IConnectionProvider` to participate in the scoped `UnitOfWork` transaction.
- Enums are cast to/from `int` in SQL parameters and result mapping.

### Unit of Work
- `UnitOfWorkEndpointFilter` wraps mutating endpoints in a database transaction.
- Mark write endpoints with `.WithUnitOfWork()` in endpoint definitions.
- `IUnitOfWork` and `IConnectionProvider` are scoped and backed by `UnitOfWork`.

### API Endpoints
- Minimal APIs grouped in static classes under `KulturHub.Api.Endpoints`.
- Each feature group exposes a `MapXxxEndpoints(this IEndpointRouteBuilder)` extension method called from `Program.cs`.
- Use `.RequireAuthorization()` and `.RequireOrganisationMembership()` for protected routes.
- Input DTOs live in `KulturHub.Api.Requests`; output DTOs in `KulturHub.Api.Responses`.

### Application Services
- Located in `KulturHub.Application.Features.{Domain}.{ActionName}`.
- Each service has an interface (e.g., `IGetEventsService`) and implementation (e.g., `GetEventsService`).
- Services return `ErrorOr<T>` and are registered as scoped in `DependencyInjection.cs`.
- FluentValidation validators are registered via `AddValidatorsFromAssembly` in `KulturHub.Application`.

### Tests
- Framework: xUnit + Moq + FluentAssertions.
- Each handler/service gets its own test class: `{ServiceName}Tests.cs`.
- Repositories are always mocked — never use a real database in unit tests.
- **Naming convention:** `MethodName_Scenario_ExpectedResult` (e.g., `Handle_WhenBirthDateIsInFuture_ShouldReturnFailure`).
- **Structure:** list all domain rules as a comment block at the top of the test class, then implement tests.
- **Required test cases per handler:** happy path, each validation rule as a failure case, edge cases (null, empty, boundary values).

### Worker Jobs
- Jobs are `BackgroundService` implementations in `KulturHub.Worker.Jobs`.
- `Worker:RunImmediately: true` in configuration runs jobs immediately on startup for local testing.
- Jobs resolve application services from a DI scope (`IServiceScopeFactory`).

### Configuration
- Secrets managed via `dotnet user-secrets` or `appsettings.json`.
- Required config keys: `ConnectionStrings:Default`, `Chayns:*`, `Supabase:*`, `OpenAI:ApiKey`, `Cors:AllowedOrigins`.
- HTTP client private environment files are in `KulturHub.Api/http/http-client.private.env.json` (gitignored).

### Code Style
- Target framework: .NET 10.
- Implicit usings and nullable reference types are enabled in all projects.
- All source code (types, methods, variables) is in English.
