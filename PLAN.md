# Plan

Diese Datei dient zur besseren Planung von neuen Funktionen:

## User-Story — Membership löschen

Als Nutzer
möchte ich ein Membership löschen können
um die Liste der berechtigten Personen aktuell zu halten

### Akzeptanzkriterien

- Neuer DELETE Endpunkt: memberships/{membershipId}
- Endpunkt ist mit RequireAuth abgesichert
- Löschen erlaubt, wenn Actor Mitglied der Organisation ist (Membership.Status == Accepted)
- Membership darf gelöscht werden, sofern danach noch mindestens ein weiteres aktives Member übrig bleibt
- Self-Delete (eigene Membership) immer erlaubt, sofern danach noch mindestens ein weiteres aktives Member übrig bleibt
- Es darf nicht das letzte aktive Member der Organisation gelöscht werden (Org muss mindestens ein Member behalten) → 409 Conflict
- Der Status der Membership ist egal
- Idempotentes Verhalten: Zweiter DELETE auf dieselbe Membership gibt 404 zurück
- Soft-Delete: Neues DB-Feld deleted_at (nullable)
- ChangeLog wird erstellt (Delete-Event mit Actor + MembershipId + OrgId)
- Membership.Delete nimmt Actor als Parameter und prüft Org-Mitgliedschaft + "letztes Member"-Constraint in einer Transaction
- Bestehende Queries (GetMemberships, GetInvites etc.) filtern `deleted_at IS NULL` per globalem Query-Filter
- Bestehende MembershipResponse/InviteMembershipResponse bleiben unverändert — DeletedAt ist eine interne Metrik und wird nicht nach außen gegeben
- Migration: deleted_at neu (nullable) + Index auf (org_id, user_id, deleted_at)
- Bestehende Tests, Handler, Configurations, Responses entsprechend anpassen

## User-Story — Eigene Stammdaten abfragen / Soft-Delete am User-Aggregat

Als angemeldeter Nutzer
möchte ich meine eigenen Stammdaten abfragen können
um im Frontend Profil, Header und Account-Einstellungen rendern zu können,
ohne jeden Wert einzeln aus dem JWT-Claim ableiten zu müssen.

### Voraussetzung — Soft-Delete am User-Aggregat

`User` trägt heute noch keine Soft-Delete-Felder, obwohl alle anderen
veränderlichen Entities (Membership, Organisation, Invitation, ChangeLog)
bereits nach dem Muster der `Domain-instructions.md` mit
`IsDeleted`/`DeletedAt` und Global Query Filter ausgestattet sind.
Wir ergänzen diesen Drift im selben Schritt, damit die
404-Regel für gelöschte User sauber greift.

### Akzeptanzkriterien — Soft-Delete-Substanz

- Domain: `KulturHub.Domain/Users/User.cs`
  - Privater Konstruktor nimmt zusätzlich `bool isDeleted, DateTime? deletedAt`
  - Neue Felder `public bool IsDeleted { get; private set; }`,
    `public DateTime? DeletedAt { get; private set; }`
  - `Create(...)` Factory setzt `isDeleted: false, deletedAt: null`
  - Neue Methode `Delete(TimeProvider clock)` analog zu `Membership.Delete`:
    UTC-Check, `IsDeleted = true`, `DeletedAt = now`,
    gibt `ErrorOr<Success>` zurück
  - Signatur von `Create(...)` bleibt unverändert (kein Bruch der Aufrufer)
- Configuration: `KulturHub.Infrastructure/Persistence/Configurations/UserConfiguration.cs`
  - `IsDeleted` → `is_deleted` (boolean, NOT NULL, Default `false`)
  - `DeletedAt` → `deleted_at` (timestamp with time zone, nullable)
  - `builder.HasQueryFilter(x => !x.IsDeleted)`
- Migration `AddUserSoftDelete`:
  - `AddColumn is_deleted boolean NOT NULL DEFAULT FALSE` (Backfill implizit über Default)
  - `AddColumn deleted_at timestamp with time zone NULL`
  - `CreateIndex IX_users_is_deleted ON users (is_deleted)`
- `SignUp`-Tests und alle bestehenden User-Aufrufer bleiben grün (Default-Werte
  stellen kompatibles Verhalten sicher)
- Folge-Story „User-Account endgültig löschen" (DB-Soft-Delete + Supabase-Disable
  + Last-Active-Member-Check, analog Membership-Delete) wird bewusst
  **zurückgestellt** und nicht in dieser Story mit verbaut

