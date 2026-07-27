# Plan

Diese Datei dient zur besseren Planung von neuen Funktionen:

## User-Story
Als Entwickler
möchte ich einen Invitation-Code erstellen können
um neuen Nutzern die Registrierung zu ermöglichen

### Akzeptanzkriterien
- Es gibt einen Endpunkt, um einen InvitationCode zu erzeugen.
- Er befindet sich im Admin Schema
- Da wir noch keine User haben, ist er auch nicht abgesichert
- Für die invitation_codes Tabelle brauchen wir erstmal eine Id, Code, CreatedAt, ExpiresAt, IsDeleted, DeletedAt
- Format ist ´XXX-XXX´ mit großen Buchstaben und Zahlen, außer 0, O, I und 1
