# KulturHub.Api

Konventionen für die API-Schicht. Hier leben Endpunkte, Filter, OpenAPI-Aufbau
und ASP.NET-Core-Middleware. Implementiert ist **ausschließlich** Minimal API.

## Zweck

ASP.NET Core 10 Host. Verdrahtet DI, exponiert drei OpenAPI-Dokumente,
definiert JSON-Konventionen, führt die Middleware-Pipeline aus und bindet
die Minimal-API-Endpunkte an die Application-Handler.

## Konventionen

- **Endpunkt-Layout** spiegelt die Audienz:
  `Endpoints/Public/`, `Endpoints/Platform/`, `Endpoints/Admin/`.
- **Eine Datei je Endpunkt-Gruppe** als
  `public static class XxxEndpoints` mit Extension-Method
  `public static void MapXxxEndpoints(this IEndpointRouteBuilder app)`.
- **OpenAPI-Gruppe** wird per `.WithGroupName("public|platform|admin")`
  gesetzt (`KulturHub.Api/Endpoints/Platform/MembershipEndpoints.cs`,
  `Endpoints/Admin/InvitationEndpoints.cs`).
- **`WithName("Domain_Action")`** ist PascalCase und stabil
  (`"Invitations_Create"`, `"Memberships_Invite"`,
  `"Auth_DeleteMe"`, `"Organisations_Update"`).
- **Authorisierung** ist **je Endpunkt** per `.RequireAuthorization()` –
  es gibt **keine** globale Vorgabe. Anonyme Endpunkte: `/auth/signup`,
  `/auth/login`, `/auth/validate-invitation`, `/health`.
- **Admin-Authorization** via `.AddEndpointFilter<AdminAuthorizationFilter>()`
  (`Filters/AdminAuthorizationFilter.cs:6-24`), 401/403 mit
  `code: "User.NotAdmin"`.
- **Membership-Authorization** via
  `.AddEndpointFilter<MembershipAuthorizationFilter>()` liest die
  `organisationId` aus den Routen-Parametern und nutzt
  `IMembershipRepository.IsMemberAsync` (`Filters/MembershipAuthorizationFilter.cs:6-35`),
  403 mit `code: "Organisation.NotMember"`.
- **Request-DTOs** in `KulturHub.Api/Requests/` als `record` (überwiegend
  `public sealed record`), camelCase über ASP.NET-Default, **keine**
  `[JsonPropertyName]`-Annotationen, kein eigenes `[JsonStringEnumConverter]`.
- **Response-DTOs** sind die Application-Response-Records – keine Re-Wraps.
- **`http/`-Ordner**: `http-client.env.json` mit `dev: { baseUrl, token }`;
  Unterordner spiegeln `Endpoints/{public,platform,admin}/`; Anfragen nutzen
  `{{baseUrl}}`, `{{token}}` und `@organisationId`-Variablen.
- **`Program.cs`-Pipeline** in genau dieser Reihenfolge:
  1. `DefaultTypeMap.MatchNamesWithUnderscores = true;` (zuerst!).
  2. Service-Reihung: `AddKulturHubJson().AddKulturHubOpenApi().AddApplication().AddInfrastructure(cfg).AddKulturHubAuth(cfg).AddKulturHubCors(cfg)`.
  3. `AddAuthorization()`.
  4. Im Development: `MapOpenApi("/openapi/{documentName}.json")` +
     `MapScalarApiReference(...)` mit drei `AddDocument(...)`-Aufrufen.
  5. Middleware: `UseKulturHubExceptionHandler()` → `UseHttpsRedirection()`
     → `UseCors()` → `UseAuthentication()` → `UseAuthorization()`.
  6. Endpunkte: `MapHealthEndpoints()`, `MapAuthEndpoints()`,
     `MapInvitationEndpoints()`, `MapOrganisationEndpoints()`,
     `MapMembershipEndpoints()`, `MapChangeLogEndpoints()`.

## Patterns

- **Result-Bindung** identisch in jedem Handler:
  `return result.Match(response => Results.Ok(response), errors => errors.ToResult());`
  (`MembershipEndpoints.cs:46-62`, `OrganisationEndpoints.cs`).
- **Neu erstellte Ressourcen** antworten mit
  `Results.Created($"/…/{id}", response)`.
