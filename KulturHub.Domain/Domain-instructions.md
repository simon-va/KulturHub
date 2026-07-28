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

Properties haben `private set` oder sind immutable. Öffentliche
`set`-Methoden werden vermieden, damit Invariants nicht von außen
umgangen werden können.

## Factory-Pattern

Jede Entity besitzt mindestens eine `Create(...)`-Factory, die als
`private constructor` + `public static ErrorOr<T> Create(...)` umgesetzt
ist:

- Setzt die vom System vergebenen Felder (`Id`, `CreatedAt`).
- Validiert Pflichtfelder und Invariants.
- Gibt bei Validierungsfehlern `ErrorOr<T>` zurück oder wirft eine
  `DomainException` für harte, nicht behebbare Fehler.

Hydration aus der Datenbank läuft über `HasConversion(id => id.Value, v => new XxxId(v))`
plus privatem Konstruktor — eine separate `Reconstitute`-Factory wird
**nicht** benötigt.

Beispiel:

```csharp
public static ErrorOr<Organisation> Create(string name, TimeProvider clock)
{
    if (string.IsNullOrWhiteSpace(name))
        return OrganisationValidationErrors.NameRequired;
    // ...
    return new Organisation(/* ... */);
}
```

## Soft-Delete-Felder

Veränderliche Entities tragen:

```csharp
public bool IsDeleted { get; private set; }
public DateTime? DeletedAt { get; private set; }
```

EF Core blendet sie per Global Query Filter in Infrastructure aus
(siehe `Infrastructure-instructions.md`). `Delete(...)` / `Restore()`-
Methoden werden on-demand ergänzt, sobald ein Endpoint sie braucht
(`Membership.Delete` ist aktuell der einzige Fall).

## DateTime-Semantik

- **Alle `DateTime`-Werte sind UTC.** Factories erzwingen
  `DateTimeKind.Utc` beim Setzen.
- Bezeichner heißen konsequent `...At` (`CreatedAt`, `UpdatedAt`,
  `DeletedAt`, `PublishedAt`).

## IDs als strongly typed

IDs sind `readonly record struct`-Typen, die einen `Guid` wrappen:

```csharp
public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());
    public static UserId From(Guid value) => new(value);
}
```

- Properties heißen `Id` (vom Typ `XxxId`, nicht `Guid`).
- Ausnahme: optionale Fremdschlüssel, die technisch kein eigener
  Aggregate-Typ sind, dürfen als `Guid?` bleiben — bis ein eigener
  `UserId?`-Bedarf entsteht.

## Domain-Validierungsfehler

Validierungsfehler, die von Entity-Factories zurückgegeben werden,
leben in einer **eigenen Datei** pro Entity:

```
KulturHub.Domain/
└── <BoundedContext>/
    ├── <Entity>.cs
    └── <Entity>ValidationErrors.cs   <-- hier
```

- Klasse ist **`internal static`**, weil sie nur von der Factory der
  eigenen Entity konsumiert wird.
- Fehler heißen immer `Error.Validation("Entity.RuleName", "...")`.

Beispiel:

```csharp
internal static class InvitationValidationErrors
{
    public static readonly Error CodeRequired =
        Error.Validation("Invitation.CodeRequired", "Code is required.");
}
```

Die in `Application/Errors/` lebenden `Error`-Sammlungen (`<X>Errors`)
sind bewusst separat — sie beschreiben Fehler aus dem
Application-Handler (`Conflict`, `NotFound`, etc.), nicht aus der
Domain-Validierung.

## Validierung: was hier passiert

Factories prüfen **echte Invariants** (UTC-Zeit, `ExpiresAt > CreatedAt`,
Code-Format, Pflichtfelder). Form & Shape des Requests (Längen,
Email-Format) lebt im `Application`-Layer als FluentValidation. Die
Konstante wird im Domain gehalten und vom Validator referenziert — so
gilt die Regel an genau einer Stelle.

## Was hier **nicht** hineingehört

- Keine Repository-Interfaces
- Keine Validators (leben im Application-Layer als FluentValidation)
- Keine DTOs oder Request-/Response-Modelle
- Keine EF-Core-, Datenbank- oder Auth-Bezüge
- Keine doppelten `Error`-Klassen für Application-Fehler
