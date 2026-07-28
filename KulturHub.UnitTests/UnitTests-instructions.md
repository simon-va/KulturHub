# KulturHub.UnitTests

Isolierte Tests für die Use-Case-Handler. Pro Handler wird **ein Test
pro Pfad** durch den Handler-Code geschrieben. Andere Belange
(Validatoren, Domain-Regeln, Generatoren) werden hier **nicht** getestet
— sie sind bereits durch ihre eigenen Code-Pfade in den Handlern
abgedeckt bzw. gehören in dedizierte Test-Projekte.

## Test-Stack

- **xUnit** als Test-Runner
- **Moq** für Interface-Stubs (z. B. `IInvitationCodeGenerator`,
  `IAuthProvider`, `IUserAdminClient`)
- **FluentAssertions** für lesbare Asserts
- **EF Core InMemory-Provider** (`UseInMemoryDatabase`) oder
  **`Microsoft.EntityFrameworkCore.InMemory`** zum Testen des
  `AppDbContext`. `InMemory` ist in Ordnung, wenn keine
  datenbankspezifischen Features genutzt werden; sonst lieber
  SQLite-in-Memory.

> **Hinweis:** `FluentValidation` wird im Test-Projekt **nicht**
> referenziert. Validatoren leben im Application-Layer und werden
> indirekt über den `HandleAsync`-Pfad des Handlers mitgetestet
> (siehe „Was hier **nicht** getestet wird" weiter unten).
> `FluentValidation.TestHelper` o. Ä. ist hier bewusst nicht im
> Einsatz, damit keine parallelen Validator-Test-Suiten entstehen.

## Namenskonventionen

- Testordner spiegeln das zu testende Projekt:

```
KulturHub.UnitTests/
├── Features/
│   └── Application/                  # ausschließlich Use-Case-Handler
│       └── <Bereich>/                # z. B. Public/Auth, Admin/Invitations
│           └── <UseCase>/
│               └── <Handler>Tests.cs
└── TestHelpers/                      # projektweite Mocks und Factories
```

- Pro Handler eine eigene Testklasse: `<HandlerName>Tests.cs`.
- Testmethode: `MethodName_Scenario_ExpectedResult`.

Beispiele:

```csharp
Handle_WhenNameIsEmpty_ShouldReturnValidationError
Handle_WhenOrganisationNotFound_ShouldReturnNotFound
Handle_WhenAllRetriesCollide_ShouldReturnCodeGenerationFailed
```

## Was hier getestet wird

Nur die einzelnen **Pfade durch den Handler-Code**. Ein Pfad ist jede
eindeutige Rückgabe bzw. jeder Seiteneffekt-Zweig, den der Handler
annehmen kann (z. B. `return InvitationErrors.NotFound`,
`return signUpResult.Errors`, persistierter User + markierte Einladung,
Rollback-Aufruf an den Admin-Client).

Pro Handler wird **genau ein Test pro Pfad** geschrieben. Nicht mehr,
nicht weniger. Das bedeutet:

- **Happy Path** – gültiger Request → erwartete Response und
  Seiteneffekte.
- **Jeder Fehlerpfad** – jeder `return Errors`-Zweig bekommt einen
  eigenen Test.
- **Jeder Kompensations-/Rollback-Pfad** – jeder Aufruf einer externen
  Kompensationsaktion bekommt einen eigenen Test.

### Was bewusst **nicht** getestet wird

- **Validatoren** (`SignUpRequestValidator`, etc.) – die
  Validierungslogik wird indirekt über den `HandleAsync`-Pfad des
  Handlers geprüft (siehe `User.EmailInvalid`-Test im `SignUpHandler`).
  Eigene `<Request>ValidatorTests.cs`-Dateien werden **nicht** angelegt.
- **Domain-Entities** (`User`, `Invitation`) und
  **Domain-Services** (`InvitationCodeGenerator`) – gehören in ein
  separates `KulturHub.Domain.Tests`-Projekt oder werden über die
  Handler-Use-Cases indirekt mitgetestet. Hier keine
  `Features/Domain/`-Ordner.
- **Doppelte Absicherung** desselben Pfads (z. B. mehrere
  `[Theory]`-Inline-Datasets für denselben Fehlerzweig) – ein Test pro
  Pfad reicht.
- **Happy Path** und **Fehlerpfad**, die der Handler gar nicht hat.

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
- Tests, die soft-gelöschte Datensätze lesen müssen, nutzen
  `IgnoreQueryFilters()` direkt am `DbSet`. Das ist die einzige
  produktive Verwendung außerhalb von Admin/Recovery-Pfaden und
  daher im Testcode ausdrücklich erlaubt. Wenn `IgnoreQueryFilters()`-
  Pfade getestet werden, ist das im Testnamen explizit zu kennzeichnen.
- In-Memory-Provider **kennt keine Unique-Constraints** — Tests, die
  den `DbUpdateException`-Retry-Pfad absichern, müssen gegen eine
  echte Postgres-Instanz laufen (Integrationstest, separate Suite).

## Was hier **nicht** hineingehört

- **Validator-Tests** — kein `<Request>ValidatorTests.cs`. Wenn die
  Validierungslogik getestet werden soll, gehört das in ein
  dediziertes Test-Projekt für Validatoren.
- **Domain-Tests** — keine `Features/Domain/`-Ordner mit Tests für
  `User`, `Invitation`, `InvitationCodeGenerator` etc. Diese Tests
  gehören in ein separates `KulturHub.Domain.Tests`-Projekt.
- **Api-/Endpoint-Tests** — keine HTTP-Tests (separates
  Integration-Test-Projekt, nicht Teil dieser Anleitung).
- Keine echte Datenbankverbindung (Ausnahme: dedizierter
  Integrationstest für DB-spezifische Features wie Constraints)
- Keine produktiven `appsettings.json`-Werte
- Keine Reflection-Tricks oder `InternalsVisibleTo`-Konstrukte — wenn
  Test-Determinismus nötig ist, wird ein Port-Interface gemockt
- Keine leeren `Request`-Tests, wenn der Handler keinen Request nimmt
- Keine zwei parallelen `CreateSut`-Helfer (z. B. „String" vs.
  „Queue") — eine Helper-Methode pro Testklasse reicht