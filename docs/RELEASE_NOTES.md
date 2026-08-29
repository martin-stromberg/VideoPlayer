# Release Notes

> Diese Datei wird von `.github/workflows/main-release.yml` als Body des GitHub-Releases verwendet.
> Vor einem Release sollte sie auf den aktuellen Stand gebracht werden; alter Inhalt kann entfernt oder durch den neuen Release-Text ersetzt werden.

## Important Notes Before Update

- A new database migration adds the `UnlockedMediaEntries` table; it is applied automatically on startup. Backups from older versions remain restorable because missing new tables and fields are tolerated.

## What's New

- Fixed the title list not updating to the last selected source when switching sources through the menu.
- Fixed source page rendering for users with only individually unlocked items; no more unhandled exceptions when opening a source.
- Block direct URL manipulation for non-unlocked detail and stream endpoints.
- Sources containing at least one unlocked item now appear in the user menu.
- Source detail pages list only explicitly unlocked titles for users without full source access.
- "Neu im Programm" / recent list shows only explicitly unlocked titles for sources the user cannot fully access.
- Added user-specific individual unlocks for TV shows and movie collections, including a lock/unlock button and user-selection dialog on detail pages.
- Backup/restore now tolerates missing `UnlockedMediaEntries` and `EndThreshold` fields.
- Added controller, service and end-to-end tests for unlock visibility and authorization.
- Added help page `einzelfreischaltungen`.

## Wichtige Hinweise vor dem Update

- Eine neue Datenbank-Migration fügt die Tabelle `UnlockedMediaEntries` hinzu; sie wird beim Start automatisch angewendet. Datensicherungen älterer Versionen bleiben wiederherstellbar, da fehlende neue Tabellen und Felder toleriert werden.

## Neuerungen

- Fehler behoben, durch den die Titelliste beim Wechsel zwischen Quellen über das Menü nicht auf die zuletzt ausgewählte Quelle aktualisiert wurde.
- Fehler bei der Quellenseite für Benutzer mit nur einzelnen Freischaltungen behoben; kein Seitenfehler mehr beim Aufrufen einer Quelle.
- Direkte URL-Manipulation auf nicht freigegebene Detail- und Stream-Endpunkte wird blockiert.
- Quellen mit mindestens einem freigeschalteten Titel erscheinen jetzt im Benutzermenü.
- Quellenseiten zeigen Benutzern ohne vollen Quellenzugriff nur explizit freigegebene Titel.
- "Neu im Programm" zeigt für eingeschränkte Quellen nur explizit freigegebene Titel.
- Benutzerspezifische Einzelfreischaltungen für Serien und Filmsammlungen hinzugefügt, inklusive Freigabe-Schaltfläche und Benutzerauswahl-Dialog auf Detailseiten.
- Backup/Wiederherstellung toleriert jetzt fehlende `UnlockedMediaEntries`- und `EndThreshold`-Felder.
- Controller-, Service- und End-to-End-Tests für Sichtbarkeit und Autorisierung ergänzt.
- Hilfeseite `einzelfreischaltungen` hinzugefügt.
