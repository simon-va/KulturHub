# KulturHub.UnitTests

Isolierte Tests für die Use-Case-Handler. Pro Handler wird **ein Test
pro Pfad** durch den Handler-Code geschrieben.

## Test-Stack

- **xUnit** als Test-Runner
- **Moq** für Interface-Stubs (z. B. `IInvitationCodeGenerator`,
  `IAuthProvider`, `IUserAdminClient`)
- **FluentAssertions** für lesbare Asserts
- **EF Core InMemory-Provider** (`UseInMemoryDatabase`) für den
  `AppDbContext`. In-Memory reicht, solange keine
  datenbankspezifischen Features nötig sind.

> **Hinweis:** `FluentValidation` wird im Test-Projekt **nicht**
> referenziert. Validatoren leben im Application-Layer und werden
> indirekt über den `HandleAsync`-Pfad des Handlers mitgetestet.
> `FluentValidation.TestHelper` o. Ä. ist hier bewusst nicht im Einsatz,
> damit keine parallelen Validator-Test-Suiten entstehen.

## Namenskonventionen

Testordner spiegeln das zu testende Projekt:

```
KulturHub.UnitTests/
├── Features/Application/<Bereich>/<UseCase>/<Handler>Tests.cs
└── TestHelpers/                      # projektweite Mocks und Factories
```

- Pro Handler eine eigene Testklasse: `<HandlerName>Tests.cs`.
- Testmethode: `Handle_<Scenario>_Should<ExpectedResult>`.

Beispiele:

```csharp
Handle_WhenNameIsEmpty_ShouldReturnValidationError
Handle_WhenOrganisationNotFound_ShouldReturnNotFound
Handle_WhenAllRetriesCollide_ShouldReturnCodeGenerationFailed
```

## Was hier getestet wird

Nur die einzelnen **Pfade durch den Handler-Code**. Ein Pfad ist jede
eindeutige Rückgabe bzw. jeder Seiteneffekt-Zweig (z. B.
`return InvitationErrors.NotFound`, persistierter User + markierte
Einladung, Rollback-Aufruf an den Admin-Client).

Pro Handler wird **genau ein Test pro Pfad** geschrieben. Nicht mehr,
nicht weniger:

- **Happy Path** – gültiger Request → erwartete Response und
  Seiteneffekte.
- **Jeder Fehlerpfad** – jeder `return Errors`-Zweig bekommt einen
  eigenen Test.
- **Jeder Kompensations-/Rollback-Pfad** – jeder Aufruf einer externen
  Kompensationsaktion bekommt einen eigenen Test.

### Was bewusst **nicht** getestet wird

- **Validatoren** (`SignUpRequestValidator`, etc.) — die
  Validierungslogik wird indirekt über den `HandleAsync`-Pfad des
  Handlers geprüft. Eigene `<Request>ValidatorTests.cs`-Dateien werden
  **nicht** angelegt.
- **Domain-Entities** (`User`, `Invitation`) und
  **Domain-Services** (`InvitationCodeGenerator`) – gehören in ein
  separates `KulturHub.Domain.Tests`-Projekt.
- **Doppelte Absicherung** desselben Pfads (mehrere
  `[Theory]`-Inline-Datasets für denselben Fehlerzweig) – ein Test pro
  Pfad reicht.

### Bewusste Ausnahmen

Im aktuellen Projekt liegen zwei Testdateien, die keine
Use-Case-Handler testen — sie sind als begründete Ausnahmen bewusst
hier, nicht in eigenen Test-Projekten:

- `Features/Application/Platform/Memberships/JsonSerialization/MembershipResponseJsonTests.cs`
  — DTO-JSON-Serialisierung (wäre andernfalls `Application.Tests`).
- `Infrastructure/Persistence/MembershipReaderTests.cs` — Adapter-Test
  für den `IMembershipReader` (wäre andernfalls
  `Infrastructure.Tests`).

