Aktuell können Veranstaltungen (events) über einen Post Endpunkt erstellt werden.
Ich möchte einen ChatBot erstellen, der die Aufgabe übernimmt, event zu erstellen.

Im Frontend wird es eine Page geben, die wie ein klassiches Chatbot Interface die Veranstaltungen verwaletet.
Zur Erinnerung: In Claude Desktop z.B. gibt es links eine Liste mit allen Unterhaltungen.
Rechts ist dann der Nachrichtenverlauf der UNterhaltung.
Wenn man mit der KI ein Artefakt, wie eine Datei, erstellt hat, dann gibt es rechts neben dem Chat noch einmal einen Bereich, in dem das Artefakt angezeigt werden kann.

Wir unsere Veranstaltungen werden wir links alle Events anzeigen.
Zu jedem Event wird es eine Unterhaltung geben. Das Event ist praktisch das Artefakt.
Daher gibt es eine Panel rechts neben dem Chat für die Daten der Veranstaltungen.
Die Daten des Events werden aber nicht durch direkte Endpunkte verwaletet, sondern rein über die Unterhaltung.
Der Workflow ist dann so:
- Der Nutzer klickt auf einen Button, um eine Veranstaltung zu erstellen.
- Es wird ein Endpunkt aufgerufen, um eine neue Veranstaltung zu erstellen.
- Der Endpunkt erstellt einen leeren Eintrag in der events Tabelle. Setzt den Status auf "draft".
- Der Endpunkt erstellt eine neue Unterhaltung, die das event referenziert.
- Es wird eine erste Message mit dem Inhalt "Neue Veranstaltung erstellt. Erzähl mir von ihr."

Wir brauchen also zwei neue Tabellen. 
conversations:
- id
- organisation_id
- created_at

messages
- id
- conversation_id
- role: 1 system, 2 user (enum im Backend erstellen)
- content
- create_at


Da ich den Bot später auch für andere Bereiche, wie Berichte oder "Mach Mit!"-Aktionen nutzen möchte, sollen die conversations an die aktionen gebunden werden.
Für den Start referenzieren wir also die conversation_id in der events-Tabelle. 

Das für den Start. Den eigentlichen Bot und die Chat-Endpunkte werden wir nicht in diesem Schritt implementieren.
Erstmal nur die Tabellen und der überarbeitete postEvent endpunkt, der alles aufsetzt.