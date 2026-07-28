# Plan

Diese Datei dient zur besseren Planung von neuen Funktionen:

## User-Story
Als Nutzer
möchte ich Daten meiner Organisation ändern
um diese aktuell zu halten

### Akzeptanzkriterien
- Es gibt einen PUT Endpunkt, um die Informationen zu einer Organisation (aktuell nur Name) zu aktualisieren
- Es wird ein Changelog erstellt
- Der Endpunkt hat .RequireAuthorization
- Wir brauchen einen Filter, der Prüft, ob der User auch in der Organisation ist, für die er das Update durchführen möchte (Code aus V1 wurde für Inspiration eingefügt)
- Der Endpunkt liegt in platform/organisations