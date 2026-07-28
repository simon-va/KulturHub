# KulturHub.Api

Enthält:

- **HTTP-Endpunkte** (Minimal API)
- ruft **Handler** aus dem Application-Layer auf
- gibt **Responses** zurück

## Aufgaben

- Routing und HTTP-Binding
- Authentifizierung und Autorisierung
- Validierung der Requests (FluentValidation, vor Handler-Aufruf)
- OpenAPI-Dokumentation
- Mapping von `ErrorOr<T>`-Ergebnissen auf HTTP-Antworten
- Querschnittsthemen: Logging, CORS, JSON, Exception Handling

## Validierung — was hier passiert (und was nicht)

Diese Schicht ist **nicht** für fachliche Korrektheit zuständig. Sie ist
die **Eingangsvalidierung** für ankommende API-Requests.

- **Hier:** Form, Shape, Pattern, Längen, Pflichtfelder. Ausgeführt im
  `ValidationFilter<TRequest>` per FluentValidation. Antwort: 400 mit
  Property-bezogener Fehlerliste (`Results.ValidationProblem(...)`).
- **Nicht hier:** fachliche Regeln, die System-Zustand brauchen
  (Eindeutigkeit, Existenz, Berechtigung), Domain-Invarianten,
  Authentifizierung.

Die Trennung im Detail ist in den Layer-Instructions beschrieben:
- [`KulturHub.Application/Application-instructions.md`](../Application/Application-instructions.md) → „Validierung"
- [`KulturHub.Domain/Domain-instructions.md`](../Domain/Domain-instructions.md) → „Validierungs-Schichten"

## Verschmelzung

- **Application-Handler werden direkt aufgerufen** – keine weitere
  Service-Schicht.
- Validation läuft im Endpoint-Filter; bei `IsValid == false` wird mit
  `ProblemDetails` (400) geantwortet und der Handler nicht aufgerufen.
- **Authentifizierter User-Id** wird im Endpoint aus
  `HttpContext.User` extrahiert und in den Request geschrieben oder via
  `IAuthProvider` an den Handler gereicht.
- Endpoints sind bewusst dünn. Geschäftslogik gehört in Handler.

## Ordnerstruktur

```
KulturHub.Api/
├── Program.cs                       # Pipeline, Service-Registrierung
├── appsettings.json
├── appsettings.Development.json
├── Extensions/                      # I-ServiceCollection-Extensions
│   ├── ApplicationBuilderExtensions.cs
│   ├── AuthServiceCollectionExtensions.cs
│   ├── ClaimsPrincipalExtensions.cs
│   ├── CorsServiceCollectionExtensions.cs
│   ├── ErrorExtensions.cs
│   └── OpenApiServiceCollectionExtensions.cs
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

Endpoints werden **ressourcenorientiert** organisiert. Das heißt: alle
HTTP-Endpoints einer Ressource leben in **einer** statischen Klasse
(`<Resource>Endpoints.cs`) und werden über **eine** `Map...`-Methode
(`Map<Resource>Endpoints`) registriert.

Beispiel — `InvitationEndpoints.cs`:

```csharp
using KulturHub.Api.Extensions;
using KulturHub.Application.Features.Admin.Invitations.CreateInvitation;
using Microsoft.AspNetCore.Mvc;

namespace KulturHub.Api.Endpoints.Admin.Invitations;

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
            .WithName("Invitations_Create");

        return app;
    }
}
```

### `[FromServices]` an jedem DI-getriebenen Lambda-Parameter

Minimal APIs versuchen standardmäßig, **jeden** Lambda-Parameter aus dem
Request-Body zu deserialisieren. Erst danach fällt die Binding-Logik auf
den DI-Container zurück. Für komplexe Typen wie einen Handler
schlägt die JSON-Deserialisierung fehl
(`System.InvalidOperationException: Each parameter in the deserialization
constructor on type '...' must bind to an object property or field on
deserialization.`).

Lösung: jeder DI-getriebene Lambda-Parameter — also Handler,
zusätzliche Services, Repositorys — bekommt explizit `[FromServices]`
aus dem Namespace `Microsoft.AspNetCore.Mvc`:

```csharp
group.MapPost("/", async (
    [FromServices] CreateInvitationHandler handler,
    CancellationToken ct) => { ... });
