# Plan

Diese Datei dient zur besseren Planung von neuen Funktionen:

## User-Story
Als Nutzer
möchte ich einen Account erstellen können
um Zugriff auf die Plattform zu erlangen

### Akzeptanzkriterien
- Es gibt einen SignUp Endpunkt, der einen User anlegt
- Er ist in der Plattform-Domäne unter Auth Endpoints
- Request-Daten: Email, Password, FirstName, LastName, InvitationCode
- Request-daten werden validiert
- Der Invitationcode wird auf seine Gültigkeit geprüft
- supabase User wird erstellt 
- User wird in eigener Tabelle gespeichert: Id (von Supabase), Email, FirstName, LastName, IsAdmin
- IsAdmin ist false (wird von Entwicklern direkt in der Datenbank gesetzt)
- Wenn user in eigener Datenbank nicht angelegt werden kann, muss der User bei supabase wieder gelöscht werden
- Wenn User erfolgreich angelegt wurde, muss der InvitationCode als "used" Markiert werden. Neues Feld UsedBy das die UserId referenziert
- SignUpResponse enthält AccessToken, RefreshToken, die neue UserId, FirstName und LastName
