# KulturHub.Application

Hier liegt die komplette Anwendungslogik.

## Enthält

- **Use-Case-Handler** – je Feature ein Handler mit `HandleAsync`
- **Daten laden und speichern** – über den injizierten `IAppDbContext`
  (Application darf EF Core direkt nutzen)
- **Validieren** – FluentValidation-Validatoren pro Request (nur wenn
  der Request Eingabedaten hat)
- **Requests / Responses / Commands** – Übergabeobjekte zwischen
  Endpoint und Handler
- **Ports** – Interfaces für externe Systeme und testbare Domain-Helfer

## Verschmelzung mit Infrastructure

Pragmatisch, nicht dogmatisch:

- **`IAppDbContext`** wird im Application-Layer definiert
  (`Abstractions/Persistence/IAppDbContext.cs`) und vom `AppDbContext`
  in Infrastructure implementiert. So bleibt `DbSet<T>`-Zugriff möglich,
  ohne dass Application den konkreten DbContext kennt.
- **Application darf `IQueryable<T>` und LINQ-To-Entities** nutzen.
  Filter und Includes werden im Handler geschrieben.
- **Keine Repository-Abstraktion um jeden Preis.** Eine kleine
  EF-Core-Abfrage direkt im Handler ist klarer als ein dediziertes
  Repository.
- **Ports** unter `Application/Ports/` sind **zwei** Zwecken vorbehalten:
  1. **Externe Systeme** (`IAuthProvider`, `IUserAdminClient`, …).
  2. **Testbare Domain-Helfer** (statische Generatoren, kryptografische
     Funktionen), die sonst nur durch Reflection testbar wären.

## Ordnerstruktur

```
KulturHub.Application/
├── DependencyInjection.cs
├── Abstractions/Persistence/IAppDbContext.cs
├── Errors/                        # ErrorOr-Error-Sammlungen je Bereich
├── Ports/                         # externe Systeme + testbare Helfer
└── Features/
    └── <Bereich>/                 # Public / Platform / Admin
        └── <UseCase>/
            ├── <UseCase>Handler.cs
            ├── <UseCase>Request.cs           ← nur bei Eingabedaten
            ├── <UseCase>RequestValidator.cs  ← nur bei Validation
            └── <UseCase>Response.cs
```

- **Ein Handler pro Use-Case.** Keine generischen "Service"-Klassen.
- **Request/Response liegen im selben Ordner** wie der Handler.
- **Command-Records** (`<UseCase>Command`) ergänzen das Bild, wenn der
  Handler Eingabedaten + Identity aus dem Endpoint zu einem internen
  Parameter bündelt.

## Handler-Aufbau

**Primary Constructors sind Pflicht.** Handler sind
`public sealed class XHandler(IAppDbContext db, ILogger<XHandler> logger, ...)`
auf einer Zeile. Die alte Variante mit zwei Konstruktoren (public + internal)
ist tabu — Determinismus für Tests kommt über **Port-Interfaces** und Moq.

```csharp
public sealed class CreateXHandler(
    IAppDbContext db,
    TimeProvider clock,
    ILogger<CreateXHandler> logger)
{
    public async Task<ErrorOr<XResponse>> HandleAsync(
        XRequest request,
        CancellationToken cancellationToken)
    {
        // 1. Identity ist bereits im Command/Request (vom Endpoint)
        // 2. Domänenobjekt über Create(...) anlegen
        // 3. DbContext-Mutation + SaveChangesAsync
        // 4. ChangeLog-Eintrag (db.ChangeLogs.Add(...)) — sofern nicht übersprungen
        // 5. Response als ErrorOr<T> zurückgeben
    }
}
```

### Leere Requests weglassen

