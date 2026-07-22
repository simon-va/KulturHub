# KulturHub Copilot Instructions

> **Implementierungsstand**: Aktuell realisiert sind nur `KulturHub.Api` und `KulturHub.UnitTests` (leer) mit dem `Auth`-Use-Case (SignUp mit Invitation-Code, JWT, Supabase, Dapper, PostgreSQL). Die übrigen Abschnitte unten beschreiben die **Zielarchitektur**. Mit `[PLANNED]` markierte Stellen existieren noch nicht im Code.

## Build, Test, and Run Commands

```bash
# Build the entire solution
dotnet build KulturHub.sln

# Run the API (http://localhost:5159)
dotnet run --project KulturHub.Api

# Run all unit tests
dotnet test KulturHub.UnitTests
```

`KulturHub.Worker` ist geplant — siehe Abschnitt *Worker Jobs*.

## Architecture Overview

Clean Architecture. Aktuell vorhanden: 5 Projekte (das Worker-Projekt ist geplant).

| Project | Responsibility | Dependencies | Status |
|---------|---------------|--------------|--------|
| `KulturHub.Domain` | Entities, enums, repository interfaces | None | implementiert |
| `KulturHub.Application` | Use cases / services, FluentValidation validators, application errors, ports (abstractions for infrastructure) | Domain, ErrorOr, FluentValidation | implementiert |
| `KulturHub.Infrastructure` | Repository implementations, external API clients, image generation, file storage, persistence | Application + Domain | implementiert |
| `KulturHub.Api` | Minimal API endpoints, auth, CORS, OpenAPI document | Application + Infrastructure | implementiert |
| `KulturHub.Worker` `[PLANNED]` | Background services (hosted services) for scheduled jobs | Application + Infrastructure | **nicht vorhanden** |
| `KulturHub.UnitTests` | Unit tests for application services | Application + Domain | Projekt vorhanden, **Tests fehlen** |

**Entry points:**
- `KulturHub.Api` — ASP.NET Core API with JWT authentication backed by Supabase. *(implementiert)*
- `KulturHub.Worker` `[PLANNED]` — Background service host running `WeeklyPostJob` and `TokenRefreshJob`.

**Key external integrations:**
- **PostgreSQL** via Dapper (raw SQL; no EF Core). *(implementiert)*
- **Supabase** for JWT authentication and file storage. *(Auth implementiert, Storage geplant)*
- **Chayns API** `[PLANNED]` — for aggregating cultural events.
- **Instagram Graph API** `[PLANNED]` — for publishing carousel posts.

**Database migrations** `[PLANNED]` — manual SQL files in `KulturHub.Infrastructure/migrations/` (Ordner existiert noch nicht) and must be run sequentially in filename order.

## Key Conventions

### Error Handling
- Application services return `ErrorOr<T>` from the ErrorOr library. Never throw exceptions for business errors.
- Application errors are defined in static classes under `KulturHub.Application.Errors` (e.g., `AuthErrors.AlreadyRegistered`, `EventErrors.NotFound(id)`).
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
- All methods (reads and writes) use `IDbConnectionFactory` directly, create/own the connection, and manage their own transactions via `BeginTransactionAsync()` / `CommitAsync()` when multiple statements must run atomically.
- Enums are cast to/from `int` in SQL parameters and result mapping.

### API Endpoints
- Minimal APIs grouped in static classes under `KulturHub.Api.Endpoints`.
- Each feature group exposes a `MapXxxEndpoints(this IEndpointRouteBuilder)` extension method called from `Program.cs`.
- `.RequireAuthorization()` for protected routes. `.RequireOrganisationMembership()` `[PLANNED]`.
- Input DTOs live in `KulturHub.Api.Requests`; output DTOs in `KulturHub.Api.Responses`.

### Application Services
- Located in `KulturHub.Application.Features.{Domain}.{ActionName}`.
- Each service has an interface (e.g., `IAuthService`, `IGetEventsService`) and implementation (e.g., `AuthService`, `GetEventsService`).
- Services return `ErrorOr<T>` and are registered as scoped in `DependencyInjection.cs`.
- FluentValidation validators are registered via `AddValidatorsFromAssembly` in `KulturHub.Application`.

### Tests
- Framework: xUnit + Moq + FluentAssertions.
- Each handler/service gets its own test class: `{ServiceName}Tests.cs`.
- Repositories are always mocked — never use a real database in unit tests.
- **Naming convention:** `MethodName_Scenario_ExpectedResult` (e.g., `Handle_WhenBirthDateIsInFuture_ShouldReturnFailure`).
- **Structure:** list all domain rules as a comment block at the top of the test class, then implement tests.
- **Required test cases per handler:** happy path, each validation rule as a failure case, edge cases (null, empty, boundary values).

### Worker Jobs `[PLANNED]`
- Jobs are `BackgroundService` implementations in `KulturHub.Worker.Jobs`.
- `Worker:RunImmediately: true` in configuration runs jobs immediately on startup for local testing.
- Jobs resolve application services from a DI scope (`IServiceScopeFactory`).

### Configuration
- Secrets managed via `dotnet user-secrets` or `appsettings.json`.
- Required config keys (aktuell): `ConnectionStrings:Default`, `Supabase:Url`, `Supabase:Key`, `Supabase:DiscoveryUrl`, `Cors:AllowedOrigins`.
- Required config keys `[PLANNED]`: `Chayns:*`, weitere Supabase-Storage-Keys.
- HTTP client private environment files are in `KulturHub.Api/http/http-client.private.env.json` (gitignored).

### Code Style
- Target framework: .NET 10.
- Implicit usings and nullable reference types are enabled in all projects.
- All source code (types, methods, variables) is in English.
