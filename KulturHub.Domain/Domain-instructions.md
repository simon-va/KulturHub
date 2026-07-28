# KulturHub.Domain

Die Domain enthält die fachlichen Modelle und Regeln.

## Enthält

- **Entities** – POCO-Klassen, die das Kernmodell abbilden
- **Fachliche Logik** – Methoden, die Geschäftsregeln durchsetzen
- **Business-Regeln** – Invariants, die in den Entitäten selbst geprüft werden
- **Domain-Validierungsfehler** – `Error`-Instanzen, die von Entity-Factories zurückgegeben werden

## Designprinzipien

### Purity

Die Domain hat **keine** Abhängigkeiten auf Frameworks, Datenbanken,
Auth-Libraries oder externe Dienste. Sie referenziert ausschließlich das
.NET-Standard-API und das **`ErrorOr`-Paket** (für `ErrorOr<T>` und `Error`).

### Keine EF-Core-Attribute

Entities tragen keine `[Table]`, `[Column]`, `[Key]` oder
`[DatabaseGenerated]`-Attribute. Das Mapping wird ausschließlich in der
Infrastructure über `IEntityTypeConfiguration<T>` definiert.

### Keine öffentlichen Setter

Properties haben private Setter. Öffentliche `set`-Methoden werden
vermeiden, damit Invariants nicht von außen umgangen werden können.

## Factory-Pattern

Jede Entity besitzt zwei Factory-Methoden:

- **`Create(...)`** – der reguläre Weg, eine neue Entität anzulegen.
  - Setzt die vom System vergebenen Felder (`Id`, `CreatedAt`, `CreatedBy`).
  - Validiert Pflichtfelder und Invariants.
  - Gibt bei Validierungsfehlern `ErrorOr<T>` zurück oder wirft eine
    `DomainException` für harte, nicht behebbare Fehler.

- **`Reconstitute(...)`** – wird ausschließlich vom Infrastructure-Layer
  genutzt, um eine Entity aus der Datenbank wiederherzustellen.
  - Setzt alle Properties ohne Validierung.
  - Wird in `IEntityTypeConfiguration<T>`-Klassen oder im DbContext-Ladevorgang
    verwendet, wenn Reflection-basierte Mapper nicht ausreichen.
  - **Niemals** in Handlern oder Tests aufrufen.

**YAGNI-Hinweis:** `Reconstitute(...)` wird nur implementiert, wenn EF Core es
tatsächlich braucht. Bei aktueller Konfiguration (siehe
[`KulturHub.Infrastructure/Infrastructure-instructions.md`](../../KulturHub.Infrastructure/Infrastructure-instructions.md))
ist `HasConversion` ausreichend — der Default-Konstruktor plus EF-Hydration
kommen ohne explizite Factory aus.

Beispiel-Signaturen:

```csharp
public static ErrorOr<Organisation> Create(
    string name,
    string description,
    UserId createdBy);

public static Organisation Reconstitute(
    OrganisationId id,
    string name,
    string description,
    UserId createdBy,
    DateTime createdAt,
    DateTime? updatedAt,
    UserId? updatedBy,
    bool isDeleted,
    DateTime? deletedAt);
```

## Soft-Delete-Felder

Jede veränderliche Entity besitzt:

```csharp
public bool IsDeleted { get; private set; }
public DateTime? DeletedAt { get; private set; }
```

- Löschen passiert über eine `Delete(UserId deletedBy)`-Methode, die
  `IsDeleted = true` und `DeletedAt = UtcNow` setzt.
- Reaktivieren erfolgt analog über `Restore()`.
- Das Filtern nach `IsDeleted` übernimmt EF Core per Global Query Filter
  im Infrastructure-Layer. Die Domain selbst kennt kein Filtern.

**YAGNI-Hinweis:** `Delete(...)` und `Restore()` werden **erst dann**
implementiert, wenn ein Endpoint sie auch tatsächlich aufruft. Aktuell
reichen `IsDeleted` und `DeletedAt` als Property, weil die EF-Configuration
den Global Query Filter auch ohne Domain-Methoden anwenden kann. Wird der
erste Delete-/Restore-Endpoint benötigt, kommen die Methoden mit passenden
Tests zurück.

## DateTime-Semantik

