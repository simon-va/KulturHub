# KulturHub.Api

Enthält HTTP-Endpunkte (Minimal API), ruft Handler aus dem
Application-Layer auf und gibt Responses zurück.

## Aufgaben

- Routing und HTTP-Binding
- Authentifizierung und Autorisierung
- Request-Validierung (FluentValidation, vor Handler-Aufruf) — die
  fachlichen Regeln lebt im Application-Layer (siehe
  `Application-instructions.md` → Validierung)
- OpenAPI-Dokumentation
- Mapping von `ErrorOr<T>` auf HTTP-Antworten
- Querschnittsthemen: Logging, CORS, Exception Handling

## Ordnerstruktur

```
KulturHub.Api/
├── Program.cs                       # Pipeline, Service-Registrierung
├── appsettings.json
├── appsettings.Development.json
├── Extensions/                      # IServiceCollection-Extensions
├── Filters/                         # Endpoint-Filter
├── Endpoints/
│   ├── Public/
│   ├── Platform/
│   └── Admin/
└── http/                            # .http-Beispielrequests
```

## OpenAPI je Bereich

Drei separate OpenAPI-Dokumente, auswählbar in Scalar:

| Dokument   | Audience           | Inhalt                                          |
|------------|--------------------|-------------------------------------------------|
| `public`   | Anonyme Besucher   | `GET /health`, künftige Public-Reads            |
| `platform` | Angemeldete Nutzer | Eigene Organisationen, Memberships, Change Logs |
| `admin`    | Administratoren    | Systemweite Verwaltung, Invitations             |

- Endpoints wählen das Dokument per `.WithGroupName(...)` (Werte:
  `"public"`, `"platform"`, `"admin"`).
- Dokumente liegen unter `/openapi/{documentName}.json`.
- Scalar ist im Development-Modus unter `/scalar` erreichbar.

## Endpoint-Konvention: eine Datei pro REST-Ressource

Endpoints werden **ressourcenorientiert** organisiert. Alle HTTP-Endpoints
einer Ressource leben in **einer** statischen Klasse
(`<Resource>Endpoints.cs`) und werden über **eine** `Map<Resource>Endpoints`-
Methode registriert.

```csharp
public static class InvitationEndpoints
{
    public static IEndpointRouteBuilder MapInvitationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/invitations")
            .WithTags("Invitations")
            .WithGroupName("admin");

        group.MapPost("/", async ([FromServices] CreateInvitationHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(ct);

            return result.Match(
                response => Results.Json(response, statusCode: StatusCodes.Status201Created),
                errors => errors.ToResult());
        })
            .Produces<CreateInvitationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithName("Invitations_Create");

        return app;
    }
}
```

- `MapGroup` wird **einmal pro Ressource** gesetzt. `WithTags(...)` und
  `WithGroupName(...)` erben durch die Gruppe — **nicht** erneut pro
  Endpoint aufrufen.
- Endpoints sind **inline als Lambda** geschrieben. Wenn der Body länger
  als ~10 Zeilen wird, lagert man Handler-Aufrufe in eine private
  Hilfsmethode **derselben Klasse** aus (keine eigene Endpoint-Klasse).
- Handler-Methoden heißen durchgängig **`HandleAsync(...)`**.
- `WithName`-Strings folgen dem Muster **`<Resource>_<Verb>`**,
  z. B. `"Invitations_Create"`, `"Memberships_Delete"`.
- Pro Endpoint werden alle erwarteten Status-Codes via
  `.Produces(...)` / `.ProducesProblem(...)` deklariert (Erfolg +
  401/403/404/409 nach Bedarf).

### `[FromServices]` an jedem DI-getriebenen Lambda-Parameter

Minimal APIs versuchen standardmäßig, jeden Lambda-Parameter aus dem
Request-Body zu deserialisieren. Erst danach fällt die Binding-Logik
auf den DI-Container zurück. Für komplexe Typen wie einen Handler
schlägt die JSON-Deserialisierung fehl
(`System.InvalidOperationException: Each parameter in the deserialization
constructor on type '...' must bind to an object property or field on
deserialization.`).

Lösung: jeder DI-getriebene Lambda-Parameter bekommt explizit
`[FromServices]` aus `Microsoft.AspNetCore.Mvc`:

```csharp
group.MapPost("/", async (
    [FromServices] CreateInvitationHandler handler,
    CancellationToken ct) => { ... });
```

`CancellationToken` und einfache Bindings aus Route/Query brauchen
kein `[FromServices]`.

## Varianten für Endpunkte

- **Kein Request-Body:** Inline-Lambda nimmt nur den Handler und
  `CancellationToken`. Es gibt keine `Request`-Klasse im
  Application-Layer und keinen `AddValidationFilter<>`. Beispiel oben.
- **Kein Auth:** kein `.RequireAuthorization()` und kein
  `AddEndpointFilter<>`.
- **Auth via `RequireAuthorization()`:** Standard-Mechanismus. Reicht
  aus, solange nur „authentifiziert oder nicht" geprüft werden muss.
- **Rollen-/Org-Autorisierung:** per Endpoint-Filter
  (`AdminAuthorizationFilter`, `MembershipAuthorizationFilter`).

### 201 Created ohne Location-Header

