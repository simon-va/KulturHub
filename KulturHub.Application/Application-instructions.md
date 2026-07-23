# KulturHub.Application

Konventionen für die Application-Schicht. Hier leben Use Cases, Validierung,
Fehlerkatalog und Port-Interfaces – **ohne** HTTP, Dapper, Npgsql.

## Zweck

Pro Use Case genau einen Handler, einen Input-Record, einen Validator und
(i. d. R.) eine Response. Die Schicht definiert ausschließlich Ports,
deren Implementierung in `KulturHub.Infrastructure` liegt.

## Konventionen

- **Ordner-Layout** spiegelt die API-URL:
  `Features/<Aggregate>/<UseCase>/`
  (`Auth/SignUp`, `Organisations/CreateOrganisation`,
  `Memberships/InviteMember`, `Invitations/...`, `ChangeLogs/...`).
- **Pro Use Case** die typischen Dateien:
  - `XxxHandler.cs` – `public sealed class` mit **Primary Constructor**
    (siehe `CreateOrganisationHandler.cs:10-13`).
  - `XxxInput.cs` / `XxxQuery.cs` – `sealed record`.
  - `XxxInputValidator.cs` – `sealed class : AbstractValidator<...>`.
  - `XxxResponse.cs` – `sealed record` mit `From(entity)`-Factory
    (`CreateOrganisationResponse.cs:10-13`).
- **Methode** heißt `ExecuteAsync`, letzter Parameter ist
  `CancellationToken cancellationToken = default`.
- **DI-Registrierung** zentral in
  `KulturHub.Application/DependencyInjection.cs:28-43` – pro Handler genau
  eine `services.AddScoped<XxxHandler>();`-Zeile. **Keine** Interface-Trennung.
- **Validatoren** werden über `AddValidatorsFromAssembly(...)` aufgesammelt
  (`DependencyInjection.cs:26`) und im Handler **inline** aufgerufen.
- **Eingabe-Konstruktion** ausschließlich im Endpoint: `ClaimsPrincipal` → `user.GetUserId()` → `Input`-Record. Der Handler validiert nicht den `ClaimsPrincipal`.
- **Fehlertexte** der Validator-Regeln sind **Englisch**, die
  `ChangeLog.Message`-Strings sind **Deutsch**.

## Patterns

- **Return** ist immer `Task<ErrorOr<TResponse>>` (oder
  `ErrorOr<Deleted>`/`Success`/`IReadOnlyList<TListItem>`).
- **Validation Chain** als erste Handler-Zeile:
  `await validator.ValidateAsync(input, cancellationToken)` und
  `Result.Errors.Select(e => Error.Validation(e.PropertyName, e.ErrorMessage)).ToList()`.
- **Transaktionen** werden pro Handler erzeugt und beim ersten Schreibvorgang
  an jedes Repository weitergereicht:
  `await using var transaction = await unitOfWork.BeginTransactionAsync(ct);`
  (`CreateOrganisationHandler.cs:33-46`, `InviteMemberHandler.cs:53-66`,
  `DeleteOrganisationMembershipHandler.cs:56-66`).
- **ChangeLog** wird in derselben Transaktion wie die geschriebene Entität
  erzeugt. Geschäftsmutationen **ohne** ChangeLog sind ein Bug.
- **Optimistic Concurrency** läuft im SQL (`AND status = 0` in
  `MembershipRepository.UpdateStatusAsync`) und wird vom Handler interpretiert
  (`rows == 0` → `Error.Conflict`).
- **Errors** kommen aus `KulturHub.Application/Errors/`:
  - Code-Schema `Aggregate.ShortName`, z. B. `Organisation.NameTaken`
    (`OrganisationErrors.cs`).
  - Parameterisierte `FooFailed(string details)` für Exception-Wrapper
    (`AuthErrors.DatabaseInsertFailed(ex.Message)`).
  - Inline-Validation als `Error.Validation("UserId", "UserId is required.")`
    – kein `[PropertyName, message]`-Paar aus dem Validator.
- **Ports** unter `KulturHub.Application/Ports/`:
  - `IDbConnectionFactory`, `IUnitOfWork`, `IUnitOfWorkTransaction`
  - `IOrganisationRepository`, `IMembershipRepository`,
    `IInvitationRepository`, `IUserRepository`, `IChangeLogRepository`
  - `IAuthProvider`, `IUserAdminClient`, `AuthProviderSession`,
    `InvitationFilter`.
- Schreib-Methoden der Repositories nehmen
  `IUnitOfWorkTransaction? transaction = null, CancellationToken cancellationToken = default`.

## Pitfalls

- **Kein** MediatR / Pipeline-Behaviors. Validierung läuft inline.
- **Kein** `WithErrorCode(...)` in FluentValidation. Codes entstehen erst
  beim Mapping auf `Error.Validation(...)`.
- **Keine** `Error.Failure(...)` für Client-Fehler – ist für
  Exception-Wrapper der Infrastruktur reserviert.
- **Keine** Exceptions für Businessfehler. `InvalidOperationException` nur
  als Programmier-Fallback in unerreichbaren `switch default`-Ästen.
- **Keine** Duplizierung von Validierung im Handler – der Validator ist die
  einzige Quelle.
- **Keine** Kopplung an ASP.NET, Dapper oder Npgsql. Drittanbieter-Pakete
  sind ausschließlich `ErrorOr`, `FluentValidation`,
  `FluentValidation.DependencyInjectionExtensions`,
  `Microsoft.Extensions.Logging.Abstractions`.
- **`CancellationToken`** in **jeder** Async-Methode als letzter Parameter
  und bis ins Repository durchreichen.

## AI-Workflow

1. **Lesen**: Handler, Validator, alle vom Handler berührten Repositories
   und die zugehörige `Entity.Recognize`-Methode vollständig lesen.
2. **Regeln extrahieren**: Alle Domain-Invariants und Validator-Regeln als
   Bullet-Liste dokumentieren.
3. **Szenario-Tabelle**: Happy Path + jede Validierungsregel + jede
   Repository-Failure + Auth/Authorization-Fehler.
4. **Implementieren**: Validator **zuerst** schreiben, dann Handler-Skelett,
   dann Repository-Aufrufe, dann ChangeLog.
5. **Verifizieren**: Tests in `KulturHub.UnitTests/Features/...` mit
   Happy Path + jeder Failure schreiben; `dotnet test` muss grün sein.
