# KulturHub.Application

Hier liegt die komplette Anwendungslogik.

## Enthält

- **Use-Case-Handler** – je Feature ein Handler mit `HandleAsync`
- **Daten laden und speichern** – über den injizierten `AppDbContext`
  (Application darf EF Core direkt nutzen)
- **Validieren** – FluentValidation-Validatoren pro Request (nur wenn
  der Request Eingabedaten hat)
- **Requests** – Eingabemodelle mit Validierungsregeln (nur wenn nötig,
  siehe unten)
- **Responses** – Rückgabemodelle (Read-DTOs, Result-Payloads)
- **Interfaces für externe Systeme und testbare Domain-Helfer** – Ports-Ordner
- **DTOs** – Übergabeobjekte zwischen Handler und API

## Verschmelzung mit Infrastructure

Die Schichten verschmelzen pragmatisch. Konkret bedeutet das:

- **`AppDbContext` wird per Interface in Application injiziert** –
  typischerweise `IAppDbContext` aus dem Infrastructure-Layer oder ein
  im Application-Layer definiertes Interface, das der DbContext
  implementiert. So bleibt der direkte `DbSet<T>`-Zugriff erhalten, ohne
  dass der Application-Layer den konkreten Infrastructure-Typ kennt.
  Das Interface liegt zentral unter
  `KulturHub.Application/Abstractions/Persistence/IAppDbContext.cs`.
- **Application darf `IQueryable<T>` und LINQ-To-Entities** nutzen.
  Filter und Includes werden im Handler geschrieben.
- **Keine Repository-Abstraktion um jeden Preis.** Wenn eine kleine
  EF-Core-Abfrage direkt im Handler klarer ist, wird sie dort belassen.
  Nur wenn echte Wiederverwendung entsteht, kommt ein dediziertes
  Repository.
- **Ports** unter `KulturHub.Application/Ports/` bleiben **zwei** Zwecken
  vorbehalten:
  1. **Externe Systeme** (z. B. `IAuthProvider`, `IEmailSender`,
     `ISocialMediaPublisher`).
  2. **Testbare Domain-Helfer** (statische Generatoren, kryptografische
     Funktionen, Time-abhängige Berechnungen), die sonst nur durch
     Konstruktor-Tricks oder `InternalsVisibleTo` testbar wären.
  Ports haben **nichts** mit Datenzugriff zu tun.

## Ordnerstruktur

```
KulturHub.Application/
├── DependencyInjection.cs
├── Abstractions/                      # generische Ports zur Außenwelt
│   └── Persistence/
│       └── IAppDbContext.cs
├── Errors/                            # ErrorOr-Error-Sammlungen je Bereich
│   ├── AuthErrors.cs
│   └── InvitationErrors.cs
├── Ports/                             # externe Systeme + testbare Helfer
│   ├── IAuthProvider.cs
│   └── IInvitationCodeGenerator.cs
└── Features/
    └── <Bereich>/                     # Public / Platform / Admin
        └── <UseCase>/
            ├── <UseCase>Handler.cs
            ├── <UseCase>Request.cs    ← nur wenn der Request Daten hat
            ├── <UseCase>RequestValidator.cs   ← nur wenn Validation nötig
            └── <UseCase>Response.cs
```

- **Ein Handler pro Use-Case.** Keine generischen "Service"-Klassen.
- **Request/Response liegen im selben Ordner** wie der Handler.
- **Bereichsordner** spiegeln die API-Dokumente (`Public`, `Platform`,
  `Admin`) – nicht zwingend, aber empfohlen für Übersichtlichkeit.

