# Plan

Diese Datei dient zur besseren Planung von neuen Funktionen:

## User-Story
Als Nutzer
möchte ich eine Organisation erstllen
um Inhalte auf der Plattform veröffentlichen zu können

### Akzeptanzkriterien
- Es gibt einen Endpunkt, um eine Organisation zu erstellen
- Er befindet sich in platform/organisations
- Die Datenbank speichert für Organisationen Id, Name, CreatedAt, IsDeleted (DeletedAt und DeletedBy brauchen wir nicht, weil das später im ChangeLog gespeichert wird)
- Für die Zuordnung zwischen Organisation und User gibt es eine memberships-Tabelle
- Die Memberships-Tabelle hat Id, UserId, OrganisationId, JoinedAt und IsDeleted.
- Als Input für den CreateOrganisationHandler werden nur Name (FromBody) und UserId gebraucht
- Response enthält Id, Name und CreatedAt der Organisation
