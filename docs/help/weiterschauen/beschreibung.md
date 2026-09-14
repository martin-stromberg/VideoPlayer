← [Zurück zur Übersicht](index.md)

# Weiterschauen — Beschreibung

## Zweck

Das Feature "Weiterschauen" ermöglicht es Benutzern, an der exakten Stelle fortzufahren, an der sie eine Serie oder einen Film unterbrochen haben. Statt bei Anfang oder Ende einer Episode zu landen, wird automatisch die nächste unwiedersehene Episode vorgeschlagen und abgespielt. Dies reduziert Verwirrung und erhöht die Benutzerzufriedenheit durch nahtlose Fortführung.

## Funktionsweise

### Für Serien (TV-Shows)

1. **Speichern der Position:** Wenn ein Benutzer eine Episode anschaut, werden die aktuelle Wiedergabeposition und die Gesamtdauer regelmäßig in einem Puffer gesammelt.

2. **Erkennen des Endes:** Das System prüft, ob die Episode zu Ende (mindestens 30 Sekunden vom Ende entfernt) angeschaut wurde.

3. **Ermittlung der nächsten Episode:**
   - Das System sucht nach der Episode mit der nächsthöheren Nummer in der gleichen Staffel.
   - Sind alle Episoden der Staffel verbraucht, springt das System zur ersten Episode der nächsten Staffel.
   - Gibt es keine weitere Staffel, endet die Serie (kein automatischer Vorschlag).

4. **Aktualisierung der "Weiterschauen"-Liste:** Die neue Episode wird auf die "Weiterschauen"-Liste des Benutzers gesetzt. Nur eine Episode pro Serie ist auf dieser Liste sichtbar.

### Für Filme

1. **Speichern der Position:** Ähnlich wie bei Serien werden Position und Dauer regelmäßig gepuffert.

2. **Erkennen des Endes:** Das System prüft, ob der Film zu Ende angeschaut wurde.

3. **Ermittlung des nächsten Films:**
   - Das System sucht nach dem nächsten Film in der gleichen Filmsammlung (z. B. eine Filmreihe).
   - Die Sortierung folgt der Veröffentlichungsreihenfolge (nach Datum und Name).
   - Gibt es keinen weiteren Film, wird kein Vorschlag gemacht.

4. **Aktualisierung der "Weiterschauen"-Liste:** Der nächste Film wird auf die Liste gesetzt. Nur ein Film pro Sammlung ist sichtbar.

## Beispiele

**Szenario 1: Serie fortsetzen**
- Benutzer schaut "Breaking Bad – Staffel 1 – Episode 3" und pausiert
- Beim nächsten Login sieht er "Breaking Bad – Staffel 1 – Episode 3" in der "Weiterschauen"-Liste mit seiner zuletzt gespeicherten Position
- Er klickt darauf, Episode 3 startet ab seiner gespeicherten Position
- Er schaut Episode 3 zu Ende
- Das System ermittelt die nächste Episode: Episode 4 derselben Staffel
- Episode 4 wird jetzt in der "Weiterschauen"-Liste angezeigt

**Szenario 2: Staffelwechsel**
- Benutzer schaut die letzte Episode (Episode 10) von Staffel 2 einer Serie zu Ende
- Das System erkennt, dass es keine Episode 11 in Staffel 2 gibt
- Es springt zur nächsten Staffel und wählt deren erste Episode (Episode 1 von Staffel 3)
- Episode 3.01 wird auf die "Weiterschauen"-Liste gesetzt

**Szenario 3: Filmsammlung fortsetzen**
- Benutzer schaut "Iron Man" aus der Marvel-Sammlung zu Ende
- Das System ermittelt den nächsten Film in der Sammlung (nach Veröffentlichungsdatum)
- Der nächste Film wird auf die "Weiterschauen"-Liste gesetzt

## Weiterschauen mit Playlist-Bezug

Wenn ein Benutzer ein Video **aus einer Playlist heraus** startet, wird diese Information im Weiterschauen-Eintrag gespeichert. Die Wiedergabe wird dann später wieder **im gleichen Playlist-Kontext** fortgesetzt, einschließlich der gespeicherten Wiedergabeposition.

### Anzeige in der Weiterschauen-Liste

Einträge mit Playlist-Bezug werden mit einem Hinweis „In Playlist: [Name]" gekennzeichnet, z. B. „In Playlist: Meine Favoriten". Diese Einträge unterscheiden sich von normalen Einträgen (ohne Playlist-Bezug) — dasselbe Video kann **mehrfach** in der Liste erscheinen:

