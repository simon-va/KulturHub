# KulturHub.Domain

Konventionen für die Domänenschicht. Hier liegt das pure Geschäftsmodell –
**keine** Framework-, Persistenz- oder Transport-Abhängigkeiten.

## Zweck

POCO-Entities und Enums, die unabhängig von Datenbank, HTTP und externen
Diensten gelten. Diese Schicht besitzt die Invarianten des Modells.

## Konventionen

- **Datei pro Entity** unter `KulturHub.Domain/Entities/`, flache Struktur –
  keine Unterordner.
- **Namespace** einheitlich `KulturHub.Domain.Entities` per
  file-scoped `namespace` (`KulturHub.Domain/Entities/Organisation.cs:1`).
- **Alle Properties** sind `public … { get; private set; }` – nichts ist
  öffentlich setzbar (`KulturHub.Domain/Entities/Organisation.cs:7-11`).
- **Privater parameterloser Konstruktor** für ORM-Hydration
  (`Organisation.cs:13`, `Membership.cs:14`, `Invitation.cs:28`,
  `ChangeLog.cs:12`). **Ausnahme**: `User` hat bewusst keinen – das
  Repository materialisiert `User` nicht aus der DB.
- **String-Felder** werden mit `null!` oder `string.Empty` initialisiert, um
  den Nullable-Vertrag ohne semantischen Default zu erfüllen
  (`Organisation.cs:8`, `User.cs:6-8`).
- **Konstanten** gehören als `private const` direkt in die Entity, sofern
  sie nicht außerhalb der Entity benötigt werden
  (`Organisation.MaxNameLength`, `Invitation.CodeAlphabet`,
  `Invitation.CodePattern`). Werte, die auch außerhalb der Domain
  gebraucht werden (z. B. `InvitationCodeRules.CodePattern`), gehören
  in eine eigene Regel-Klasse im Application-Layer und werden in der
  Domain als `private const` dupliziert.
- **Guard-Klauseln** im Stil `if (...) throw new ArgumentException(...)` oder
  `InvalidOperationException(...)`. Es wird **kein** `Guard.Against…`-Library
  und **kein** `Result`-Typ verwendet.
- **Zeitstempel** ausschließlich `DateTime` mit `DateTimeKind.Utc` –
  niemals `DateTimeOffset`, niemals `DateTime.Now`.

## Patterns

- **Zwei Factory-Methoden** pro Entity, die DB round-trip-fähig ist:
  `static X Create(...)` erzeugt neue Aggregate mit `Guid.NewGuid()` und
  `CreatedAt = DateTime.UtcNow` (`Organisation.cs:15-30`,
  `Membership.cs:16-36`, `ChangeLog.cs:14-38`). `static X Reconstitute(...)`
  rekonstruiert aus allen Spalten inklusive Soft-Delete-Feldern
  (`Organisation.cs:53-65`, `Membership.cs:38-56`,
  `Invitation.cs:38-55`, `User.cs:22-38`, `ChangeLog.cs:40-54`).
- **Mutatoren re-validieren** Invariants. `Organisation.Rename` prüft Name
  erneut (`Organisation.cs:32-42`), `Membership.UpdateStatus` lässt
  Übergänge nur aus `Pending` zu (`Membership.cs:67-74`).
- **Idempotente Mutatoren** werfen, wenn sie doppelt aufgerufen werden –
  z. B. `Invitation.MarkAsUsed` / `MarkAsDeleted` (`Invitation.cs:57-72`),
  `Membership.MarkAsDeleted` (`Membership.cs:58-65`). Der Application-Layer
  prüft `entity.IsDeleted` **vor** dem Aufruf.
- **Enums** nutzen den kleinsten sinnvollen Backtyp: `MembershipStatus : short`
  mit expliziten Werten `0,1,2` (`MembershipStatus.cs:8-12`), passend zur
  `smallint`-Spalte.
- **Queryable Getter** statt Methodenaufrufe, wenn keine Aktion nötig ist –
  `Invitation.IsExpired`, `IsUsed`, `EnsureCanBeUsed()` (`Invitation.cs:20-26`).
- **`ChangeLog.Data`** ist bewusst `IReadOnlyDictionary<string, object?>`
  und wird als `jsonb` serialisiert (`ChangeLog.cs:9`).

## Pitfalls

- **Keine** Marker-Interfaces (`IEntity`, `IAuditableEntity`, `ISoftDeletable`)
  einführen – die Schicht ist konsistent vererbungsfrei.
- **Keine** `Result`-/ `ErrorOr`-Rückgaben aus Domain-Methoden. Domain
  wirft; die Application-Schicht übersetzt in `ErrorOr`.
- **Keine** `DeleteLogik` ohne Soft-Delete-Spalten. Soft-Delete ist
  Pflicht: `IsDeleted` + `DeletedAt` an jeder veränderlichen Entität.
- **Niemals** `DateTimeOffset` einführen. Immer `DateTime.UtcNow` + Mapping
  mit `DateTime.SpecifyKind(value, DateTimeKind.Utc)`.
- **Keine** Validierung in `User.Create` ergänzen – das spiegelnde Modell
  erwartet keine Invariants. Validierung passiert in der Application-Schicht
  (`SignUpInputValidator`).
- **Keine** Value Objects für `ChangeLog.Data` – bleibt absichtlich eine
  freie Map.

## AI-Workflow

1. **Lesen**: Vor jeder Änderung die Entity **komplett** lesen, inklusive
   `Create`/`Reconstitute`/Mutatoren/Konstanten.
2. **Regeln extrahieren**: Alle Invariants als Bullet-Liste dokumentieren
   (Name-Länge, Status-Übergänge, Idempotenz).
3. **Szenarien**: Happy Path + alle Verletzungen der Invariants.
4. **Implementieren**: Konstanten in der Entity halten, Mutatoren werfen
   lassen, kein `Result`-Typ im Domain.
5. **Verifizieren**: `dotnet build` + Tests in `KulturHub.UnitTests/Domain/`
   ergänzen bzw. erweitern.