Wenn ein `POST` keine `GET`-Ressource-URL liefern kann, wird
**keine** Location-URL generiert. Statt `Results.Created(uri, response)`
nutzt man `Results.Json(response, statusCode: StatusCodes.Status201Created)`.

## Mapping ErrorOr → HTTP

`errors.ToResult()` liegt zentral in `Extensions/ErrorExtensions.cs`.

| Error-Typ            | HTTP-Status |
|----------------------|-------------|
| `Error.Validation`   | 400         |
| `Error.Unauthorized` | 401         |
| `Error.Forbidden`    | 403         |
| `Error.NotFound`     | 404         |
| `Error.Conflict`     | 409         |
| alles andere         | 500         |

Format: `application/problem+json` (RFC 7807).

```csharp
return result.Match(
    response => Results.Json(response, statusCode: StatusCodes.Status201Created),
    errors => errors.ToResult());
```

## Filters (Mechanismus)

- Unter `Filters/` liegen Endpoint-Filter
  (`ValidationFilter<TRequest>`, `AdminAuthorizationFilter`,
  `MembershipAuthorizationFilter`).
- Eingehängt per `.AddEndpointFilter<TFilter>()` — bevorzugt auf der
  `MapGroup(...)` der Ressource, sodass jeder Endpoint der Ressource
  den Filter erbt.
- **Reihenfolge:** `.RequireAuthorization()` zuerst (401 ohne Body bei
  fehlendem JWT), danach `.AddEndpointFilter<AdminAuthorizationFilter>()`
  (DB-Lookup via `IUserAdminReader` und 403 bei fehlender Rolle).

`Organisations_Update` ist eine bewusste Ausnahme: die Route liegt unter
`/organisations/{id}` und nicht unter
`/organisations/{id}/memberships`, also wird der
`MembershipAuthorizationFilter` per Endpoint statt per Group
angehängt.

## Auth

- **JWT-Bearer** über Supabase OIDC.
- Discovery-URL liegt in der Konfiguration (`Supabase:DiscoveryUrl`);
  Tokens werden gegen den ausgestellten Signing-Key validiert.
- `sub`-Claim ist der stabile Nutzer-Identifier; er wird im Endpoint
  via `ClaimsPrincipal.GetUserId()` (`Extensions/ClaimsPrincipalExtensions.cs`)
  extrahiert und in den Command/Request geschrieben.
- Handler rufen **nicht** selbst `auth.GetCurrentUserId()` auf — siehe
  `Application-instructions.md` → Identity-Threading.
- `AddKulturHubAuth(...)` in `Extensions/AuthServiceCollectionExtensions.cs`
  kapselt die JWT- und Supabase-Konfiguration.

## CORS

Erlaubte Origins kommen aus der Konfiguration
(`Cors:AllowedOrigins`) und werden in `appsettings.Development.json`
bzw. `appsettings.Production.json` pro Umgebung gepflegt — nicht im
Code.

- Dev: `http://localhost:4200` (Angular)
- Prod: `https://kibuu.de`, `https://www.kibuu.de`, `https://api.kibuu.de`

In `Program.cs` läuft `UseCors()` **vor** `UseHttpsRedirection()`, damit
Preflight-OPTIONS-Antworten nicht durch den Redirect verschluckt werden.

## JSON

Es gibt keine eigene `AddKulturHubJson(...)`-Konfiguration.
ASP.NET-Core-Defaults: camelCase-Output, PascalCase im C#-Code. Wenn
später `WhenWritingNull` o. Ä. nötig wird, kommt eine zentrale
Erweiterung dazu.

## Pipeline-Reihenfolge (`Program.cs`)

```
UseKulturHubExceptionHandler()  // fängt unbehandelte Exceptions
UseCors()                       // vor Redirect, damit Preflight-OPTIONS nicht verschluckt werden
UseHttpsRedirection()
UseAuthentication()
UseAuthorization()
```

`MapOpenApi` und `MapScalarApiReference` werden nur in
`IsDevelopment()` registriert.

## Beispielrequests

Pro Bereich ein Unterordner in `KulturHub.Api/http/<bereich>/`. Datei­
endung `.http`, ausführbar mit der VS-Code-Extension *REST Client*
oder JetBrains Rider.

## Logging

`WebApplication.CreateBuilder(...)` registriert die
Default-Logging-Provider (`Console`, `Debug`, `EventSource`)
automatisch. Weitere Senken (Serilog, OTel) werden bei Bedarf in
`Program.cs` aktiviert, ohne dass Handler angepasst werden müssen — sie
nutzen ausnahmslos `ILogger<T>`.

## Was hier **nicht** hineingehört

- Keine Business-Logik (Handler)
- Keine direkten DbContext-Aufrufe
- Keine Validatoren
- Keine Entity-Mappings
- Keine `Request`-Klasse, wenn der Endpoint keinen Body erwartet
- Keine `Results.Created(uri, ...)` ohne existierenden `GET`-Endpoint
- Keine separate `<Verb><Resource>Endpoint.cs`-Datei pro Use-Case
- Keine doppelten `.WithGroupName(...)` pro Endpoint
- Keine DI-getriebenen Lambda-Parameter ohne `[FromServices]`
