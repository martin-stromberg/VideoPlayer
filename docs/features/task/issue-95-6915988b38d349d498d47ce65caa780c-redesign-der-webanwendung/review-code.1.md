# Code-Review

Status: Befunde vorhanden

## Befunde

1. `VideoWebPlayer/Components/Pages/TV/TVShowDetails.razor:344` - Die Episodenkarte wird als `role="button"` mit `tabindex="0"` ausgezeichnet, reagiert aber nur auf `@onclick`. Damit ist das Element zwar per Tastatur fokussierbar, Enter/Leertaste loesen die Auswahl aber nicht aus. Das ist eine Barrierefreiheits-Regression gegen das Redesign-Ziel "tastaturbedienbar" und kann Tastaturnutzer auf der Serien-Detailansicht blockieren. Entweder ein echtes `<button>` verwenden oder `@onkeydown` fuer Enter/Space auf `SelectEpisode(episode)` binden.

## Build- und Runtime-Pruefung

- `dotnet build` erfolgreich, 0 Fehler.
- Es bleiben bestehende Warnungen, insbesondere bekannte `NU1903`-Warnungen zu `SQLitePCLRaw.lib.e_sqlite3` sowie vorhandene Nullable-/Obsolete-Warnungen in MAUI-Projekten. Im geprueften Redesign-Diff wurde kein neuer Build-Fehler sichtbar.

## Tests

- Im uncommitted Diff sind keine neuen oder angepassten Tests enthalten.
- Fuer die gefundene Tastaturbedienungs-Regression fehlt ein Test oder eine automatisierte UI-/Accessibility-Pruefung, die fokussierbare Karten per Enter/Space auswaehlt.