- Einmal ohne Playlist-Bezug (von normaler Wiedergabe)
- Mehrfach mit unterschiedlichen Playlist-Bezügen (von verschiedenen Playlists)

Alle diese Varianten sind **unabhängige Einträge** mit eigenem Fortschritt. Jeder Eintrag wird eindeutig identifiziert und korrekt mit seinen Anzeigedaten (Titel, Bild, Wiedergabeposition, Playlist-Name) verknüpft, auch wenn mehrere Varianten desselben Videos nebeneinander in der Liste angezeigt werden.

### Verfügbare Aktionen

Die Kontextmenü-Aktionen „Ausblenden" und „Überspringen" wirken nur auf die jeweilige Playlist-Variante:
- **Ausblenden:** Entfernt nur den Eintrag mit dieser Playlist-ID aus der Weiterschauen-Liste; andere Varianten (mit anderen Playlists oder ohne) bleiben erhalten.
- **Überspringen:** Ersetzt nur diese Variante durch das nächste Medium der gleichen Playlist oder Serie.

### Video als gesehen markieren

Im Gegensatz zu den obigen Aktionen ist die Gesehen-Markierung **playlist-übergreifend**:
- Wenn ein Benutzer ein Video zu Ende schaut (Fortschritt nahe am Ende), werden **ALLE Varianten** dieses Videos aus der Weiterschauen-Liste entfernt — unabhängig davon, mit welcher Playlist oder ohne Playlist sie verknüpft sind.
- Dies entspricht dem Verhalten ohne Playlist-Bezug und vermeidet, dass der gleiche Titel danach mehrfach in der Liste vorkommt.

### Playlist gelöscht

Wird eine Playlist gelöscht, bleiben die zugehörigen Weiterschauen-Einträge in der Liste erhalten, verlieren aber ihren Playlist-Bezug:
- Der Eintrag wird zu einem normalen, playlistlosen Eintrag umgewandelt.
- Der Hinweis „In Playlist: …" verschwindet.
- Die zuletzt gespeicherte Wiedergabeposition bleibt erhalten.
- Falls bereits ein playlist-loser Eintrag für das gleiche Video existiert, wird der playlist-gebundene Eintrag entfernt, um Duplikate zu vermeiden.

### Einzelner Titel aus Playlist entfernt

Entfernt der Anwender nur einen einzelnen Titel aus einer Playlist (nicht die ganze Playlist), und existiert
dafür noch ein Weiterschauen-Eintrag mit Bezug zu genau dieser Playlist, erscheint vor dem Entfernen eine
Sicherheitsabfrage: „Dieser Eintrag befindet sich in deiner Weiterschauen-Liste. Entfernen?"
- Nach Bestätigung wird der Weiterschauen-Eintrag durch den nächsten in dieser Playlist verfügbaren Titel
  ersetzt (Wiedergabeposition beginnt bei null); gibt es keinen weiteren verfügbaren Titel, wird der
  Weiterschauen-Eintrag entfernt.
- Weiterschauen-Einträge desselben Videos ohne Playlist-Bezug oder mit Bezug zu einer anderen Playlist
  bleiben unangetastet.
- Verschwindet der Titel stattdessen still aus dem Medienbestand (z. B. Datei gelöscht), entfällt die
  Sicherheitsabfrage, aber dasselbe Ersetzen-/Entfernen-Verhalten gilt sinngemäß.

Details siehe `docs/help/weiterschauen/business-rules.md`.

## Einschränkungen

- **Nur eine Episode/Film pro Serie/Sammlung pro Playlist:** Ein Benutzer kann pro Serie/Sammlung/Playlist-Kombination nur einen Eintrag in der Weiterschauen-Liste haben. Alle anderen Einträge dieser Serie/Sammlung werden automatisch entfernt, wenn eine neue Episode/Film hinzugefügt wird.

- **Staffel-Sortierung:** Die Reihenfolge der Staffeln wird nach deren Name bestimmt. Unkonventionelle Staffel-Namen (z. B. "Spezials" ohne Nummer) können zu unerwarteten Übergängen führen.

- **Fehlende Episodennummern:** Wenn Episodennummern in einer Staffel Lücken aufweisen (z. B. 1, 2, 4 statt 1, 2, 3), wird die nächste verfügbare Episode korrekt erkannt.

- **Benutzerabhängig:** Jeder Benutzer hat seine eigene "Weiterschauen"-Liste. Gemeinsame Konten sehen die Fortsetzungen des jeweils letzten aktiven Benutzers.
