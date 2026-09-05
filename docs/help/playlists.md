# Playlists

Angemeldete Anwender können eigene Playlists anlegen, umbenennen und löschen. Playlists sind
benutzerbezogen und privat: Jeder Anwender sieht und verwaltet ausschließlich seine eigenen
Playlists, unabhängig davon, welche anderen Anwender ebenfalls Playlists angelegt haben.

Dieser Schritt umfasst die Verwaltung der Playlists selbst (anlegen, umbenennen, löschen,
Übersicht). Das Befüllen von Playlists mit konkreten Medieninhalten (z. B. Serien oder Staffeln)
ist nicht Teil dieses Schritts und folgt in einem späteren Ausbauschritt.

## Übersicht

Unter „Playlists" sieht ein Anwender eine Tabelle aller eigenen Playlists mit Name, Beschreibung,
Sortiermodus sowie Erstellungs- und Aktualisierungszeitpunkt. Von hier aus lassen sich neue
Playlists anlegen sowie bestehende bearbeiten oder löschen.

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

## Löschen

Playlists werden nach Bestätigung in einem Dialog endgültig gelöscht (kein Papierkorb).

## Zugriff und Berechtigungen

Alle Playlist-Funktionen erfordern eine Anmeldung. Der Zugriff auf eine fremde Playlist (z. B.
über eine erratene ID) wird abgelehnt, unabhängig davon, ob die Playlist existiert.

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
