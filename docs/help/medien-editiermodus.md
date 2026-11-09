# Medienmetadaten bearbeiten

Administratoren können die Metadaten von Serien, Staffeln, Episoden, Filmen
und Filmsammlungen direkt auf der jeweiligen Detailseite bearbeiten.

## Editiermodus öffnen

Aktiviere neben dem Favoriten-Stern das Stift-Symbol. Die Detailansicht zeigt
dann Eingabefelder anstelle der normalen Metadaten und des Abspiel-Buttons.
Die Felder sind mit den aktuell gespeicherten Werten vorbelegt.

Das bearbeitete Objekt richtet sich nach dem aktuellen Kontext:

- Serienseite: Serie
- Staffelansicht: Staffel
- Episodenansicht: Episode
- Filmansicht: Film
- Sammlungsansicht: Filmsammlung

Beim Wechsel einer Staffel, Episode oder eines Films wechselt auch der
Bearbeitungskontext automatisch zum ausgewählten Objekt. Über das Symbol im
Kopfbereich kannst du aus einer Staffel- oder Episodenansicht zur Serie und
aus einer Filmansicht zur Filmsammlung zurückwechseln.

## Felder und Genres

Für Serien, Filme und Filmsammlungen steht das Feld `ReleaseDate` zur
Verfügung. Für Staffeln und Episoden wird `PremieredAt` verwendet. Das Datum
wird über eine Kalenderauswahl im Format `JJJJ-MM-TT` eingegeben.

Genres können bei Serien und Filmen aus der Genre-Auswahlliste gewählt werden.
Die Liste entspricht den Genres aus der Genre-Verwaltung. Zusätzliche Genres
können als Text eingegeben werden; sie werden beim Speichern als neue Genres
übernommen. Bei Objekten ohne vorhandene Genre- oder Plotdaten werden diese
Felder nicht angeboten.

## Altersfreigabe

Bei Filmen und Serien kann zusätzlich die Altersfreigabe über eine Auswahlliste
gepflegt werden (FSK 0/6/12/16/18 sowie die MPAA-Stufen G, PG, PG-13, R und
NC-17). `Keine Altersfreigabe` entfernt einen vorhandenen Wert. Ist ein Wert
hinterlegt, der keiner bekannten Stufe entspricht (z. B. `TV-MA` aus einer
NFO-Datei), erscheint er zusätzlich als `übernommen`-Option, damit er beim
Speichern anderer Felder erhalten bleibt. Die Freigabe
wird als Badge in der Meta-Zeile der Detailansicht angezeigt; bei Serien
übernehmen Staffel- und Episodenansicht die Serienfreigabe. Eine manuell
gesetzte Freigabe wird durch den manuellen-Editier-Schutz nicht mehr vom Scan
überschrieben.

## Speichern und Verwerfen

Speichere die Änderungen über das Speichern-Symbol. Mit `Abbrechen` werden die
laufenden Änderungen verworfen und die zuletzt gespeicherten Werte wieder
angezeigt.

Wenn du während der Bearbeitung das Objekt wechselst oder den Editiermodus
verlässt, erscheint bei ungespeicherten Änderungen ein Bestätigungsdialog.
`Änderungen verwerfen` setzt den Vorgang fort, `Weiter bearbeiten` bleibt im
aktuellen Formular.

Nur Administratoren können Metadaten speichern.

## Schutz vor Scan-Überschreibung

Erfolgreich gespeicherte manuelle Änderungen werden geschützt. Nachfolgende
initiale, inkrementelle oder durch geänderte Informationsdateien ausgelöste
Scans überschreiben diese Metadaten nicht. Der Schutz gilt auch für Genres und
bleibt dauerhaft bestehen.
