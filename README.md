# KulturHub

KiBuU – **Kultur in Bocholt und Umgebung**. KulturHub ist eine Webanwendung, die
Kulturvereine, lose Gruppen und Künstler mit kulturinteressierten Menschen
verbindet. Sichtbar wird das Angebot über Veranstaltungsankündigungen, Berichte
und Vereinssteckbriefe.

Den Kern der Plattform bilden **Organisationen** – der Oberbegriff für Vereine,
lose Gruppen oder Einzelpersonen, die Inhalte veröffentlichen.

## Inhaltsverzeichnis

1. [Über das Projekt](#1-über-das-projekt)
2. [Anwender und Bereiche](#2-anwender-und-bereiche)
3. [Features und Bausteine](#3-features-und-bausteine)
4. [Architektur](#4-architektur)
5. [Tech-Stack](#5-tech-stack)
6. [Projektstruktur](#6-projektstruktur)
7. [Konventionen](#7-konventionen)
8. [Voraussetzungen](#8-voraussetzungen)
9. [Konfiguration](#9-konfiguration)
10. [Setup und Start](#10-setup-und-start)
11. [Tests](#11-tests)
12. [API-Übersicht](#12-api-übersicht)
13. [Lizenz und Kontakt](#13-lizenz-und-kontakt)

---

## 1. Über das Projekt

KulturHub unterstützt das **KiBuU-Netzwerk** dabei, kulturelles Engagement in der
Region Bocholt sichtbar zu machen. Die Plattform richtet sich gleichermaßen an
Vereine, die ihre Inhalte selbst pflegen möchten, und an Besucher, die
Veranstaltungen, Berichte und Profile entdecken wollen.

Leitgedanken:

- **Selbstverwaltung**: Organisationen verwalten ihre Inhalte eigenständig.
- **Mehrere Vertreter**: Über Memberships können mehrere Personen eine
  Organisation gemeinsam pflegen.
- **Nachvollziehbarkeit**: Jede Aktion wird über einen Change Log dokumentiert.
- **Erweiterbarkeit**: Geplante Bausteine wie Veranstaltungen, Berichte und
  Social-Media-Integrationen wachsen modular hinzu.

## 2. Anwender und Bereiche

### Anwenderrollen

| Rolle | Beschreibung |
| --- | --- |
| **Besucher** | Anonyme Person. Greift ausschließlich auf öffentlich freigegebene Inhalte zu. |
| **Nutzer** | Angemeldeter Vertreter einer Organisation. Pflegt Inhalte der eigenen Organisationen. |
| **Admin** | Systemadministrator. Verwaltet alle Inhalte aller Organisationen und kann im Notfall eingreifen. |

### Anwendungsbereiche

| Bereich | Authentifizierung | Zweck |
| --- | --- | --- |
| **Public** | keine | Veröffentlichte Inhalte aller Organisationen. |
| **Plattform** | JWT (Nutzer) | Inhaltsverwaltung für angemeldete Vereinsvertreter. |
| **Admin** | JWT (Admin) | Systemweite Verwaltung, nur für Administratoren. |

Zusammenhang: Der **Admin**-Bereich erweitert den **Plattform**-Bereich um
systemweite Operationen, der **Public**-Bereich ist die für **Besucher**
zugängliche Sicht auf veröffentlichte Inhalte.

## 3. Features und Bausteine

### Legende

- `[x]` bereits implementiert
- `[ ]` noch nicht implementiert

### Kernfunktionen (geplant)

- [ ] **Invitations**: Admins erstellen und verwalten Einladungen zur Plattform.
- [ ] **User**: Nutzer registrieren und melden sich an.
- [ ] **Organisationen**: Anlegen und Verwalten von Organisationen.
- [ ] **Memberships**: Mehrere Nutzer verwalten eine Organisation, ein Nutzer
      kann Mitglied mehrerer Organisationen sein.
- [ ] **Change Logs**: Jede Aktion eines Nutzers wird durch einen Change Log
      dokumentiert.
- [ ] **Soft Delete**: Organisationen, Memberships, Einladungen und Nutzer
      werden soft-gelöscht; Daten bleiben revisionssicher.
- [ ] **OpenAPI je Bereich**: Separate API-Dokumente für Public, Plattform und
      Admin.

### Geplante Bausteine

- [ ] **Veranstaltungen**: Anlegen und Verwalten von Veranstaltungen.
- [ ] **Berichte**: Erstellen und Verwalten von Berichten.
- [ ] **Mach Mit!**: Aufforderungen zu Aktionen einer Organisation.
- [ ] **Steckbrief**: Wichtige Informationen zu einer Organisation.
- [ ] **Berichte-Editor**: Online-Editor mit Vorlagen, der Slides für
      Social-Media-Posts als PNG-Bilder erzeugt.
- [ ] **KI-Integration**: Unterstützende KI-Funktionen beim Erstellen von
      Berichten, Posts und weiteren Bausteinen.
- [ ] **Instagram-Integration**: Posten von Inhalten auf Instagram.
- [ ] **Facebook-Integration**: Posten von Inhalten auf Facebook.
- [ ] **Wöchentlicher Post**: Zusammenfassung aller kommenden Veranstaltungen
      der nächsten Woche, automatisch auf Social-Media-Kanälen gepostet.

## 4. Architektur

KulturHub nutzt eine **pragmatische Schichtenarchitektur**. Die Schichten sind
weiterhin vorhanden, dürfen aber bewusst miteinander verschmelzen, wenn es
klarer und kürzer ist. Strikte Clean-Architecture-Regeln (keine
Schicht-übergreifenden Abhängigkeiten, kein direkter Zugriff zwischen
Anwendung und Persistenz) werden **nicht** erzwungen.

```
        ┌──────────────────────────────────────┐
        │          KulturHub.Api               │  ← HTTP-Endpunkte, Auth, OpenAPI
        └──────────────────┬───────────────────┘
                           │
        ┌──────────────────▼───────────────────┐
        │     KulturHub.Infrastructure         │  ← EF Core, Supabase
        └──────────────────┬───────────────────┘
                           │
        ┌──────────────────▼───────────────────┐
        │      KulturHub.Application           │  ← Use Cases, Validierung, DTOs
        └──────────────────┬───────────────────┘
                           │
        ┌──────────────────▼───────────────────┐
        │        KulturHub.Domain              │  ← Entities, Domain-Regeln
        └──────────────────────────────────────┘
```

### Schichtenverantwortlichkeiten

- **Domain**: Pure POCO-Entities mit Geschäftslogik, Factory-Methoden
  (`Create` / `Reconstitute`), Invariants. **Keine** Abhängigkeiten auf
  Frameworks, Datenbanken oder externe Dienste.
- **Application**: Use-Case-Handler, `ErrorOr`-Result-Pattern,
  FluentValidation-Validatoren, Request-/Response-DTOs, Ports für externe
  Systeme. Darf den `AppDbContext` über das `IAppDbContext`-Interface direkt
  nutzen (DbSets, `IQueryable<T>`, LINQ-to-Entities). Es gibt **keinen**
  Repository-Zwang – eine direkte EF-Core-Abfrage im Handler ist erlaubt.
- **Infrastructure**: `AppDbContext`, `IEntityTypeConfiguration<T>`-Klassen,
  EF-Core-Migrations, Supabase-Auth-Client, Implementierungen der Ports aus
  dem Application-Layer.
- **Api**: ASP.NET Core Minimal Hosting, Endpunkt-Definitionen, JWT-Bearer,
  drei OpenAPI-Dokumente, globale Filter für Authentifizierung, Autorisierung
  und Validation, Mapping von `ErrorOr<T>` auf HTTP-Responses.

### Wichtige Patterns

- **Result-Pattern mit `ErrorOr`**: Handler liefern `ErrorOr<TResponse>`
  zurück, Businessfehler werden nicht über Exceptions transportiert.
- **FluentValidation** für Request-Validierung im Application-Layer,
  ausgeführt im Endpoint-Filter.
- **EF Core** statt Dapper: `AppDbContext` als zentrales
  Persistenz-Boundary, Migrations im Infrastructure-Projekt.
- **Global Query Filter für Soft Delete**: Jede Entity mit `IsDeleted` blendet
  gelöschte Datensätze automatisch aus Leseabfragen aus. Wer sie bewusst
  lesen muss, nutzt explizit `IgnoreQueryFilters()`.
- **OpenAPI je Bereich**: Endpunkte wählen per `.WithGroupName(...)` eines
  der drei Dokumente (`public`, `platform`, `admin`).
- **JWT-Bearer-Auth**: Supabase liefert Tokens; das Backend validiert sie
  über die OIDC-Discovery-URL. Der `sub`-Claim ist der stabile
  Nutzer-Identifier.

## 5. Tech-Stack

| Aspekt | Technologie |
| --- | --- |
| Sprache / Framework | C# / .NET 10, ASP.NET Core (Minimal Hosting) |
| Datenbank | PostgreSQL (Supabase) |
| ORM | EF Core (`Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`) |
| Authentifizierung | Supabase Auth (OIDC) – JWT-Bearer am Backend |
| API-Dokumentation | `Microsoft.AspNetCore.OpenApi` + Scalar UI |
| Validierung | FluentValidation |
| Result-Pattern | ErrorOr |
| Migrations | EF Core Migrations + `dotnet-ef`-CLI |
| Tests | xUnit, Moq, FluentAssertions, EF Core InMemory/SQLite-in-Memory |
| Frontend (separat) | Angular, ausgeliefert von `http://localhost:4200` (Dev) |

Versions-Badges werden ergänzt, sobald eine CI/CD-Pipeline etabliert ist.

## 6. Projektstruktur

```
KulturHub.sln
PLAN.md                                  # User-Stories und Akzeptanzkriterien
KulturHub.Domain/                        # Entities, Domain-Regeln
KulturHub.Application/                   # Use Cases, Validatoren, DTOs, Ports
KulturHub.Infrastructure/                # EF Core, Supabase
└── Persistence/
    ├── AppDbContext.cs
    ├── Configurations/                  # IEntityTypeConfiguration<T>
    ├── Migrations/                      # EF Core Migrations
    └── DesignTimeDbContextFactory.cs
KulturHub.Api/                           # HTTP-Endpunkte, Auth, OpenAPI
├── Extensions/                          # ServiceCollection-Erweiterungen
├── Filters/                             # Endpoint-Filter (Validation, ...)
├── Endpoints/                           # Public / Platform / Admin
└── http/                                # .http-Beispielrequests
KulturHub.UnitTests/                     # xUnit, Moq, FluentAssertions
```

### Projekte

| Projekt | Zweck |
| --- | --- |
| `KulturHub.Domain` | POCO-Entities und Geschäftsregeln. Keine Abhängigkeiten. |
| `KulturHub.Application` | Use-Case-Handler, `ErrorOr`, FluentValidation, Request-/Response-DTOs, Ports für externe Systeme. Darf `IAppDbContext` nutzen. |
| `KulturHub.Infrastructure` | `AppDbContext`, EF-Core-Migrations, Konfigurationen, Supabase-Integration. |
| `KulturHub.Api` | ASP.NET-Core-Endpunkte, JWT-Bearer, OpenAPI, Scalar. |
| `KulturHub.UnitTests` | Isolierte Tests mit xUnit, Moq, FluentAssertions. |

### Wichtige Inhalte

- **Migrationen** liegen unter
  `KulturHub.Infrastructure/Persistence/Migrations/` und werden über
  `dotnet ef` verwaltet.
- **Beispielrequests** für jeden API-Bereich liegen unter
  `KulturHub.Api/http/<bereich>/` und können mit der VS-Code-Erweiterung
  *REST Client* oder JetBrains Rider direkt ausgeführt werden.
- **Konventionen**: Schichtspezifische Regeln und Patterns liegen in den
  `*-instructions.md`-Dateien der jeweiligen Projekte. Siehe
  [Konventionen](#7-konventionen).

## 7. Konventionen

Detaillierte Konventionen je Schicht – Patterns, Stilregeln und Pitfalls –
sind pro Projekt in einer eigenen `*-instructions.md` dokumentiert. Vor
jeder Änderung in einer Schicht die zugehörige Datei lesen.

| Datei | Inhalt |
| --- | --- |
| [`KulturHub.Domain/Domain-instructions.md`](KulturHub.Domain/Domain-instructions.md) | Aufbau der Entities, `Create`/`Reconstitute`-Factory-Pattern, Invariants, Soft-Delete-Felder, `DateTime`-Semantik, strongly typed IDs. |
| [`KulturHub.Application/Application-instructions.md`](KulturHub.Application/Application-instructions.md) | Handler-Aufbau, `ErrorOr`-Pattern, Validatoren, Transaktionen, Change-Log-Pflicht, erlaubter EF-Core-Zugriff, Ports für externe Systeme. |
| [`KulturHub.Infrastructure/Infrastructure-instructions.md`](KulturHub.Infrastructure/Infrastructure-instructions.md) | EF-Core-Konventionen, `AppDbContext`, `IEntityTypeConfiguration<T>`, Global Query Filter, Migrations-Workflow, Supabase-Integration. |
| [`KulturHub.Api/Api-instructions.md`](KulturHub.Api/Api-instructions.md) | Minimal-API-Endpunkte, Filter, OpenAPI je Bereich, `Program.cs`-Pipeline, JWT-/CORS-/JSON-Setup, `ErrorOr`-Mapping. |
| [`KulturHub.UnitTests/UnitTests-instructions.md`](KulturHub.UnitTests/UnitTests-instructions.md) | Test-Stack, Namenskonventionen, Pflicht-Testfälle für jeden Handler, EF-Core in Tests. |

## 8. Voraussetzungen

- **.NET 10 SDK** (`dotnet --version` muss 10.x ausgeben)
- **`dotnet-ef`-Tool** (global), passend zur .NET-Version
- **PostgreSQL 14+**, lokal oder über Supabase
- **Supabase-Projekt** (Auth + Postgres) – eine kostenlose Tier-Instanz reicht
  für die Entwicklung
- **JetBrains Rider** als bevorzugte IDE (das Projekt nutzt
  `KulturHub.sln.DotSettings.user`-Einstellungen)

## 9. Konfiguration

Die Anwendung erwartet folgende Konfigurationsabschnitte:

| Sektion | Zweck |
| --- | --- |
| `ConnectionStrings:Default` | PostgreSQL-Verbindungszeichenfolge |
| `Supabase:Url` | Basis-URL des Supabase-Projekts |
| `Supabase:Key` | Anonymer Public-Key des Supabase-Projekts |
| `Supabase:DiscoveryUrl` | OIDC-Discovery-URL von Supabase Auth |
| `Cors:AllowedOrigins` | Liste erlaubter Frontend-Ursprünge. Dev: `http://localhost:4200` (`appsettings.Development.json`). Prod: `https://kibuu.de`, `https://www.kibuu.de`, `https://api.kibuu.de` (`appsettings.Production.json`). |
| `Logging` | Standard ASP.NET-Core-Logging |

### ⚠️ Sicherheitshinweis

Echte Werte für `ConnectionStrings:Default`, `Supabase:Url`, `Supabase:Key`
und `Supabase:DiscoveryUrl` gehören **nicht** in den Git-Tree. Lege sie
stattdessen über **User Secrets** oder **Umgebungsvariablen** an:

```bash
dotnet tool install --global dotnet-ef --version 10.*

cd KulturHub.Api
dotnet user-secrets init   # einmalig pro Klone (UserSecretsId ist bereits gesetzt)

dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=db.<project>.supabase.co;Database=postgres;Username=postgres;Password=<PW>;SSL Mode=Require;Trust Server Certificate=true"

dotnet user-secrets set "Supabase:Url"        "https://<project>.supabase.co"
dotnet user-secrets set "Supabase:Key"        "<anon-public-key>"
dotnet user-secrets set "Supabase:DiscoveryUrl" "https://<project>.supabase.co/auth/v1/.well-known/openid-configuration"
```

Alternativ funktionieren Umgebungsvariablen im Stil
`Supabase__Url`, `ConnectionStrings__Default` (jeweils `:` durch `__` ersetzt).

## 10. Setup und Start

```bash
# 1. Repository klonen
git clone <repo-url>
cd KulturHub

# 2. dotnet-ef-Tool installieren (einmalig)
dotnet tool install --global dotnet-ef --version 10.*

# 3. User Secrets setzen (siehe oben)

# 4. Datenbankmigrationen anwenden
dotnet ef database update \
  --project KulturHub.Infrastructure \
  --startup-project KulturHub.Api

# 5. Backend bauen
dotnet build

# 6. Backend starten
dotnet run --project KulturHub.Api
```

Erwartete Entwickler-URLs (siehe `KulturHub.Api/Properties/launchSettings.json`):

- HTTP:    `http://localhost:5159`
- HTTPS:   `https://localhost:7158`
- Scalar:  `http://localhost:5159/scalar`
- OpenAPI:  `http://localhost:5159/openapi/public.json` · `/platform.json` · `/admin.json`

## 11. Tests

```bash
# Alle Tests
dotnet test

# Mit Coverage
dotnet test --collect:"XPlat Code Coverage"
```

Die Tests sind isoliert von Datenbank und HTTP:

- **xUnit** als Test-Runner
- **Moq** für Service-Stubs (z. B. `IAuthProvider`, `TimeProvider`)
- **FluentAssertions** für lesbare Asserts
- **EF Core InMemory** oder SQLite-in-Memory für DbContext-Tests
- Pro Handler eine eigene Testklasse (`{HandlerName}Tests.cs`)
- Benennung: `MethodName_Scenario_ExpectedResult`, z. B.
  `Handle_WhenNameIsEmpty_ShouldReturnValidationError`

Konventionen sind in [`KulturHub.UnitTests/UnitTests-instructions.md`](KulturHub.UnitTests/UnitTests-instructions.md) festgehalten.

## 12. API-Übersicht

Die API ist in **drei OpenAPI-Dokumente** aufgeteilt, die in Scalar einzeln
ausgewählt werden können:

| Dokument | Audience | Inhalt |
| --- | --- | --- |
| `/openapi/public.json` | Anonyme Besucher | `GET /health`, künftige Public-Reads |
| `/openapi/platform.json` | Angemeldete Nutzer | `GET /users/me`, eigene Organisationen, Memberships, `GET /organisations/{id}/change-logs` |
| `/openapi/admin.json` | Administratoren | Systemweite Verwaltung, Invitations |

Ausführliche Schema-Beschreibungen und „Try it out"-Funktionen stellt
[Scalar](https://github.com/scalar/scalar) bereit (im Development-Modus
unter `/scalar`).

Beispielhafte HTTP-Requests liegen im jeweiligen Bereich unter
`KulturHub.Api/http/<bereich>/` und lassen sich mit der
[VS Code-Erweiterung REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client)
oder JetBrains Rider direkt ausführen.

## 13. Lizenz und Kontakt

Dieses Projekt wird im Kontext des **KiBuU – Kultur in Bocholt und Umgebung**
gepflegt. Eine Lizenz ist aktuell nicht hinterlegt; bei Interesse an
Mitwirkung oder Nutzung bitte Kontakt zum KiBuU-Vorstand aufnehmen.

## 14. dotnet für AI Sessions

Falls `dotnet` in der Shell nicht gefunden wird:
`export PATH="$HOME/.dotnet:$PATH"`