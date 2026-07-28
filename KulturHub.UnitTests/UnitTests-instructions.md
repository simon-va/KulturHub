# KulturHub.UnitTests

Isolierte Tests für Handler, Validatoren und Domänenregeln.

## Test-Stack

- **xUnit** als Test-Runner
- **Moq** für Interface-Stubs (z. B. `IInvitationCodeGenerator`,
  `TimeProvider`, später `IAuthProvider`)
- **FluentAssertions** für lesbare Asserts
- **EF Core InMemory-Provider** (`UseInMemoryDatabase`) oder
  **`Microsoft.EntityFrameworkCore.InMemory`** zum Testen des
  `AppDbContext`. `InMemory` ist in Ordnung, wenn keine
  datenbankspezifischen Features genutzt werden; sonst lieber
  SQLite-in-Memory.

## Namenskonventionen

- Testordner spiegeln das zu testende Projekt:

```
KulturHub.UnitTests/
├── Features/
│   └── <Bereich>/                   # Domain / Application / Api / Infrastructure
│       └── <UseCase>/
│           ├── <Handler>Tests.cs
│           └── <Request>ValidatorTests.cs       ← nur wenn Validator existiert
└── TestHelpers/                     # projektweite Mocks und Factories
```

- Pro Handler eine eigene Testklasse: `<HandlerName>Tests.cs`.
- Testmethode: `MethodName_Scenario_ExpectedResult`.

Beispiele:

```csharp
Handle_WhenNameIsEmpty_ShouldReturnValidationError
Handle_WhenOrganisationNotFound_ShouldReturnNotFound
Validate_WhenRequestIsValid_ShouldNotHaveErrors
```

## Pflicht-Testfälle pro Handler

- **Happy Path** – gültiger Request → erwartete Response.
- **Validierungsfehler** – invalider Request → passender `Error.Validation`.
- **Nicht gefunden** – Entity existiert nicht → `Error.NotFound`.
- **Konflikte** – Eindeutigkeitsverletzungen → `Error.Conflict`.
- **Autorisierung** – falls relevant: falscher User → `Error.Forbidden`.
- **Soft Delete** – gelöschte Entity darf nicht zurückgegeben werden.

## AAA-Struktur

Jeder Test folgt Arrange–Act–Assert. Aussagekräftige Variablennamen
(`sut` für System-under-Test, `expected`, `actual`) sind erwünscht.

```csharp
[Fact]
public async Task Handle_WhenRequestIsValid_ShouldCreateOrganisation()
{
    // Arrange
    var db = CreateInMemoryDbContext();
    var auth = MockAuthProvider.WithUser(TestUsers.Admin);
    var sut = new CreateOrganisationHandler(db, auth, TimeProvider.System, NullLogger<CreateOrganisationHandler>.Instance);

    // Act
    var result = await sut.HandleAsync(new CreateOrganisationRequest("Verein", "Beschreibung"), CancellationToken.None);

    // Assert
    result.IsError.Should().BeFalse();
    result.Value.Name.Should().Be("Verein");
}
```

## Test-Fixtures und Mocks

- Test-Daten und -Mocks liegen im jeweiligen Testordner oder in einer
  `TestHelpers/`-Klasse, wenn sie projektweit geteilt werden.
- `Mock.Of<T>()` für einfache Stubs, `new Mock<T>()` für Assertions
  auf Interaktionen.
- `TimeProvider` wird mit `FakeTimeProvider` aus
  `Microsoft.Extensions.TimeProvider.Testing` für deterministische
  Zeitstempel verwendet.

### Ports mocken statt Konstruktor-Tricks

Wenn der Handler einen **Port** injiziert bekommt (z. B.
`IInvitationCodeGenerator`), wird der Test den Port per Moq
stubben — **nicht** den Konstruktor des Handlers per Reflection oder
`InternalsVisibleTo` öffnen.

