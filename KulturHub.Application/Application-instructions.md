# KulturHub.Application

Hier liegt die komplette Anwendungslogik.

## Enthält

- **Use-Case-Handler** – je Feature ein Handler mit `HandleAsync(Request)`
- **Daten laden und speichern** – über den injizierten `AppDbContext`
  (Application darf EF Core direkt nutzen)
- **Validieren** – FluentValidation-Validatoren pro Request
- **Requests** – Eingabemodelle mit Validierungsregeln
- **Responses** – Rückgabemodelle (Read-DTOs, Result-Payloads)
- **Interfaces für externe Systeme** – Ports-Ordner für Auth,
  E-Mail-Versand, externe APIs
- **DTOs** – Übergabeobjekte zwischen Handler und API

## Verschmelzung mit Infrastructure

Die Schichten verschmelzen pragmatisch. Konkret bedeutet das:

- **`AppDbContext` wird per Interface in Application injiziert** –
  typischerweise `IAppDbContext` aus dem Infrastructure-Layer oder ein
  im Application-Layer definiertes Interface, das der DbContext
  implementiert. So bleibt der direkte `DbSet<T>`-Zugriff erhalten, ohne
  dass der Application-Layer den konkreten Infrastructure-Typ kennt.
- **Application darf `IQueryable<T>` und LINQ-To-Entities** nutzen.
  Filter und Includes werden im Handler geschrieben.
- **Keine Repository-Abstraktion um jeden Preis.** Wenn eine kleine
  EF-Core-Abfrage direkt im Handler klarer ist, wird sie dort belassen.
  Nur wenn echte Wiederverwendung entsteht, kommt ein dediziertes
  Repository.
- **Ports** unter `KulturHub.Application/Ports/` bleiben externen
  Systemen vorbehalten (z. B. `IAuthProvider`, `IEmailSender`,
  `ISocialMediaPublisher`). Sie haben **nichts** mit Datenzugriff zu tun.

## Ordnerstruktur

```
KulturHub.Application/
├── DependencyInjection.cs
├── Errors/                  # ErrorOr-Error-Typen je Bereich
├── Ports/                   # Interfaces für externe Systeme
├── Rules/                   # domänenspezifische Helper (z. B. Slug, Zeit)
└── Features/
    └── <Bereich>/           # Public / Platform / Admin
        └── <UseCase>/
            ├── <UseCase>Handler.cs
            ├── <UseCase>Request.cs
            ├── <UseCase>RequestValidator.cs
            └── <UseCase>Response.cs
```

- **Ein Handler pro Use-Case.** Keine generischen "Service"-Klassen.
- **Request/Response liegen im selben Ordner** wie der Handler.
- **Bereichsordner** spiegeln die API-Dokumente (`Public`, `Platform`,
  `Admin`) – nicht zwingend, aber empfohlen für Übersichtlichkeit.

## Handler-Aufbau

```csharp
public sealed class CreateOrganisationHandler(
    IAppDbContext db,
    IAuthProvider auth,
    TimeProvider clock,
    ILogger<CreateOrganisationHandler> logger)
{
    public async Task<ErrorOr<CreateOrganisationResponse>> HandleAsync(
        CreateOrganisationRequest request,
        CancellationToken cancellationToken)
    {
        // 1. Validierung (manuell oder via Validator – Validator läuft im Endpoint)
        // 2. Domänenobjekt erzeugen
        // 3. DbContext-Mutation + SaveChangesAsync
        // 4. Change-Log-Eintrag schreiben
        // 5. Response bauen
    }
}
```

### Pflichtschritte pro Handler

1. **Identity aus dem Auth-Provider** holen (`auth.GetCurrentUserId()`).
2. **Domänenobjekt** über die `Create(...)`-Factory der Entity anlegen.
3. **Persistenz** über den `DbContext`. `SaveChangesAsync` wird pro
   Handler einmal am Ende aufgerufen, mehrere Schreibvorgänge werden in
   einer Transaktion zusammengefasst (Standard: implizite Transaktion
   über `SaveChangesAsync`).
4. **Change-Log-Eintrag** schreiben, sofern die Aktion nicht rein lesend
   ist. Idempotenz: Log-Einträge gehen über den `IChangeLogWriter`-Port
   (siehe Infrastructure-Implementierung).
5. **Response** als `ErrorOr<TResponse>` zurückgeben.

### Transaktionen

- Innerhalb eines Handlers reicht `await db.SaveChangesAsync(ct)` – EF
  Core kapselt das in einer Transaktion.
- **Mehrere logische Schreibschritte, die zusammen committed werden
  müssen** (z. B. „Membership anlegen + Change Log schreiben"), werden
  explizit über `db.Database.BeginTransactionAsync(ct)` umschlossen. Der
  Commit erfolgt im `try`, Rollback im `catch`.
- `IDbContextTransaction` wird **nicht** über mehrere Handler geteilt.

### ErrorOr-Pattern

- Handler liefern **`ErrorOr<TResponse>`** zurück. Businessfehler werden
  als `Error`-Instanzen transportiert, nicht als Exceptions.
- Exceptions sind echten Infrastrukturfehlern vorbehalten (DB down,
  Netzwerk weg).
- Fehlertypen leben unter `KulturHub.Application/Errors/`:

```csharp
public static class OrganisationErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Organisation.NotFound", "Organisation wurde nicht gefunden.");
    public static readonly Error NameTaken = Error.Conflict(
        "Organisation.NameTaken", "Eine Organisation mit diesem Namen existiert bereits.");
}
```

- `Error.Validation(...)` für Eingabefehler, die das
  FluentValidation-Setup nicht abdeckt (z. B. fachliche
  Eindeutigkeitsprüfungen gegen die DB).

## Validierung

- **FluentValidation** mit `AbstractValidator<TRequest>`.
- Validator-Dateien heißen `<Request>Validator.cs`.
- Validatoren registrieren sich automatisch via
  `services.AddValidatorsFromAssembly(...)` in
  `DependencyInjection.cs`.
- Die API ruft `IValidator<T>.ValidateAsync(request)` im Endpoint-Filter
  auf; nur dann wird die Pipeline aufgerufen, wenn `IsValid`.

## Logging

- **Strukturiertes Logging** via `ILogger<T>`. Keine `Console.WriteLine`.
- **Keine PII loggen** (E-Mail-Adressen, Klarnamen, Tokens).
- Log-Scope mit `BeginScope` für Korrelations-IDs ist erlaubt.

## Was hier **nicht** hineingehört

- Keine direkten DbContext-Implementierungen
- Keine Migrations oder `OnModelCreating`-Logik
- Keine HTTP-Typen (`HttpContext`, `IFormFile`, Action Results)
- Keine JSON-Serialisierung
