# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TVSenderAdmin.razor (TVSenderAdmin)

- **Doppelter Code** — Der Aktivieren/Deaktivieren-Button ist in den Zeilen 145–156
  als zwei nahezu identische `<button>`-Blöcke in einer `@if/@else`-Verzweigung
  ausprogrammiert: Klassenliste (`btn btn-ghost btn-icon tvsender-rule-toggle`),
  `@onclick`-Handler und die Struktur „`title`/`aria-label` + Inline-SVG" sind in
  beiden Zweigen gleich; nur der Label-Text („Deaktivieren"/„Aktivieren") und das
  SVG-Icon (Pause- vs. Play-Symbol) unterscheiden sich. Bei späteren Änderungen
  (z. B. Handler oder Klassen) können die beiden Zweige auseinanderlaufen.

  Empfehlung: Auf einen Button reduzieren — `title`/`aria-label` über
  `@(rule.IsActive ? "Deaktivieren" : "Aktivieren")` setzen und nur das SVG-Element
  im Inneren konditional rendern (`@if (rule.IsActive) { <svg Pause/> } else { <svg Play/> }`).

## Geprüfte Dateien

- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
- `VideoWebPlayer/wwwroot/app.css`
- `VideoWebPlayer.Tests/Components/TVSenderAdminTests.cs`
- `VideoWebPlayer.Tests/Components/TVSenderAdminTests_ClipGroups.cs`
- `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs`
- `VideoWebPlayer.Tests/TVSenderAdminClipGroupsE2ETests.cs`
- `docs/help/tv-sender.md`