- **`Delete`** antwortet mit `Results.NoContent()` für `ErrorOr<Deleted>`.
- **`CancellationToken ct`** ist immer der letzte Lambda-Parameter und ist
  an `HttpContext.RequestAborted` gebunden.
- **JSON-Konvention**: `JsonStringEnumConverter` global in
  `Extensions/JsonServiceCollectionExtensions.cs:9-12`. Kein Per-Type
  `[JsonStringEnumConverter]`.
- **CORS** aus `Cors:AllowedOrigins[]` mit `AllowAnyHeader/Method` +
  `AllowCredentials` (`Extensions/CorsServiceCollectionExtensions.cs:9-19`).
- **JWT-Bearer** mit `Authority = Supabase:DiscoveryUrl`,
  `ValidAudience = "authenticated"`, `ClockSkew = 30s`,
  `JwtSecurityTokenHandler.DefaultMapInboundClaims = false`
  (`Extensions/AuthServiceCollectionExtensions.cs:9-32`).
- **`ErrorExtensions.ToResult(this List<Error>)`** mappt:
  - `Validation` → `Results.ValidationProblem(...)`
  - `NotFound` → 404, `Conflict` → 409, `Unauthorized` → 401,
    `Forbidden` → 403, alles andere → 500
  - Nicht-Validation-Ergebnisse tragen `extensions: ["code"] = firstError.Code`.
- **Claims** werden über `ClaimsPrincipalExtensions.GetUserId()` gelesen
  – diese Methode greift auf den `sub`-Claim zu (kein
  `ClaimTypes.NameIdentifier`, weil `DefaultMapInboundClaims = false`).
- **Drei OpenAPI-Dokumente** (`public`, `platform`, `admin`) sind über
  `OpenApiServiceCollectionExtensions.AddKulturHubOpenApi` registriert und
  jeweils mit `BearerSecurityDocumentTransformer` als Bearer-HTTP-Security
  ausgestattet.
- **OpenAPI/Scalar-Kopplung**: Jeder neue Dokumentname muss sowohl in
  `AddKulturHubOpenApi` als auch in `MapScalarApiReference(...)` ergänzt
  werden.

## Pitfalls

- **Kein** `RequireAuthorization()` global – pro Endpunkt entscheiden.
- **Kein** Re-Mapping der JWT-Claims. `sub` bleibt `sub`.
- **Kein** Wechsel von `ValidAudience` weg von `"authenticated"`.
- **Keine** Controller. Das Projekt ist Minimal-API-only.
- **Keine** Aufrufe von `IUserRepository`/`IMembershipRepository` im Handler
  zur Auth-Prüfung – das ist Aufgabe der Filter.
- **Keine** neuen OpenAPI-Dokumente ohne Scalar-Coupling.
- **Keine** lokalisierten `WithName`-Strings – sie sind Identifier.
- **Keine** handgebauten `Results.Problem(...)` in Endpunkten – immer
  `errors.ToResult()`.
- **Kein** Entfernen von `UseKulturHubExceptionHandler` – sonst gibt es
  keinen geordneten 500-Pfad.
- **Keine** `.http`-Dateien außerhalb der `http/{public,platform,admin}/`-
  Struktur (Rider gruppiert sonst nicht korrekt).

## AI-Workflow

1. **Lesen**: Den existierenden Endpunkt, das `Request`/`Response`-DTO,
   den Handler, die Filter (`AdminAuthorizationFilter`,
   `MembershipAuthorizationFilter`) und die `ErrorExtensions` lesen.
2. **Regeln extrahieren**: Audience (public/platform/admin), Auth-Anforderung,
   Filter, Status-Codes, Error-Codes und OpenAPI-Metadaten dokumentieren.
3. **Szenario-Tabelle**: Happy Path + jede `ErrorOr`-Failure +
   401/403/404/409.
4. **Implementieren**: Erst `Request`-Record, dann Endpoint mit
   `RequireAuthorization()` + Filter(n), dann OpenAPI-Metadaten, dann
   ggf. ein `.http`-Beispiel.
5. **Verifizieren**: `dotnet build`, Endpunkt manuell über Scalar
   `/scalar` und `.http`-Beispiele ausprobieren, `dotnet test` darf nicht
   brechen.