Beide werden aus diesem Test-Projekt mitgetestet, weil die Aufteilung
in separate Test-Projekte den Aufwand nicht rechtfertigt. Neue
Adapter- oder JSON-Serialisierungs-Tests sollten **diese** Konvention
übernehmen, statt eigene Projekte aufzumachen.

## AAA-Struktur

Jeder Test folgt Arrange–Act–Assert. Aussagekräftige Variablennamen
(`sut` für System-under-Test, `expected`, `actual`) sind erwünscht.

```csharp
[Fact]
public async Task Handle_WhenRequestIsValid_ShouldCreateOrganisation()
{
    var db = CreateInMemoryDbContext();
    var auth = MockAuthProvider.WithUser(TestUsers.Admin);
    var sut = new CreateOrganisationHandler(db, auth, TimeProvider.System, NullLogger<CreateOrganisationHandler>.Instance);

    var result = await sut.HandleAsync(new CreateOrganisationRequest("Verein", "Beschreibung"), CancellationToken.None);

    result.IsError.Should().BeFalse();
    result.Value.Name.Should().Be("Verein");
}
```

## Test-Fixtures und Mocks

- `new Mock<T>()` für Interface-Stubs, mit Setup-Ketten für
  Assertions auf Interaktionen.
- `TimeProvider` wird mit `FakeTimeProvider` aus
  `Microsoft.Extensions.TimeProvider.Testing` für deterministische
  Zeitstempel verwendet.

### Ports mocken statt Konstruktor-Tricks

Wenn der Handler einen **Port** injiziert bekommt
(`IInvitationCodeGenerator`, `IAuthProvider`, `IUserAdminClient`),
wird der Port per Moq gestubbt — **nicht** der Konstruktor des
Handlers per Reflection oder `InternalsVisibleTo` geöffnet.

```csharp
var queue = new Queue<string>(new[] { "AAA-BCD", "DEF-GHJ" });
var generator = new Mock<IInvitationCodeGenerator>();
generator.Setup(g => g.Generate())
         .Returns(() => queue.Count > 0 ? queue.Dequeue() : throw new InvalidOperationException("Generator exhausted."));
```

Wenn die Sequenz erschöpft ist, schlägt der Test mit einer klaren
Exception fehl. So werden Endlos-Loops in der Implementierung früh
sichtbar.

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
  angewandt – erwünscht, weil es die Produktionslogik spiegelt.
- Tests, die soft-gelöschte Datensätze lesen müssen, nutzen
  `IgnoreQueryFilters()` direkt am `DbSet`. Wenn
  `IgnoreQueryFilters()`-Pfade getestet werden, ist das im Testnamen
  explizit zu kennzeichnen.
- In-Memory-Provider **kennt keine Unique-Constraints** — Tests, die
  den `DbUpdateException`-Retry-Pfad absichern, müssen gegen eine
  echte Postgres-Instanz laufen (Integrationstest, separate Suite).

## Was hier **nicht** hineingehört

- **Validator-Tests** — kein `<Request>ValidatorTests.cs`.
- **Domain-Tests** — keine `Features/Domain/`-Ordner mit Tests für
  `User`, `Invitation`, `InvitationCodeGenerator` etc.
- **Api-/Endpoint-Tests** — keine HTTP-Tests (separates
  Integration-Test-Projekt).
- Keine echte Datenbankverbindung (Ausnahme: dedizierter
  Integrationstest für DB-spezifische Features wie Constraints).
- Keine produktiven `appsettings.json`-Werte.
- Keine Reflection-Tricks oder `InternalsVisibleTo`-Konstrukte — wenn
  Test-Determinismus nötig ist, wird ein Port-Interface gemockt.
- Keine leeren `Request`-Tests, wenn der Handler keinen Request nimmt.
- Keine zwei parallelen `CreateSut`-Helfer — eine Helper-Methode pro
  Testklasse reicht.