Endpunkte ohne Body (z. B. `POST /admin/invitations` mit Aktion
„generiere Code") bekommen **keine** Request-Klasse. Der Handler nimmt
nur `CancellationToken` und es gibt keinen Validator:

```csharp
public Task<ErrorOr<Response>> HandleAsync(CancellationToken cancellationToken);
```

### Identity-Threading

Identity kommt **nicht** über `auth.GetCurrentUserId()` innerhalb des
Handlers. Das `sub`-Claim wird im Endpoint via
`ClaimsPrincipal.GetUserId()` extrahiert und in den Command geschrieben
(siehe `Api-instructions.md` → Auth). Handler nehmen die UserId als
Command-Feld entgegen.

### Transaktionen

`await db.SaveChangesAsync(ct)` reicht — EF Core kapselt das in einer
Transaktion. Mehrere logische Schreibschritte im selben Handler sind
dadurch atomar. Explizite `BeginTransactionAsync`-Aufrufe sind YAGNI,
bis ein Use-Case sie tatsächlich braucht.

### Handler explizit registrieren

`DependencyInjection.cs` ruft pro Handler ein explizites
`services.AddScoped<…Handler>()` auf — keine Reflection-basierte
Auto-Registrierung. Tippfehler und vergessene Handler fallen so schon
beim Build auf:

```csharp
services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
services.AddScoped<CreateInvitationHandler>();
services.AddScoped<SignUpHandler>();
// ...
```

Validierer registrieren sich automatisch via
`services.AddValidatorsFromAssembly(...)`.

### Kollisionen / Unique-Constraints

Vertraut die Domain auf eine `UNIQUE`-Constraint (alle
Eindeutigkeitsregeln sollten in der DB sitzen), ist ein `AnyAsync`-Pre-Check
**nicht** ausreichend — TOCTOU-Race zwischen zwei parallelen Requests.
Stattdessen:

```csharp
try { await db.SaveChangesAsync(cancellationToken); }
catch (DbUpdateException) { /* Tx ist bereits zurückgerollt */ }
```

Andere `DbUpdateException`-Ursachen werden hochpropagiert und in Tests
gegen eine echte PG-Instanz verifiziert.

## ErrorOr-Pattern

- Handler liefern **`ErrorOr<TResponse>`** zurück. Businessfehler
  werden als `Error`-Instanzen transportiert, nicht als Exceptions.
- Exceptions sind echten Infrastrukturfehlern vorbehalten (DB down,
  Netzwerk weg).

Fehlersammlungen leben unter `KulturHub.Application/Errors/` und heißen
`<BoundedContext>Errors` oder `<Entity>Errors` (`public static`).
`Error.Validation(...)` ist auch hier erlaubt für fachliche
Eindeutigkeitsprüfungen, die das FluentValidation-Setup nicht abdeckt.

```csharp
public static class InvitationErrors
{
    public static readonly Error CodeGenerationFailed = Error.Conflict(
        "Invitation.CodeGenerationFailed",
        "Could not generate a unique invitation code after multiple attempts.");
}
```

Domain-Validation-Errors (`<Entity>ValidationErrors`) und
Application-Handler-Errors (`<Entity>Errors`) sind **bewusst** zwei
verschiedene Klassen — siehe `Domain-instructions.md`.

## Validierung

- **FluentValidation** mit `AbstractValidator<TRequest>`.
- Validator-Dateien heißen `<Request>Validator.cs` und werden **nur**
  angelegt, wenn der Request Eingabedaten hat.
- Validatoren prüfen **Form & Shape** — nicht Fachlichkeit.

| Gehört in den Validator | Gehört **nicht** in den Validator |
|---|---|
| `NotEmpty`, `EmailAddress`, `Matches(pattern)` | Eindeutigkeit gegen DB (Handler) |
| `MinimumLength`, `MaximumLength` (Domain-Konstante!) | Existenz/Status von Aggregaten (Handler) |
| Strukturelle Konsistenz (`EndDate > StartDate`) | Domain-Invarianten (UTC etc.) |
| | Auth/Authorization (Filter / `RequireAuthorization()`) |

**Konstanten-Quelle:** Pattern und Längen kommen aus dem Domain-Layer
(z. B. `Organisation.MaxNameLength`, `InvitationCodeSpecs.Pattern`).
Validator und Domain-Factory prüfen denselben Wert — wer eine Regel
ändert, ändert sie an genau einer Stelle.

## Logging

- **Strukturiertes Logging** via `ILogger<T>`. Keine `Console.WriteLine`.
- **Keine PII** loggen (E-Mail, Klarname, Token, eingelöste Codes).
- `BeginScope` für Korrelations-IDs ist erlaubt.
- Default-Provider (Console + Debug + EventSource) registriert
  `WebApplication.CreateBuilder(...)` automatisch. Weitere Senken
  (Serilog, OTel) werden in `Program.cs` ergänzt, nicht in Handlern.

## Was hier **nicht** hineingehört

- Keine direkten DbContext-Implementierungen
- Keine Migrations oder `OnModelCreating`-Logik
- Keine HTTP-Typen (`HttpContext`, `IFormFile`, Action Results)
- Keine JSON-Serialisierung
- Keine zwei Konstruktoren (public + internal) zur Test-Injection
- Keine leeren `Request`-Klassen für Endpoints ohne Body
