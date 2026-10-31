# Code-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

— (Status `Keine Befunde`)

## Geprüfte Dateien

- `VideoWebPlayer/Services/IProgramScheduleService.cs` — neuer Record `ProgramRuleDiagnosis`
  mit vollständiger XML-Doku; neue Methode konsistent zu den bestehenden Signaturen
  (`CancellationToken` zuletzt, `default`).
- `VideoWebPlayer/Services/ProgramScheduleService.cs` — Diff gegen Basisbranch geprüft.
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` — Diff geprüft.
- `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Diagnosis.cs` — neue Testklasse.
- `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs` — erweiterte Assertions im
  konsolidierten Test.
- `docs/help/tv-sender.md`, `docs/RELEASE_NOTES.md`.

## Hinweise

- **Semantik-Erhalt:** `MovieMediaItemsAsync`/`EpisodeMediaItemsAsync` wählen weiterhin das
  niedrigste `MediaItemId`-Item **mit** Laufzeit (`OrderBy(Duration is null).ThenBy(MediaItemId)`)
  — die Slot-Zuordnung ändert sich nicht; `SkippedNoDuration` zählt weiterhin alle drei
  Fälle (kein Item, Probe ausstehend, Probe erschöpft), sodass öffentliche Summaries und
  Bestandstests unverändert bleiben (gefilterter Lauf grün).
- **Zählreihenfolge** in `TryMovieItem`/`ToItem` unverändert: erst Laufzeit, dann Freigabe —
  ein Kandidat ohne Laufzeit *und* ohne Freigabe zählt unter „keine Laufzeit", wie bisher.
- **Bereinigung:** die bereits vorher ungenutzte private Methode `TryEpisodeItem` wurde
  entfernt (toter Code, Signatur musste ohnehin angepasst werden).
- **`RequireAdminAsync`** dupliziert bewusst das `ProgramRuleService`-Muster — kleine,
  bewusste Duplikation statt geteilter Helper über Service-Grenzen hinweg; konsistent mit
  der Codebase.
- **`GenerateNowAsync`:** `disposed`-Flag via `@implements IDisposable` verhindert
  `StateHasChanged` auf entsorgter Komponente; Fehler landen wie bei den übrigen Handlern
  in der Statuszeile.
- **`Task.Delay(4s)`** als magische Zahl: akzeptabel dokumentiert am Einsatzort; ein
  konfigurierbarer Wert wäre Over-Engineering für eine Admin-Ansicht.
