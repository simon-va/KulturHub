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
│   ├── JsonServiceCollectionExtensions.cs
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

| Dokument | Audience | Inhalt |
| --- | --- | --- |
| `public` | Anonyme Besucher | `GET /health`, künftige Public-Reads |
| `platform` | Angemeldete Nutzer | Eigene Organisationen, Memberships, Change Logs |
| `admin` | Administratoren | Systemweite Verwaltung, Invitations |

- Endpunkte wählen das Dokument per `.WithGroupName(...)` (Werte:
  `"public"`, `"platform"`, `"admin"`).
- Dokumente liegen unter `/openapi/{documentName}.json`.
- Scalar ist im Development-Modus unter `/scalar` erreichbar.

## Endpoint-Konvention

```csharp
public static class CreateOrganisationEndpoint
{
    public static IEndpointRouteBuilder MapCreateOrganisation(this IEndpointRouteBuilder app)
    {
        app.MapPost("/organisations", CreateOrganisation)
            .WithName("CreateOrganisation")
            .WithGroupName("platform")
            .WithSummary("Legt eine neue Organisation an.")
            .RequireAuthorization()
            .AddValidationFilter<CreateOrganisationRequest>();

        return app;
    }

    private static async Task<IResult> CreateOrganisation(
        CreateOrganisationRequest request,
        IValidator<CreateOrganisationRequest> validator,
        CreateOrganisationHandler handler,
        CancellationToken ct)
    {
        // Validation passiert im Filter; hier nur Handler-Aufruf.
        var result = await handler.HandleAsync(request, ct);
        return result.Match(Results.Created, errors => errors.ToProblemResult());
    }
}
```

- Pro Use-Case eine eigene statische Klasse mit `Map...`-Methode.
- Endpoints werden in `Program.cs` über die jeweiligen
  `Map<Irgendwas>Endpoint()`-Methoden registriert.
- Endpoints liefern `IResult` zurück, niemals direkte DTOs. Das
  Mapping passiert zentral in `ErrorExtensions.cs`.

## Mapping ErrorOr → HTTP

- `error.ToProblemResult()` zentral in `ErrorExtensions.cs`.
- Mapping:

| Error-Typ | HTTP-Status |
| --- | --- |
| `Error.Validation` | 400 |
| `Error.Unauthorized` | 401 |
| `Error.Forbidden` | 403 |
| `Error.NotFound` | 404 |
| `Error.Conflict` | 409 |
| alles andere | 500 |

- Format: `application/problem+json` (RFC 7807).

## Auth

- **JWT-Bearer** über Supabase OIDC.
- Discovery-URL liegt in der Konfiguration
  (`Supabase:DiscoveryUrl`); Tokens werden gegen den ausgestellten
  Signing-Key validiert.
- Der `sub`-Claim ist der stabile Nutzer-Identifier; er wird in der
  Application-Schicht als `UserId` verwendet.
- `AddKulturHubAuth(...)` kapselt die JWT-Konfiguration.

## CORS

- Erlaubte Origins kommen aus der Konfiguration (`Cors:AllowedOrigins`).
- Im Dev ist standardmäßig `http://localhost:4200` (Angular) erlaubt.

## JSON

- Property-Naming: **camelCase** im Output, PascalCase im C#-Code.
- `JsonSerializerOptions.DefaultIgnoreCondition`
  `WhenWritingNull`.
- `AddKulturHubJson(...)` kapselt die JSON-Konfiguration.

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
- Beispiel: `KulturHub.Api/http/platform/create-organisation.http`.

## Was hier **nicht** hineingehört

- Keine Business-Logik (gehört in Handler)
- Keine direkten DbContext-Aufrufe (gehört in Handler/Application)
- Keine Validatoren (gehört in Application)
- Keine Entity-Mappings (gehört in Infrastructure-Konfigurationen)
