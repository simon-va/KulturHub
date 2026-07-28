# Plan

Diese Datei dient zur besseren Planung von neuen Funktionen:

## User-Story
Als Nutzer
möchte ich eine Liste von allen Mitgliedern einer Organisation erhalten
um zu wissen, wer Teil der Organisation ist

### Akzeptanzkriterien
- Es gibt einen GET Endpunkt, der eine Liste von Memberships für eine bestimmte Organisation zurückgibt
- Der Endpunkt ist in platform/memberships
- route `/organisation/id/memberships`
- Response: Id (Membership), UserId, FullName (User), Email (User), JointAt
- Endpunkt ist mit .RA abgesichert.
- Endpunkt ist mit Membership Filter abgesichert