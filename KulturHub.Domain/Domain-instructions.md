# KulturHub.Domain

Die Domain enthält die fachlichen Modelle und Regeln.

## Enthält

- **Entities** – POCO-Klassen, die das Kernmodell abbilden
- **Fachliche Logik** – Methoden, die Geschäftsregeln durchsetzen
- **Business-Regeln** – Invariants, die in den Entitäten selbst geprüft werden

## Designprinzipien

### Purity

Die Domain hat **keine** Abhängigkeiten auf Frameworks, Datenbanken,
Auth-Libraries oder externe Dienste. Sie referenziert ausschließlich das
.NET-Standard-API.

### Keine EF-Core-Attribute

Entities tragen keine `[Table]`, `[Column]`, `[Key]` oder
`[DatabaseGenerated]`-Attribute. Das Mapping wird ausschließlich in der
Infrastructure über `IEntityTypeConfiguration<T>` definiert.

### Keine öffentlichen Setter

Properties haben private Setter. Öffentliche `set`-Methoden werden
vermieden, damit Invariants nicht von außen umgangen werden können.

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

## Was hier **nicht** hineingehört

- Keine Repository-Interfaces
- Keine Validators (leben im Application-Layer als FluentValidation)
- Keine DTOs oder Request-/Response-Modelle
- Keine EF-Core-, Datenbank- oder Auth-Bezüge
