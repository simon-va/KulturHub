# Plan

Diese Datei dient zur besseren Planung von neuen Funktionen:

## User-Story
Als Nutzer
möchte ich den Status meiner Membership, die Pending ist, auf Accepted oder Rejected setzen
um eine Antwort auf die Einladung zu geben

### Akzeptanzkriterien
- Neuer POST Endpunkt: memberships/{membershipId}/status
- Body: neuer Status (Accepted oder Rejected) – nur diese zwei Werte, eigenes Request-Enum
- Endpunkt ist mit RequireAuth abgesichert + Self-Check (nur der eingeladene User selbst)
- Statuswechsel nur von Pending → Accepted oder Pending → Rejected
- Neues DB-Feld decided_at (nullable), gesetzt bei Accepted UND Rejected
- Feld joined_at wird zu invited_at umbenannt (immer gesetzt, Zeitpunkt der Einladung)
- ChangeLog wird erstellt
- Membership.Create nimmt Status als Parameter; CreateAccepted entfällt; bei Founder InvitedAt == DecidedAt
- Eigenes Enum MembershipChangeStatus (nur Accepted/Rejected) für den Request
- Bestehende MembershipResponse/InviteMembershipResponse bekommen DecidedAt statt JoinedAt
- Migration: invited_at neu + decided_at neu + joined_at entfernen, Backfill aus joined_at für Accepted
- Bestehende Tests, Handler, Configurations, Responses entsprechend anpassen