# Plan

Diese Datei dient zur besseren Planung von neuen Funktionen:

## User-Story
Als Nutzer
möchte ich weitere Nutzer zu meiner Organisation hinzufügen können
um nicht alleine die Organisation zu verwalten

### Akzeptanzkriterien
- Es gibt einen Post Endpunkt, um einen Nutzer zur Organisation einzuladen
- Endpunkt ist in platform/memberships
- Route /invite
- Per .RequireAuth abgesichert
- Per MembershipFilter abgesichert
- Request: Email
- Neues Feld "Status": Number Enum Pending, Accepted, Rejected
- Status wird per default auf Pending gesetzt
- Es wird ein ChangeLog erstellt
- Es muss geprüft werden, ob die Email-Adresse zu einem Nutzer gehört
- Response: Neuer Membership mit neuen Feld Status

- Durch das neue Statusfeld ergeben sich weitere Änderungen:
- Bei CreateOrganisation muss der Membership-Status des Erstellers direkt auf Accepted gesetzt werden
- Bei Memberships_ListByOrganisation muss der Status ebenfalls inkludiert werden