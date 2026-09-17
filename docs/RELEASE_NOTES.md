# Release Notes

> As of the CI standardization migration, `.github/workflows/release.yml` (previously
> `main-release.yml`) no longer reads this file as the GitHub release body. Releases now use
> semantic-release's auto-generated notes (derived from conventional commit messages) instead
> of this hand-curated changelog. This file is kept for historical reference; whether a
> manually maintained changelog should be reintroduced is an open follow-up question.
>
> Frühere Fassung: Diese Datei wurde von `main-release.yml` als Body des GitHub-Releases
> verwendet. Seit der CI-Vereinheitlichung generiert semantic-release die Release-Notes
> automatisch aus den Commit-Nachrichten; diese Datei wird nicht mehr automatisch verwendet.

## Important Notes Before Update

- There are no special notices.

## What's New

- Fixed recurring database errors during actor classification of already known movies and episodes.
- The actor backfill for existing media now continues with the next item instead of aborting when a single entry fails.
- Media directories that were deleted on the source no longer cause errors during scans and actor backfill.
- Fixed the "recent entries" list failing with an internal query error.
- Requests with malformed host headers are rejected early instead of crashing page rendering.
- Internal API calls without a signed-in user now log a warning instead of an error.

## Wichtige Hinweise vor dem Update

- Es gibt keine besonderen Hinweise.

## Neuerungen

- Wiederkehrende Datenbankfehler bei der Schauspieler-Klassifizierung bereits bekannter Filme und Episoden behoben.
- Die Schauspieler-Nacherfassung für den Altbestand läuft bei einem fehlerhaften Eintrag jetzt mit dem nächsten Element weiter, statt abzubrechen.
- Auf der Quelle gelöschte Medienverzeichnisse verursachen beim Scan und bei der Schauspieler-Nacherfassung keine Fehler mehr.
- Fehler behoben, durch den die Liste der zuletzt hinzugefügten Einträge mit einem internen Abfragefehler scheiterte.
- Anfragen mit fehlerhaftem Host-Header werden jetzt frühzeitig abgelehnt, statt die Seitendarstellung abstürzen zu lassen.
- Interne API-Aufrufe ohne angemeldeten Benutzer erzeugen jetzt eine Warnung statt eines Fehlers.
