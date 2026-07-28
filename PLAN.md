# Plan

Diese Datei dient zur besseren Planung von neuen Funktionen:

## User-Story

Als Nutzer
möchte ich ein Membership löschen können
um die Liste der berechtigten Personen aktuell zu halten

### Akzeptanzkriterien

- Neuer DELETE Endpunkt: memberships/{membershipId}
- Endpunkt ist mit RequireAuth abgesichert
- Löschen erlaubt, wenn Actor Mitglied der Organisation ist (Membership.Status == Accepted)
- Membership darf gelöscht werden, sofern danach noch mindestens ein weiteres aktives Member übrig bleibt
- Self-Delete (eigene Membership) immer erlaubt, sofern danach noch mindestens ein weiteres aktives Member übrig bleibt
- Es darf nicht das letzte aktive Member der Organisation gelöscht werden (Org muss mindestens ein Member behalten) → 409 Conflict
- Der Status der Membership ist egal
- Idempotentes Verhalten: Zweiter DELETE auf dieselbe Membership gibt 404 zurück
- Soft-Delete: Neues DB-Feld deleted_at (nullable)
- ChangeLog wird erstellt (Delete-Event mit Actor + MembershipId + OrgId)
- Membership.Delete nimmt Actor als Parameter und prüft Org-Mitgliedschaft + "letztes Member"-Constraint in einer Transaction
- Bestehende Queries (GetMemberships, GetInvites etc.) filtern `deleted_at IS NULL` per globalem Query-Filter
- Bestehende MembershipResponse/InviteMembershipResponse bleiben unverändert — DeletedAt ist eine interne Metrik und wird nicht nach außen gegeben
- Migration: deleted_at neu (nullable) + Index auf (org_id, user_id, deleted_at)
- Bestehende Tests, Handler, Configurations, Responses entsprechend anpassen