- **Alle `DateTime`-Werte sind UTC.**
- Bezeichner heißen konsequent `...At` (`CreatedAt`, `UpdatedAt`,
  `DeletedAt`, `PublishedAt`).
- `DateTimeKind.Utc` wird beim Setzen erzwungen; lokale Zeiten werden in
  Boundary-Code konvertiert (siehe `*-instructions.md` der API).

## IDs als strongly typed

IDs sind `readonly record struct`-Typen, die einen `Guid` wrappen:

```csharp
public readonly record struct OrganisationId(Guid Value)
{
    public static OrganisationId New() => new(Guid.NewGuid());
}
```

- Properties heißen `Id` (vom Typ `OrganisationId`, nicht `Guid`).
- Vergleiche laufen über die zugrundeliegenden `Guid`s, sind aber durch
  den Typ geschützt.

## Domain-Validierungsfehler

Validierungsfehler, die von Entity-Factories (`Create`, später auch
`Update`) zurückgegeben werden, leben in einer **eigenen Datei**:

```
KulturHub.Domain/
└── <BoundedContext>/
    ├── <Entity>.cs
    └── <Entity>ValidationErrors.cs   <-- hier
```

- Datei- und Klassenname folgen dem Muster `<Entity>ValidationErrors`,
  z. B. `InvitationValidationErrors`.
- Die Klasse ist **`internal static`**, weil sie nur von der Factory der
  eigenen Entity konsumiert wird.
- Die in `Application/Errors/` lebenden `Error`-Sammlungen sind bewusst
  separat — sie beschreiben Fehler aus dem Application-Handler (z. B.
  `Conflict`, `NotFound`), nicht aus der Domain-Validierung.

Beispiel:

```csharp
internal static class InvitationValidationErrors
{
    public static readonly Error CodeRequired =
        Error.Validation("Invitation.CodeRequired", "Code is required.");
}
```

## Validierungs-Schichten

Validierung lebt in **zwei** Schichten, mit klarer Trennung.

### Domain (`Create`-Factories)

- Prüft **echte Invarianten**, die das Aggregat unabhängig vom Aufrufer
  garantieren muss.
- Beispiele: UTC für `CreatedAt`/`JoinedAt`, `ExpiresAt > CreatedAt`,
  Format von intern erzeugten Codes, falls die Factory sie annimmt.
- Antwort: `ErrorOr<T>` mit `Error.Validation(...)` aus der
  `<Entity>ValidationErrors`-Klasse.

### Application (`*RequestValidator`)

- Prüft **Form & Shape** der eingehenden API-Requests.
- Beispiele: `NotEmpty`, Längen (referenziert Domain-Konstanten),
  Patterns.
- Antwort: `ValidationResult` → 400 mit Property-Map im Endpoint-Filter.

### Was nicht doppelt geprüft werden muss

Shape-Checks (NotEmpty, Längen, Email-Format) **dürfen** sowohl im
Validator als auch in der Domain-Factory stehen — als
**Defense-in-Depth**, damit die Factory auch dann korrekt arbeitet,
wenn sie aus Tests oder interner Logik direkt aufgerufen wird. In
diesem Fall gilt:

- **Konstante im Domain, Referenz im Validator.** Keine Magic-Numbers.
- Domain-Check prüft denselben Wert, den der Validator bereits
  durchgesetzt hat. Wer eine Regel ändert, ändert sie an genau einer
  Stelle.

### Was nur in eine Schicht gehört

| Regel | Domain | Validator |
|---|---|---|
| Aggregat-Invariante (UTC, `ExpiresAt > CreatedAt`) | ✅ | ❌ (Daten kommen vom Server) |
| Eindeutigkeit gegen DB | ❌ (Handler) | ❌ (Handler) |
| Shape des Requests (Email-Format, Längen) | optional | ✅ |

## Was hier **nicht** hineingehört

- Keine Repository-Interfaces
- Keine Validators (leben im Application-Layer als FluentValidation)
- Keine DTOs oder Request-/Response-Modelle
- Keine EF-Core-, Datenbank- oder Auth-Bezüge
- Keine doppelten `Error`-Klassen — Domain-Errors heißen
  `<Entity>ValidationErrors`, Application-Errors heißen `<BoundedContext>Errors`
  oder `<Entity>Errors`.
