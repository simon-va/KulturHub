# Plan

Diese Datei dient zur besseren Planung von neuen Funktionen:

## User-Story
Als Admin
möchte ich als einziger die Endpunkte aus der Admin Domäne aufrufen können
um Unberechtigten den Zugriff zu verwehren

### Akzeptanzkriterien
- Die Endpunkte in Admin Domäne sind mit RequireAuthorization abgesichert, um einen JWT zu erzwingen
- Es gibt einen AdminAuthorizationFilter, der für den Endpunkt einen Admin Check durchführt.