### Akzeptanzkriterien — GET /users/me

- Neuer Endpunkt: `GET /users/me` (Platform-Bereich, OpenAPI-Gruppe `platform`, Tag `Users`)
- `RequireAuthorization()` → 401 bei fehlendem/ungültigem JWT
- Identität ausschließlich aus JWT-`sub` (keine UserId aus Query/Body)
- Antwort `200 OK` mit `MeResponse { UserId, FirstName, LastName, Email, CreatedAt }`
- `UserId` als `Guid` (wie SignInResponse), `CreatedAt` UTC `DateTime`
- Keine weiteren Felder (`IsAdmin`, Tokens, Passwörter)
- 404 `code = "User.NotFound"` wenn User soft-gelöscht ist
  (`deleted_at IS NOT NULL` per Global Query Filter, JWT kann noch gültig sein)
- 404 auch wenn der User in der DB komplett fehlt (Edge Case, gleiche Antwort)
- Idempotent: zweiter Aufruf liefert identische Daten, kein Logging-Spam
- Kein Change-Log-Eintrag (Read-only)

### Anpassungen Application-Layer

- `IUserReader.cs`: neue Methode `Task<User?> GetByIdAsync(UserId id, CancellationToken ct)`
- `Application/Errors/UserErrors.cs` (neu oder erweitert):
  - `NotFound` → `Error.NotFound("User.NotFound", "...")`
- `Application/Features/Platform/Users/GetCurrentUser/`:
  - `MeResponse.cs` — `record MeResponse(Guid UserId, string FirstName, string LastName, string Email, DateTime CreatedAt)`
  - `GetCurrentUserHandler.cs` — `HandleAsync(Guid userId, CancellationToken)` →
    `ErrorOr<MeResponse>`; liest via `IUserReader.GetByIdAsync`, mappt,
    `null` → `UserErrors.NotFound`

### Anpassungen Infrastructure-Layer

- `UserReader.cs`: `GetByIdAsync` per
  `db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id)`;
  Global Query Filter respektieren → soft-deleted = `null` → 404

### Anpassungen Api-Layer

- Neue Datei `KulturHub.Api/Endpoints/Platform/UserEndpoints.cs`
- `MapGroup("/users")` einmalig:
  `.WithTags("Users").WithGroupName("platform").RequireAuthorization()`
- `GET /me` → `WithName("Users_GetMe")`,
  `.Produces<MeResponse>(200)`, `.ProducesProblem(401)`, `.ProducesProblem(404)`
- DI-Parameter mit `[FromServices]`, kein Body → kein `ValidationFilter`
- `app.MapUserEndpoints()` in `Program.cs` registrieren

### HTTP-Beispiel

`KulturHub.Api/http/platform/users/get-me.http`:

```http
GET {{baseUrl}}/users/me
Authorization: Bearer {{token}}
```

### Tests

`KulturHub.UnitTests/Features/Application/Platform/Users/GetCurrentUser/GetCurrentUserHandlerTests.cs`:

- `Handle_WhenUserExists_ShouldReturnMeResponse`
- `Handle_WhenUserIsSoftDeleted_ShouldReturnNotFound`
- `Handle_WhenUserDoesNotExistInDb_ShouldReturnNotFound`
- `Handle_PassesCancellationTokenToReader`

`Mock<IUserReader>` mit `Mock.Of<TimeProvider>()` falls später `User.Delete`
in Tests gebraucht wird.

### Doku

- `README.md` Abschnitt 12: Eintrag „`GET /users/me` – aktueller Nutzer"
  unter Plattform-API
- Diese User-Story ist die kanonische Quelle für beide Sub-Bereiche
  (Soft-Delete-Substanz + Read-Endpunkt)

## User-Story — Change-Logs einer Organisation abfragen

Als Nutzer
möchte ich die Change-Logs einer Organisation paginiert durchsuchen können
um nachvollziehen zu können, wer wann welche Änderungen vorgenommen hat.

### Akzeptanzkriterien

- Neuer Endpunkt: `GET /organisations/{organisationId:guid}/change-logs`
  (Platform-Bereich, OpenAPI-Gruppe `platform`, Tag `ChangeLogs`)
- `RequireAuthorization()` → 401 bei fehlendem/ungültigem JWT
- `MembershipAuthorizationFilter` → 403 wenn Actor kein akzeptiertes
  Mitglied der Organisation ist
