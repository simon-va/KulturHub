# KulturHub

Automatisiert die wöchentliche Instagram-Kommunikation für Kulturveranstaltungen: Events werden aus der Chayns-API aggregiert, als Carousel-Bilder aufbereitet und automatisch gepostet.

## Voraussetzungen

- .NET 10 SDK
- PostgreSQL
- Supabase-Projekt (Storage)
- Chayns API-Zugangsdaten
- Instagram Long-Lived Access Token (siehe unten)

## Setup

**1. Konfiguration** — `appsettings.json` oder User Secrets befüllen:

```json
{
  "ConnectionStrings": { "Default": "" },
  "Chayns": { "SiteId": "", "PageId": "", "EventSiteId": "", "EventPageId": "", "EventLocationId": "", "Username": "", "Password": "" },
  "Supabase": { "Url": "", "Key": "" }
}
```

**2. Datenbank** — Migrationen in `/migrations` der Reihe nach ausführen.

**3. Instagram Token** — Einmalig den Workflow in `/http` durchlaufen (Token gegen Long-Lived Token tauschen, User-ID holen, Token in DB einfügen). Danach übernimmt der `TokenRefreshJob` automatisch.

## Starten

```bash
dotnet run --project KulturHub.Api      # API auf http://localhost:5159
dotnet run --project KulturHub.Worker   # Hintergrundjobs
dotnet test KulturHub.UnitTests         # Tests
```

> `Worker:RunImmediately: true` in der Config lässt beide Jobs sofort beim Start ausführen (zum lokalen Testen).
