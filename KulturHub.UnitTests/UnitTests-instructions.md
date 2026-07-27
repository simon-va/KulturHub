# KulturHub.UnitTests

Isolierte Tests für Handler, Validatoren und Domänenregeln.

## Test-Stack

- **xUnit** als Test-Runner
- **Moq** für Repository- und Service-Stubs (z. B. `IAuthProvider`,
  `TimeProvider`)
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
└── Features/
    └── <Bereich>/
        └── <UseCase>/
            └── <Handler>Tests.cs
            └── <Request>ValidatorTests.cs
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

## Validierungs-Tests

- Validatoren werden direkt mit `IValidator<T>.ValidateAsync(request)`
  getestet – ohne HTTP.
- Assert über `TestValidationResult`-Helfer von FluentValidation oder
  direkt über `result.IsValid` und `result.Errors`.

## Test-Fixtures und Mocks

- Test-Daten und -Mocks liegen im jeweiligen Testordner oder in einer
  `TestData/`-Klasse im selben Bereich, wenn sie geteilt werden.
- `Mock.Of<T>()` für einfache Stubs, `new Mock<T>()` für Assertions
  auf Interaktionen (selten nötig).
- `TimeProvider` wird mit `FakeTimeProvider` aus
  `Microsoft.Extensions.TimeProvider.Testing` für deterministische
  Zeitstempel verwendet.

## Was hier **nicht** hineingehört

- Keine echte Datenbankverbindung
- Keine HTTP-Tests (separates Integration-Test-Projekt, nicht Teil
  dieser Anleitung)
- Keine produktiven `appsettings.json`-Werte
