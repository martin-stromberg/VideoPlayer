← [Zurück zur Übersicht](index.md)

# Weiterschauen — Fehlerbehebung

## Endlosschleife zwischen zwei Episoden

**Symptom:** Ein Benutzer schaut eine Episode zu Ende, und das System springt immer wieder zwischen zwei Episoden hin und her (z. B. Episode 2 ↔ Episode 3). Die "Weiterschauen"-Liste wechselt ständig zwischen den beiden Episoden.

**Ursache:** Das System konnte die nächste Episode nicht zuverlässig ermitteln, weil es sich auf Datumsfelder (`ReleaseDate`) statt auf Episodennummern verlassen hat. Datumsfelder können NULL sein oder sich widersprechen, was zu fehlerhaften Sortierungen führte.

**Lösung:**
1. Stelle sicher, dass die aktuelle Version des Systems installiert ist (mit der Bugfix für `GetNextEpisodeAsync()`)
2. Leere den Puffer: Starte die Anwendung neu
3. Schau die Episode erneut zu Ende — das System sollte jetzt die Episode korrekt erkannt und die nächste Episode korrekt ermittelt haben
4. Falls das Problem weiterhin auftritt: Kontaktiere den Administrator und gib an, welche beiden Episoden sich wiederholen

> **Hinweis:** Dieser Fehler wurde in einer kürzlichen Version behoben. Die Episode-Ermittlung basiert jetzt auf Episodennummern statt auf Datumsangaben.

---

## Episode wird nicht zur "Weiterschauen"-Liste hinzugefügt

**Symptom:** Benutzer schaut eine Episode bis zum Ende, aber die nächste Episode erscheint nicht in der "Weiterschauen"-Liste.

**Ursache (wahrscheinlich):** Eine der folgenden Situationen:
1. Die Episode hat keine nächste Episode (Serienende erreicht)
2. Die nächste Staffel existiert nicht oder hat keine Episoden
3. Ein Datenbankfehler verhindert die Aktualisierung

**Lösung:**
1. **Prüfe, ob die Serie zu Ende:** Ist die aktuelle Episode die letzte Episode der letzten Staffel? Falls ja, ist dies das erwartete Verhalten.
2. **Prüfe die Staffel-Struktur:** Stelle sicher, dass Staffeln mit dem erwarteten Namen existieren
3. **Logs überprüfen:** Der Administrator sollte die Anwendungslogs auf Fehler prüfen (`ProcessBufferedEntryAsync`, `GetNextEpisodeAsync`)
4. **Anwendung neu starten:** Manchmal hilft ein Neustart, Datenbankverbindungsfehler zu beheben
5. **Puffer leeren:** Schau die Episode erneut zu Ende und stelle sicher, dass mindestens 5 Sekunden Wiedergabe gemeldet wurden

---

## "Weiterschauen"-Liste zeigt falsches Medium

**Symptom:** Die "Weiterschauen"-Liste zeigt die vorherige Episode oder den vorherigen Film an, obwohl der Benutzer bereits eine neue Episode zu Ende geschaut hat.

**Ursache:** Das Update der Liste wurde nicht korrekt an den Client übermittelt, oder die SignalR-Benachrichtigung kam nicht an.

**Lösung:**
1. **Seite aktualisieren (F5):** Der Browser sollte die aktuelle "Weiterschauen"-Liste vom Server abrufen
2. **SignalR-Verbindung überprüfen:** Stelle sicher, dass die WebSocket-Verbindung aktiv ist (DevTools → Network → WS)
3. **Netzwerk-Latenzen:** Falls die Verbindung langsam ist, kann es bis zu einigen Sekunden dauern, bis das Update ankommt
4. **Logs überprüfen:** Der Administrator sollte auf SignalR-Fehler prüfen

---

## Mehrere Episoden derselben Serie in der "Weiterschauen"-Liste

**Symptom:** Die "Weiterschauen"-Liste zeigt zwei oder mehr Episoden aus der gleichen Serie an.

**Ursache:** Eine Datenbankinkonsistenz oder ein Fehler in einer älteren Version hat zu mehreren Einträgen pro Serie geführt.

