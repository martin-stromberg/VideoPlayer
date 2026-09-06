# Playlists

Angemeldete Anwender können eigene Playlists anlegen, öffnen, bearbeiten und löschen. Playlists sind
benutzerbezogen und privat: Jeder Anwender sieht und verwaltet ausschließlich seine eigenen
Playlists, unabhängig davon, welche anderen Anwender ebenfalls Playlists angelegt haben.

Dieser Abschnitt umfasst die Verwaltung der Playlists selbst (anlegen, öffnen, bearbeiten, löschen,
Übersicht), das Befüllen einer Playlist mit konkreten Medieninhalten (Filme, Episoden, Staffeln,
Serien, Filmsammlungen) sowie die automatisch sortierte, fortlaufend nachladende Anzeige der
Inhalte auf der Detailseite.

## Übersicht

Unter „Playlists" sieht ein Anwender eine Tabelle aller eigenen Playlists mit Name, Beschreibung,
Sortiermodus sowie Erstellungs- und Aktualisierungszeitpunkt. Von hier aus lassen sich neue
Playlists anlegen sowie bestehende öffnen, bearbeiten oder löschen.

Für jede Playlist werden drei Aktionen angeboten:

- **Öffnen** — Navigiert zur Detailseite der Playlist (siehe Abschnitt „Detailseite")
- **Bearbeiten** — Öffnet ein Formular zum Ändern der Stammdaten (Name, Beschreibung, Sortiermodus)
- **Löschen** — Entfernt die Playlist nach Bestätigung in einem Dialog

Ist noch keine Playlist vorhanden, zeigt die Übersicht einen Hinweis anstelle der Tabelle an.
Schlägt das Laden der Playlists fehl (z. B. weil der Anwender nicht angemeldet ist), erscheint
eine Fehlermeldung anstelle der Übersicht.

## Anlegen und Bearbeiten

Beim Anlegen oder Bearbeiten einer Playlist werden folgende Angaben erfasst:

- **Name** (Pflichtfeld): maximal 255 Zeichen, darf nicht leer oder nur aus Leerzeichen bestehen.
  Der Name muss pro Anwender eindeutig sein (Groß-/Kleinschreibung wird dabei nicht
  unterschieden); ein doppelter Name führt zu einer Fehlermeldung.
- **Beschreibung** (optional): maximal 2000 Zeichen.
- **Sortiermodus**: entweder „Nach Erscheinungsdatum" (automatisch, Standard) oder „Manuell".

Ungültige Eingaben werden sowohl im Formular als auch serverseitig abgelehnt und mit einer
aussagekräftigen Fehlermeldung angezeigt.

## Detailseite

Durch die "Öffnen"-Aktion in der Übersicht oder direkte Navigation gelangt ein Anwender zur
Detailseite einer Playlist. Die Detailseite zeigt die Stammdaten der Playlist an:

- **Name** — Name der Playlist
- **Beschreibung** — Beschreibungstext (falls vorhanden)
- **Sortierung** — Sortiermodus der Playlist („Nach Erscheinungsdatum" oder „Manuell")
- **Erstellt** — Zeitpunkt der Erstellung
- **Aktualisiert** — Zeitpunkt der letzten Änderung

Von der Detailseite aus lassen sich die gleichen Aktionen wie in der Übersicht durchführen:

- **Bearbeiten** — Öffnet das Bearbeitungsformular für die Playlist
- **Löschen** — Löscht die Playlist nach Bestätigung
- **Zurück zur Übersicht** — Kehrt zur Playlist-Übersicht zurück

## Inhalte hinzufügen und entfernen

Unterhalb der Stammdaten zeigt die Detailseite die Liste der Inhalte („Einträge") dieser Playlist
mit Titelbild, Typ, Titel, zugehöriger Sammlung (falls vorhanden) und Hinzufügedatum.

### Titelbild

Jeder Eintrag zeigt ein kleines Titelbild des referenzierten Inhalts (Film, Serie, Staffel,
Episode oder Filmsammlung). Ist für den Inhalt kein eigenes Bild hinterlegt, erscheint stattdessen
ein Platzhalterbild.

### Zugriffsstatus in der Liste

Serien und Filmsammlungen, die für den angemeldeten Anwender nicht freigeschaltet sind, werden in
der Liste optisch abgeblendet dargestellt, um anzuzeigen, dass die enthaltenen Inhalte derzeit
nicht angeschaut werden können. Der **Entfernen**-Button bleibt für solche Einträge weiterhin
nutzbar — der Anwender kann einen nicht freigeschalteten Eintrag also jederzeit aus der Playlist
entfernen.

### Sortierung und Anzeige

Steht die Playlist im Sortiermodus „Nach Erscheinungsdatum" (Standard), erscheinen die Einträge
automatisch chronologisch nach Erscheinungsdatum des jeweiligen Inhalts. Besitzt ein Eintrag kein
Erscheinungsdatum, wird er stattdessen anhand seiner Serien-/Staffel-/Episodenzugehörigkeit
eingeordnet; ist auch das nicht möglich, richtet sich die Reihenfolge danach, wann der Eintrag der
Playlist hinzugefügt wurde. Im Sortiermodus „Manuell" erscheinen die Einträge in der Reihenfolge
ihres Hinzufügedatums.

Enthält eine Playlist viele Einträge, werden zunächst nur die ersten davon angezeigt. Beim
Herunterscrollen der Liste werden automatisch weitere Einträge nachgeladen und angehängt, sodass
die Seite auch bei sehr umfangreichen Playlists flüssig bedienbar bleibt. Ein Hinweistext
("Weitere Einträge werden beim Scrollen geladen.") zeigt an, dass noch nicht alle Einträge geladen
sind; sobald die Liste vollständig geladen ist, verschwindet dieser Hinweis.

### Hinzufügen

Zum Hinzufügen eines Medieninhalts wählt der Anwender:

1. Den Medientyp aus einem Dropdown: **Film**, **Serie**, **Staffel**, **Episode** oder **Filmsammlung**
2. Die Medien-ID des gewünschten Inhalts (eine Zahl)
3. Bestätigt mit dem Button **Hinzufügen**

Das System prüft sofort, ob der Medieninhalt existiert. Falls nicht, wird eine Fehlermeldung angezeigt.

**Kaskaden-Logik:** Wird eine Serie, Staffel oder Filmsammlung hinzugefügt, werden automatisch
auch alle zugehörigen Staffeln/Episoden bzw. Filme mit aufgenommen. Beispiele:
- Hinzufügen einer **Serie** → alle Staffeln und Episoden dieser Serie werden hinzugefügt
- Hinzufügen einer **Staffel** → alle Episoden dieser Staffel werden hinzugefügt
- Hinzufügen einer **Filmsammlung** → alle Filme dieser Sammlung werden hinzugefügt

Falls Teile der Kaskade bereits in der Playlist vorhanden sind, werden diese übersprungen,
ohne dass dies dem Anwender als Fehler angezeigt wird. Nur neue Inhalte werden hinzugefügt.

**Duplikat-Prüfung:** Ein Medieninhalt darf nicht doppelt in derselben Playlist vorkommen.
Der Versuch, einen bereits vorhandenen Inhalt erneut hinzuzufügen, wird **nicht** mit einer
Fehlermeldung abgelehnt. Stattdessen werden die bereits vorhandenen Inhalte übersprungen
und der Anwender erhält eine aussagekräftige Rückmeldung, z. B. „3 Titel hinzugefügt, 2 bereits
vorhanden und übersprungen." oder „Alle 5 Titel waren bereits vorhanden." Die Operation wird
erfolgreich abgeschlossen (grüne Info-Meldung), auch wenn nichts Neues hinzugefügt wurde.

### Entfernen

Jeder Eintrag in der Liste besitzt einen **Entfernen**-Button. Ein Klick darauf entfernt
genau diesen Eintrag sofort aus der Playlist. Die Liste wird unmittelbar aktualisiert.

### Automatische Bereinigung

Wird ein Medieninhalt aus dem Bestand entfernt (z. B. eine Serie oder ein Film gelöscht),
verschwindet der zugehörige Playlist-Eintrag beim nächsten Laden der Playlist still
(ohne Fehlermeldung oder Hinweismeldung für den Anwender). Diese automatische Bereinigung
verhindert, dass die Playlist auf nicht mehr existierende Inhalte verweist.

## Löschen

Playlists werden nach Bestätigung in einem Dialog endgültig gelöscht (kein Papierkorb).

## Zugriff und Berechtigungen

Alle Playlist-Funktionen erfordern eine Anmeldung. Jeder Anwender kann nur auf seine eigenen
Playlists zugreifen: Der Versuch, eine fremde Playlist zu bearbeiten oder zu löschen, wird
abgelehnt. Falls eine Playlist nicht existiert, wird ebenfalls eine Fehlermeldung angezeigt.

Wird ein Benutzerkonto gelöscht, werden auch alle Playlists dieses Anwenders automatisch entfernt.

## Konfiguration (Administratoren)

Optional lässt sich die maximale Anzahl an Playlists pro Anwender begrenzen. Der Wert wird in der
Konfiguration festgelegt:

```json
{
  "Playlists": {
    "MaxPlaylistsPerUser": null,
    "MaxPlaylistItemCount": null,
    "DefaultPageSize": 20,
    "MaxPageSize": 100
  }
}
```

`null` bedeutet keine Begrenzung (Standardeinstellung). Ein numerischer Wert begrenzt die Anzahl
der Playlists, die ein einzelner Anwender gleichzeitig anlegen kann; beim Überschreiten wird das
Anlegen weiterer Playlists mit einer Fehlermeldung abgelehnt.

`MaxPlaylistItemCount` ist für eine spätere Begrenzung der Anzahl an Einträgen pro Playlist
vorbereitet. Die Konfiguration existiert bereits, wird aber aktuell noch nicht durchgesetzt
(`null`, unbegrenzt).

`DefaultPageSize` legt fest, wie viele Einträge auf der Detailseite pro Ladevorgang beim Scrollen
nachgeladen werden (Standard: 20). `MaxPageSize` begrenzt die höchstzulässige Anzahl an Einträgen
pro Ladevorgang (Standard: 100).
