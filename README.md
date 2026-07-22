# KulturHub

> **Status**: Frühes Stadium. Aktuell implementiert: **Auth** (SignUp mit Invitation-Code, JWT-Authentifizierung, Supabase, Dapper/PostgreSQL).
> Alle weiteren unten beschriebenen Features sind **geplant** und noch nicht im Repo vorhanden.

## Zielbild (geplant)

Automatisiert die wöchentliche Instagram-Kommunikation für Kulturveranstaltungen: Events werden aus der Chayns-API aggregiert, als Carousel-Bilder aufbereitet und automatisch gepostet.

## Voraussetzungen (aktuell)

- .NET 10 SDK
- PostgreSQL
- Supabase-Projekt (Auth + Storage)
- User-Secrets oder `appsettings.json` für `Supabase:Url`, `Supabase:Key`, `Supabase:DiscoveryUrl`, `ConnectionStrings:Default`, `Cors:AllowedOrigins`

## Voraussetzungen (geplant)

- Chayns API-Zugangsdaten
- Instagram Long-Lived Access Token

## Setup (aktuell)

**1. Konfiguration** — `KulturHub.Api/appsettings.json` oder User Secrets befüllen:

```json
{
  "ConnectionStrings": { "Default": "" },
  "Supabase": { "Url": "", "Key": "", "DiscoveryUrl": "" }
}
```

## Setup (geplant)

> **Hinweis**: Die folgenden Schritte beziehen sich auf noch nicht implementierte Features (`KulturHub.Worker`, Datenbank-Migrationen, Instagram-Token-Workflow).

**2. Datenbank** — Migrationen in `/migrations` der Reihe nach ausführen (Ordner und Dateien existieren noch nicht).

**3. Instagram Token** — Einmalig den Workflow in `/http` durchlaufen (Token gegen Long-Lived Token tauschen, User-ID holen, Token in DB einfügen). Danach übernimmt der `TokenRefreshJob` automatisch.

## Starten (aktuell)

```bash
dotnet run --project KulturHub.Api      # API auf http://localhost:5159
dotnet test KulturHub.UnitTests         # Tests (aktuell leer, siehe TestingStrategy.md)
```

## Geplante Startkommandos

```bash
dotnet run --project KulturHub.Worker   # Hintergrundjobs (Worker-Projekt existiert noch nicht)
```

> `Worker:RunImmediately: true` in der Config lässt beide Jobs sofort beim Start ausführen (zum lokalen Testen) — relevant, sobald der `Worker` implementiert ist.