```csharp
var queue = new Queue<string>(new[] { "AAA-BCD", "DEF-GHJ" });
var generator = new Mock<IInvitationCodeGenerator>();
generator.Setup(g => g.Generate())
         .Returns(() => queue.Count > 0 ? queue.Dequeue() : throw new InvalidOperationException("Generator exhausted."));
```

Wenn die Sequenz erschöpft ist, schlägt der Test mit einer klaren
Exception fehl (statt ungewollt mit `string.Empty` zu arbeiten).
So werden Endlos-Loops in der Implementierung früh sichtbar.

### Test-Helfer `CreateSut` mit Sequenz-Input

Einheitliches Muster für Handler mit geseeded Daten und gestubbten
Ports:

```csharp
private static (THandler Sut, TDbContext Db, Mock<TPort> Port) CreateSut(
    IEnumerable<string> codesToReturn,
    IEnumerable<DomainEntity> seed)
{
    var db = TestDbContextFactory.CreateInMemory();
    db.AddRange(seed);
    db.SaveChanges();

    var queue = new Queue<string>(codesToReturn);
    var port = new Mock<TPort>();
    port.Setup(p => p.Generate()).Returns(() => /* queue or throw */);

    var handler = new THandler(db, port.Object, /* ... */);
    return (handler, db, port);
}
```

**Ein** `CreateSut`-Helfer pro Testklasse. Keine zwei Varianten für
„String" und „Queue"; `IEnumerable<string>` deckt beide Fälle ab.

### Seed-Daten konsistent speichern

`db.AddRange(seed); db.SaveChanges();` einmal im `CreateSut` reicht.
Im Test selbst **kein** weiteres `SaveChangesAsync` — die Daten sind
schon persistiert. Das verhindert Inkonsistenzen zwischen Tests, die
synchron `SaveChanges()` und welche, die `SaveChangesAsync()` aufrufen.

## EF Core in Tests

- Pro Test eine eigene In-Memory-Datenbank mit eindeutigem Namen
  (`Guid.NewGuid().ToString()`), damit Tests sich nicht gegenseitig
  beeinflussen.
- `SaveChangesAsync` in Tests **ohne** `BeginTransaction` – jede
  Aktion ist atomar.
- Global Query Filter (`!IsDeleted`) wird auch in In-Memory-Tests
  angewandt – das ist erwünscht, weil es die Produktionslogik spiegelt.
- Wenn `IgnoreQueryFilters()`-Pfade getestet werden, ist das im
  Testnamen explizit zu kennzeichnen.
- In-Memory-Provider **kennt keine Unique-Constraints** — Tests, die
  den `DbUpdateException`-Retry-Pfad absichern, müssen gegen eine
  echte Postgres-Instanz laufen (Integrationstest, separate Suite).

## Validierungs-Tests

- Validatoren werden direkt mit `IValidator<T>.ValidateAsync(request)`
  getestet – ohne HTTP.
- Assert über `TestValidationResult`-Helfer von FluentValidation oder
  direkt über `result.IsValid` und `result.Errors`.
- Wenn ein Endpoint **keinen Validator hat**, gibt es auch keinen
  `<Request>ValidatorTests.cs`. Endlosschleife-Check: stellt sicher,
  dass ein versehentlich angelegter Validator auch wirklich
  ausgeführt wird.

## Was hier **nicht** hineingehört

- Keine echte Datenbankverbindung (Ausnahme: dedizierter
  Integrationstest für DB-spezifische Features wie Constraints)
- Keine HTTP-Tests (separates Integration-Test-Projekt, nicht Teil
  dieser Anleitung)
- Keine produktiven `appsettings.json`-Werte
- Keine Reflection-Tricks oder `InternalsVisibleTo`-Konstrukte — wenn
  Test-Determinismus nötig ist, wird ein Port-Interface gemockt
- Keine leeren `Request`-Tests, wenn der Handler keinen Request nimmt
- Keine zwei parallelen `CreateSut`-Helfer (z. B. „String" vs.
  „Queue") — eine Helper-Methode pro Testklasse reicht