```

`CancellationToken` und einfache Bindings aus Route/Query brauchen
kein `[FromServices]` — der Bindings-Mechanismus erkennt sie von selbst.

### Warum eine Datei pro Ressource?

- **`MapGroup`** bündelt das gemeinsame Routing (`/admin/invitations`),
  die Tags und die OpenAPI-Gruppe einmal für die ganze Ressource.
  Doppelte `.WithGroupName(...)`-Aufrufe pro Endpoint entfallen.
- Wenn ein zweiter Endpoint für dieselbe Ressource dazukommt
  (z. B. `GET /admin/invitations`, `DELETE /admin/invitations/{id}`),
  wandert er **in dieselbe Datei**. Cohesion schlägt Datei-Größe.
- Der `Map<Resource>Endpoints`-Aufruf ist die einzige Stelle in
  `Program.cs`, an der die Ressource auftaucht.

### Konventionen innerhalb der Ressource

- `MapGroup` wird **einmal pro Ressource** gesetzt.
  `WithTags(...)` und `WithGroupName(...)` müssen nicht erneut pro
  Endpoint aufgerufen werden — sie erben durch die Gruppe.
- Endpoints sind **inline als Lambda** geschrieben. Wenn der Body
  länger als ~10 Zeilen wird, lagert man die Handler-Aufrufe in eine
  private Hilfsmethode **derselben Klasse** aus (nicht in eine eigene
  Endpoint-Klasse).
- Handler-Methoden heißen durchgängig **`HandleAsync(...)`** — auch
  dann, wenn sie Querys, Requests oder nur IDs entgegennehmen. Das
  hält den Aufruf-Site-Code uniform.
- `WithName`-Strings folgen dem Muster **`<Resource>_<Verb>`**, z. B.
  `"Invitations_Create"`, `"Invitations_List"`, `"Invitations_Delete"`.
- Pro Endpoint werden alle erwarteten Status-Codes via `.Produces(...)`
  bzw. `.ProducesProblem(...)` deklariert (mindestens die Erfolgs- und
  die häufigsten Fehlerfälle 401/403/404/409).

### `Produces` für Fehlerfälle

Das Snippet aus dem Vorgänger-Code deklariert systematisch die
Fehlerstatus-Codes:

```csharp
.ProducesProblem(StatusCodes.Status401Unauthorized)
.ProducesProblem(StatusCodes.Status403Forbidden)
.ProducesProblem(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status409Conflict)
```

Diese Liste ist Bestandteil des Endpoints und gehört in die
Ressource-Datei. Sie macht den OpenAPI-Contract vollständig und
ermöglicht Scalar/REST-Client-Tools, Fehlerantworten beim
„Try it out" korrekt zu modellieren.

## Varianten für Endpunkte ohne Body, ohne Auth oder ohne Validator

**Kein Request-Body:** Inline-Lambda nimmt nur den Handler und
`CancellationToken`. Der Handler hat entsprechend keine
Request-Parameter (`HandleAsync(CancellationToken)`). Es gibt keine
`Request`-Klasse im Application-Layer und keinen
`AddValidationFilter<>`. Beispiel oben.

**Kein Auth:** wenn der Endpoint nicht durch Auth geschützt ist
(z. B. weil noch keine User existieren), wird schlicht kein
`.RequireAuthorization()` und kein `AddEndpointFilter<>` aufgerufen.

**Auth via `RequireAuthorization()`:** der Standard-Mechanismus. Er
reicht aus, solange nur „authentifiziert oder nicht" geprüft werden
muss.

**Autorisierungs-Filter:** für rollen- oder organisationsbezogene
Checks (z. B. „nur Admins", „nur Mitglieder einer Org"). Siehe
unten — der Mechanismus ist in der Doku erwähnt, aber die konkreten
Filter-Klassen werden reaktiviert, sobald die Auth-User-Story
umgesetzt wird.

### 201 Created ohne Location-Header

Wenn ein `POST`-Endpoint eine Ressource erzeugt, aber kein `GET`-Endpoint
existiert, der die Ressource unter einer konkreten URL zurückgibt, wird
**keine** Location-URL generiert. Statt
`Results.Created(uri, response)` wird
`Results.Json(response, statusCode: StatusCodes.Status201Created)`
benutzt. Der 201-Status bleibt erhalten, der Body enthält die volle
Response. Sobald der passende `GET`-Endpoint existiert, kann zurück auf
`Results.Created(uri, response)` gewechselt werden.

## Mapping ErrorOr → HTTP

- `error.ToResult()` zentral in `ErrorExtensions.cs`.
- Mapping:

| Error-Typ            | HTTP-Status |
|----------------------|-------------|
| `Error.Validation`   | 400         |
| `Error.Unauthorized` | 401         |
| `Error.Forbidden`    | 403         |
| `Error.NotFound`     | 404         |
| `Error.Conflict`     | 409         |
| alles andere         | 500         |

- Format: `application/problem+json` (RFC 7807).

### Beispiel mit `Match`

```csharp
return result.Match(
    response => Results.Json(response, statusCode: StatusCodes.Status201Created),
    errors => errors.ToResult());
