# Plan

Diese Datei dient zur besseren Planung von neuen Funktionen:

## User-Story
Als Nutzer
möchte ich eine Liste von Organisationen zu denen ich gehöre erhalten
um zu wissen, für welche Organisationen ich Inhalte erstellen kann

### Akzeptanzkriterien
- Es gibt einen GET Endpunkt, der eine Liste von Organisationen zurückgibt, bei denen der Nutzer einen Membershipeintrag hat
- Der Endpunkt ist in platform/organisations
- route `/mine`
- Response: Id und Name
- Endpunkt ist mit .RA abgesichert.