**Lösung:**
1. **Datenbankbereinigung:** Der Administrator sollte folgende Abfrage manuell ausführen:
   ```sql
   -- Entfernt Duplikate und behält die zuletzt aktualisierte Episode pro Serie pro Benutzer
   WITH ranked AS (
       SELECT cwe.Id, 
              ROW_NUMBER() OVER (PARTITION BY cwe.UserId, s.TVShowId ORDER BY cwe.UpdatedAt DESC) AS rn
       FROM ContinueWatchingEntries cwe
       JOIN TVShowEpisodes e ON cwe.TVShowEpisodeId = e.Id
       JOIN TVShowSeasons s ON e.TVShowSeasonId = s.Id
       WHERE cwe.TVShowEpisodeId IS NOT NULL
   )
   DELETE FROM ContinueWatchingEntries
   WHERE Id IN (SELECT Id FROM ranked WHERE rn > 1)
   ```
2. **Seite aktualisieren:** Benutzer sollte die "Weiterschauen"-Seite aktualisieren (F5)
3. **Logs überprüfen:** Prüfe auf Fehler in `RemoveExistingTVShowEntry()` oder `UpsertAsync()`

---

## Position wird nicht gespeichert

**Symptom:** Benutzer schaut eine Episode, pausiert, und beim Zurückkehren startet die Episode vom Anfang statt von der zuletzt gespeicherten Position.

**Ursache:** Positionen werden asynchron gepuffert und verarbeitet. Mögliche Ursachen:
1. Position war kleiner als 5 Sekunden (wird ignoriert)
2. Der Puffer-Worker war nicht aktiv
3. Ein Datenbankfehler verhinderte das Speichern

**Lösung:**
1. **Mindestposition prüfen:** Stelle sicher, dass du mindestens 5 Sekunden in die Episode hineinschaust, bevor du pausierst
2. **Worker-Status prüfen:** Der Administrator sollte überprüfen, dass der `ContinueWatchingWorker` läuft
3. **Anwendung neu starten:** Neustart kann Puffer-Probleme beheben
4. **Logs überprüfen:** Suche nach Fehlern in `ProcessBufferedEntryAsync()`

---

## Fehler beim Laden der "Weiterschauen"-Liste

**Symptom:** Die "Weiterschauen"-Seite lädt nicht oder zeigt einen Fehler an.

**Ursache:** Ein API-Fehler oder Datenbankfehler beim Abrufen der Liste.

**Lösung:**
1. **Seite aktualisieren:** Versuche, die Seite neu zu laden (F5)
2. **Browser-Cache leeren:** Leere den Cache und versuche erneut
3. **Browser-Konsole überprüfen:** Öffne die DevTools (F12) und schaue im Tab "Console" nach JavaScript-Fehlern
4. **Netzwerk-Anfrage überprüfen:** Tab "Network" → suche nach `continue-watching` API-Aufrufen und prüfe die Response
5. **Administrator kontaktieren:** Falls der Fehler fortbesteht, gib dem Administrator die Error-ID oder den Stack Trace aus der Konsole

---

## Staffelwechsel funktioniert nicht

**Symptom:** Benutzer schaut die letzte Episode einer Staffel zu Ende, aber das System springt nicht zur nächsten Staffel — oder springt zur falschen Staffel.

**Ursache:** Die Staffeln sind möglicherweise nicht nach Namen sortierbar, oder die Staffel-Namen sind mehrdeutig.

**Lösung:**
1. **Staffel-Namen überprüfen:** Der Administrator sollte die Staffel-Namen in der Datenbank überprüfen:
   ```sql
   SELECT Id, Name, TVShowId FROM TVShowSeasons 
   WHERE TVShowId = <series_id>
   ORDER BY Name
   ```
   Stelle sicher, dass die Namen konsistent und sortierbar sind (z. B. "Staffel 01", "Staffel 02" oder "Season 1", "Season 2")

2. **Staffel-Reihenfolge beeinflussen:** Falls die alphabetische Sortierung nicht das Gewünschte ergibt, können Staffel-Namen angepasst werden (z. B. von "Spezials" zu "Staffel 00" für korrektes Sorting)

3. **Logs überprüfen:** Suche nach `GetNextEpisodeAsync` im Log, um zu sehen, welche Staffel als nächste erkannt wurde
