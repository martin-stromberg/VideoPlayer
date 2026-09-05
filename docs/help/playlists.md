# Playlists

Angemeldete Anwender können eigene Playlists anlegen, öffnen, bearbeiten und löschen. Playlists sind
benutzerbezogen und privat: Jeder Anwender sieht und verwaltet ausschließlich seine eigenen
Playlists, unabhängig davon, welche anderen Anwender ebenfalls Playlists angelegt haben.

Dieser Abschnitt umfasst die Verwaltung der Playlists selbst (anlegen, öffnen, bearbeiten, löschen,
Übersicht). Das Befüllen von Playlists mit konkreten Medieninhalten (z. B. Serien oder Staffeln)
ist nicht Teil dieser Beschreibung und folgt in einem späteren Ausbauschritt.

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

Die Detailseite zeigt ausschließlich die Stammdaten an. Das Befüllen der Playlist mit konkreten
Medieninhalten (z. B. Serien oder Staffeln) wird in einem zukünftigen Ausbauschritt hinzugefügt.

## Löschen

Playlists werden nach Bestätigung in einem Dialog endgültig gelöscht (kein Papierkorb).

## Zugriff und Berechtigungen

Alle Playlist-Funktionen erfordern eine Anmeldung. Der Zugriff auf eine Playlist, die einem anderen
Anwender gehört, wird mit HTTP 403 (Forbidden) abgelehnt. Existiert die angefragte Playlist-ID gar
nicht, wird stattdessen HTTP 404 (Not Found) zurückgegeben. Da sich diese beiden Fälle im
Statuscode unterscheiden, lässt sich über die Antwort erkennen, ob eine fremde Playlist-ID
existiert.

Wird ein Benutzerkonto gelöscht, werden auch alle Playlists dieses Anwenders automatisch entfernt.

## Konfiguration (Administratoren)

Optional lässt sich die maximale Anzahl an Playlists pro Anwender begrenzen. Der Wert wird in der
Konfiguration festgelegt:

```json
{
  "Playlists": {
    "MaxPlaylistsPerUser": null
  }
}
```

`null` bedeutet keine Begrenzung (Standardeinstellung). Ein numerischer Wert begrenzt die Anzahl
der Playlists, die ein einzelner Anwender gleichzeitig anlegen kann; beim Überschreiten wird das
Anlegen weiterer Playlists mit einer Fehlermeldung abgelehnt.