```

`errors.ToResult()` produziert eine ProblemDetails-Antwort nach der
Tabelle oben. Im OpenAPI-Dokument taucht diese Fehlermodellierung
durch die `.ProducesProblem(...)`-Aufrufe im Endpoint auf.

## Filters (Mechanismus)

- Unter `KulturHub.Api/Filters/` liegen Endpoint-Filter (z. B.
  `AdminAuthorizationFilter`, `MembershipAuthorizationFilter`,
  `ValidationFilter<TRequest>`).
- Sie werden in der Endpoint-Pipeline per
  `.AddEndpointFilter<TFilter>()` eingehängt.
- Der bevorzugte Einsatzort ist die `MapGroup(...)` der jeweiligen
  Ressource — so erbt jeder Endpoint dieser Ressource den Filter
  automatisch.
- **Reihenfolge:** `.RequireAuthorization()` zuerst (erzwingt gültigen
  JWT und antwortet mit 401 ohne Body), danach
  `.AddEndpointFilter<AdminAuthorizationFilter>()` (DB-Lookup via
  `IUserAdminReader` und 403 bei fehlender Admin-Rolle). So lehnt die
  JWT-Pipeline bereits nicht authentifizierte Requests ab, bevor der
  Filter überhaupt einen DB-Roundtrip macht.

## Auth

- **JWT-Bearer** über Supabase OIDC.
- Discovery-URL liegt in der Konfiguration
  (`Supabase:DiscoveryUrl`); Tokens werden gegen den ausgestellten
  Signing-Key validiert.
- Der `sub`-Claim ist der stabile Nutzer-Identifier; er wird in der
  Application-Schicht als `UserId` verwendet.
- `AddKulturHubAuth(...)` kapselt die JWT-Konfiguration.

## CORS

- Erlaubte Origins kommen aus der Konfiguration
  (`Cors:AllowedOrigins`).
- Im Dev ist standardmäßig `http://localhost:4200` (Angular) erlaubt.

## JSON

- Property-Naming: **camelCase** im Output, PascalCase im C#-Code.
- `JsonSerializerOptions.DefaultIgnoreCondition` `WhenWritingNull`.
- Die JSON-Konfiguration erfolgt über die ASP.NET-Core-Defaults; eine eigene `AddKulturHubJson(...)` ist aktuell nicht nötig.

## Pipeline-Reihenfolge (`Program.cs`)

```
UseKulturHubExceptionHandler()  // fängt unbehandelte Exceptions
UseHttpsRedirection()
UseCors()
UseAuthentication()
UseAuthorization()
```

`MapOpenApi` und `MapScalarApiReference` werden nur in `IsDevelopment()`
registriert.

## Beispielrequests

- Pro Bereich ein Unterordner in `KulturHub.Api/http/<bereich>/`.
- Dateiendung `.http`, ausführbar mit der VS-Code-Extension
  *REST Client* oder JetBrains Rider.
- Beispiel: `KulturHub.Api/http/admin/create-invitation.http`.

## Logging in der API

`WebApplication.CreateBuilder(...)` registriert die
Default-Logging-Provider (`Console`, `Debug`, `EventSource`) automatisch.
Weitere Senken (Serilog, OTel) werden bei Bedarf in `Program.cs`
aktiviert, ohne dass Handler angepasst werden müssen — sie nutzen
ausnahmslos `ILogger<T>`.

## Was hier **nicht** hineingehört

- Keine Business-Logik (gehört in Handler)
- Keine direkten DbContext-Aufrufe (gehört in Handler/Application)
- Keine Validatoren (gehört in Application, sofern der Request Daten hat)
- Keine Entity-Mappings (gehört in Infrastructure-Konfigurationen)
- Keine `Request`-Klasse, wenn der Endpoint keinen Body erwartet
- Keine `Results.Created(uri, ...)` ohne existierenden `GET`-Endpoint
- Keine separate `<Verb><Resource>Endpoint.cs`-Datei pro Use-Case
  (eine Datei pro Ressource reicht)
- Keine doppelten `.WithGroupName(...)`-Aufrufe pro Endpoint (sie
  erben durch die `MapGroup`)
- Keine DI-getriebenen Lambda-Parameter ohne `[FromServices]` (ohne
  den Attribut versucht der Body-Binder den Handler per JSON zu
  deserialisieren und wirft eine `InvalidOperationException`)
