# Plan

Diese Datei dient zur besseren Planung von neuen Funktionen:

## User-Story
Als Nutzer
möchte ich meine Aktivitäten über ChangeLogs dokumentieren
um diese später nachvollziehen zu können 

### Akzeptanzkriterien
- Wenn eine Organisation erstellt wird, soll ein ChangeLog geschrieben werden.
- Es gibt eine neue Tabelle change_logs mit den Feldern Id, OrganisationId, CreatedBy, Message, Data, CreatedAt und IsDeleted
- Alle Felder müssen ausgefüllt sein
- IsDeleted wird später nur genutzt, wenn eine Organisation gelöscht wird, wird daher beim Erstellen immer mit false erstellt
- Data ist ein JSON Feld, in das dynamisch Werte geschrieben werden können.
- Die Message ist beim Erstellen der Organisation "Organisation wurde erstellt" und in Data steht der Name