- Query-Parameter:
  - `skip` (int, default `0`, muss `>= 0` sein)
  - `take` (int, default `50`, muss in `[1, 200]` liegen)
  - `search` (string, optional, max 500 Zeichen) — case-insensitive Suche
    auf `message` **und** `CreatedBy.FullName` (FirstName + LastName)
- Sortierung: `CreatedAt DESC` (jüngste zuerst)
- Response 200: `PagedResult<ChangeLogResponse>` mit `Items`, `Total`,
  `Skip`, `Take`
- `ChangeLogResponse` Felder: `Id`, `CreatedBy` (UserId), `CreatedByFullName`,
  `Message`, `Data`, `CreatedAt` — **keine** `IsDeleted`/`DeletedAt`-Felder
- Soft-deleted ChangeLogs (`is_deleted = true`) werden über den globalen
  Query-Filter der `ChangeLogConfiguration` ausgefiltert
- Soft-deleted User werden **nicht** ausgefiltert — der FullName soll auch
  dann aufgelöst werden, wenn der erzeugende User zwischenzeitlich gelöscht
  wurde (via `IgnoreQueryFilters()` auf dem User-Join)
- Kein Change-Log-Eintrag (Read-only)
- Pagination-Wrapper `PagedResult<T>` neu unter
  `KulturHub.Application/Abstractions/Pagination/PagedResult.cs` etabliert
- Validation per `ListChangeLogsRequestValidator` (FluentValidation),
  Ausführung inline im Endpoint, da keine Body-Bindung verfügbar
  (Query-Param-DTO)

### Anpassungen Application-Layer

- Neue Datei `KulturHub.Application/Abstractions/Pagination/PagedResult.cs`:
  `record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Skip, int Take)`
- Neue Datei `KulturHub.Application/Features/Platform/ChangeLogs/ListChangeLogs/`:
  - `ListChangeLogsRequest.cs` — `record(int Skip, int Take, string? Search)`
  - `ListChangeLogsRequestValidator.cs` — Skip≥0, Take∈[1,200], Search≤500
  - `ListChangeLogsCommand.cs` — Bundle `(Guid OrganisationId, int Skip, int Take, string? Search)`
  - `ChangeLogResponse.cs` — exakte Spalten ohne `IsDeleted`/`DeletedAt`
  - `ListChangeLogsHandler.cs` — primary constructor mit `IAppDbContext` +
    `ILogger`, query mit `db.Users.IgnoreQueryFilters()` für FullName-Auflösung

### Anpassungen Api-Layer

- Neue Datei `KulturHub.Api/Endpoints/Platform/ChangeLogEndpoints.cs`:
  `MapChangeLogEndpoints()` mit `MapGroup("/organisations/{organisationId:guid}/change-logs")`
  + `MembershipAuthorizationFilter` + Validation inline
- `app.MapChangeLogEndpoints()` in `Program.cs` registrieren
- HTTP-Beispiel: `KulturHub.Api/http/platform/changelogs/list-organisation-change-logs.http`

### Tests

`KulturHub.UnitTests/Features/Application/Platform/ChangeLogs/ListChangeLogs/ListChangeLogsHandlerTests.cs`:

- `Handle_WhenNoLogs_ShouldReturnEmptyPagedResult`
- `Handle_WithMultipleLogs_ShouldReturnOrderedByCreatedAtDescending`
- `Handle_WithSearch_ShouldMatchInMessage`
- `Handle_WithSearch_ShouldMatchInActorFirstName`
- `Handle_WithSearch_ShouldMatchInActorLastName`
- `Handle_WithSearchWhitespace_ShouldIgnoreSearch`
- `Handle_WithSkipAndTake_ShouldRespectPagination`
- `Handle_OnlyReturnsLogsForRequestedOrganisation`
- `Handle_ExcludesSoftDeletedChangeLogs` (InMemory-Workaround dokumentiert:
  EF InMemory honoriert `HasQueryFilter` nicht zuverlässig in Verbindung mit
  `Join`+`Skip`/`Take`. Mechanik wird über `ListMembershipsHandlerTests`
  gegen den realen Filter und gegen PostgreSQL abgesichert.)
- `Handle_IncludesLogsFromSoftDeletedUser`
- `Handle_PassesCancellationTokenToDb`
- `Handle_ShouldComposeFullNameAndMapResponseFields`

### Doku

- `README.md` Abschnitt 12: Eintrag
  „`GET /organisations/{id}/change-logs` – Change-Logs einer Organisation
  abfragen" unter Plattform-API