## Handler-Aufbau

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
        // 1. Validierung (manuell oder via Validator – Validator läuft im Endpoint)
        // 2. Domänenobjekt erzeugen
        // 3. DbContext-Mutation + SaveChangesAsync
        // 4. Change-Log-Eintrag schreiben (sofern nicht explizit übersprungen)
        // 5. Response bauen
    }
}
```

**Verwende Primary Constructors.** Die alte Variante mit zwei
Konstruktoren (public + internal) ist tabu — wenn ein Parameter für
Tests deterministisch gesetzt werden muss (z. B. ein Code-Generator),
wird er als **Port-Interface** injiziert (siehe
`KulturHub.Application/Ports/IInvitationCodeGenerator.cs`) und in
Tests per Moq gestubbt.

### Leere Requests weglassen

Wenn ein Endpoint keinen Request-Body hat (z. B. `POST` ohne Eingabe oder
reine Aktionen wie „generiere einen Code"), wird **keine**
`<UseCase>Request`-Klasse angelegt. Der Handler nimmt dann ausschließlich
`CancellationToken` entgegen:

```csharp
public Task<ErrorOr<Response>> HandleAsync(CancellationToken cancellationToken);
```

Analog entfallen `RequestValidator.cs` und `AddValidationFilter<>` im Endpoint.

### Pflichtschritte pro Handler

1. **Identity aus dem Auth-Provider** holen (`auth.GetCurrentUserId()`).
2. **Domänenobjekt** über die `Create(...)`-Factory der Entity anlegen.
3. **Persistenz** über den `DbContext`. `SaveChangesAsync` wird pro
   Handler einmal am Ende aufgerufen, mehrere Schreibvorgänge werden in
   einer Transaktion zusammengefasst (Standard: implizite Transaktion
   über `SaveChangesAsync`).
4. **Change-Log-Eintrag** schreiben, sofern die Aktion nicht rein lesend
   ist und nicht explizit übersprungen wurde (z. B. Admin-Aktionen ohne
   Organisationsbezug). Idempotenz: Log-Einträge gehen über den
   `IChangeLogWriter`-Port (siehe Infrastructure-Implementierung).
5. **Response** als `ErrorOr<TResponse>` zurückgeben.

### Handler explizit registrieren

Jeder Handler, den ein Endpoint per `[FromServices]` aus dem
DI-Container anfordert, muss in `AddApplication(...)` explizit per
`services.AddScoped<…Handler>()` registriert werden:

```csharp
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
    services.AddScoped<CreateInvitationHandler>();
    return services;
}
```

Mit jedem neuen Use-Case wächst die Liste um eine `AddScoped<…Handler>()`
pro Handler. Reflection-basierte Auto-Registrierung
(z. B. `services.AddScoped(Assembly.GetTypes().Where(...))`) wurde
bewusst verworfen — sie gibt die Compile-Zeit-Sicherheit auf, weil
Tippfehler und unregistrierte Handler erst zur Laufzeit als
`InvalidOperationException` aus dem DI-Container auffallen, nicht
bereits beim Build. Die explizite Liste macht jede Änderung am
Handler-Bestand sichtbar.

Scoped-Lebensdauer passt zur scoped `IAppDbContext`: ein Handler pro
Request, `DbContext` für genau diesen Handler.

### Transaktionen

- Innerhalb eines Handlers reicht `await db.SaveChangesAsync(ct)` – EF
  Core kapselt das in einer Transaktion.
- **Mehrere logische Schreibschritte, die zusammen committed werden
  müssen** (z. B. „Membership anlegen + Change Log schreiben"), werden
  explizit über `db.Database.BeginTransactionAsync(ct)` umschlossen. Der
  Commit erfolgt im `try`, Rollback im `catch`.
- `IDbContextTransaction` wird **nicht** über mehrere Handler geteilt.

### Kollisionen / Unique-Constraints

Wann immer die Domain auf eine `UNIQUE`-Constraint vertraut
(Empfehlung: alle Eindeutigkeitsregeln in der DB umsetzen), ist der
Pre-Check über `AnyAsync` **nicht** ausreichend. Es bleibt eine
TOCTOU-Race zwischen zwei parallelen Requests.

Beispiel — kollidierende `InvitationCode`-Generierung:

```csharp
try
{
    await db.SaveChangesAsync(cancellationToken);
}
catch (DbUpdateException)
{
    continue;   // sicher: Tx ist bereits zurückgerollt, der Code ist nicht gespeichert
}
```

Vor dem Erstellen `CodeExistsAsync(...)` aufzurufen ist redundant —
Postgres hat die Transaktion verworfen, der Code existiert garantiert
nicht (sonst hätte der Insert geklappt). Andere `DbUpdateException`-
Ursachen (Connection-Loss, Disk-Full etc.) sollen weiter hochpropagiert
werden und werden in Tests gegen eine echte PG-Instanz verifiziert.

### ErrorOr-Pattern

- Handler liefern **`ErrorOr<TResponse>`** zurück. Businessfehler werden
  als `Error`-Instanzen transportiert, nicht als Exceptions.
- Exceptions sind echten Infrastrukturfehlern vorbehalten (DB down,
  Netzwerk weg).
- Fehlertypen leben unter `KulturHub.Application/Errors/`:

```csharp
public static class InvitationErrors
{
    public static readonly Error CodeGenerationFailed = Error.Conflict(
        "Invitation.CodeGenerationFailed",
        "Could not generate a unique invitation code after multiple attempts.");
}
```

- `Error.Validation(...)` für Eingabefehler, die das
  FluentValidation-Setup nicht abdeckt (z. B. fachliche
  Eindeutigkeitsprüfungen gegen die DB).

### Naming der Error-Klassen

- **Domain** hat `<Entity>ValidationErrors` (`internal static`,
  ausschließlich `Error.Validation(...)`-Instanzen aus Factories).
- **Application** hat `<BoundedContext>Errors` oder `<Entity>Errors`
  (`public static`, alle anderen `ErrorType`s).

`InvitationValidationErrors` (Domain) und `InvitationErrors`
(Application) sind **bewusst** zwei verschiedene Klassen.

## Ports für Testbarkeit

Wenn eine Domain-Klasse eine Abhängigkeit hat, die im Test **deterministisch**
sein muss (kryptografische Quellen, Zufallsgeneratoren, Zeit) und nicht
intern über `TimeProvider` o. Ä. abstrahiert wird, lege ein schmales
Port-Interface in `Ports/` an:

```csharp
public interface IInvitationCodeGenerator
{
    string Generate();
}
```

- Konkrete Implementierung lebt im **Infrastructure-Projekt** (z. B.
  `Infrastructure/Invitations/InvitationCodeGeneratorAdapter.cs`).
- Registrierung in `AddInfrastructure(...)` per `AddSingleton<>`.
- Tests mocken den Port mit Moq statt `InternalsVisibleTo` zu nutzen
  oder Reflection auf private Methoden anzuwenden.

## Validierung

- **FluentValidation** mit `AbstractValidator<TRequest>`.
- Validator-Dateien heißen `<Request>Validator.cs` und werden **nur**
  angelegt, wenn der Request Eingabedaten hat.
- Validatoren registrieren sich automatisch via
  `services.AddValidatorsFromAssembly(...)` in
  `DependencyInjection.cs`.
- Die API ruft `IValidator<T>.ValidateAsync(request)` im Endpoint-Filter
  auf; nur dann wird die Pipeline aufgerufen, wenn `IsValid`.

## Logging

- **Strukturiertes Logging** via `ILogger<T>`. Keine `Console.WriteLine`.
- **Keine PII loggen** (E-Mail-Adressen, Klarnamen, Tokens,
  eingelöste Invitation-Codes).
- Log-Scope mit `BeginScope` für Korrelations-IDs ist erlaubt.
- Default-Provider (Console + Debug + EventSource) werden von
  `WebApplication.CreateBuilder(...)` registriert. Logs landen in
  `stdout`/`stderr` des Prozesses, also im Terminal bzw. Rider-Run-Fenster.
  Andere Senken (Serilog, OTel) sind Erweiterungen und kommen in
  `Program.cs` dazu, ohne dass Handler angepasst werden müssen.

## Was hier **nicht** hineingehört

- Keine direkten DbContext-Implementierungen
- Keine Migrations oder `OnModelCreating`-Logik
- Keine HTTP-Typen (`HttpContext`, `IFormFile`, Action Results)
- Keine JSON-Serialisierung
- Keine zwei Konstruktoren (public + internal) zur Test-Injection —
  stattdessen Port-Interfaces und Moq
- Keine „leeren" Request-Klassen für Endpoints ohne Body
