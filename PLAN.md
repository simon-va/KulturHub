Lass uns ein neues Feature erstellen.
Wir brauchen zwei neue Tabelle.
Tabelle organisations
Sie hat 
- id
- name

wir brauchen eine weitere Tabelle, um user einer Organisation zuzuordnen
organisation_members
- id
- user_id
- organisation_id


Wir brauchen einen Endpunkt, um
- eine neue Organisation erstellen
- eine Organisation per PUT zu updaten (aktuell nur das feld name)
- eine Organisation zu löschen

Wenn ein user eine Organisation erstellt, wird er dieser direkt zugewiesen.

Out of Scope:
- Weitere User zur Organisation hinzufügen