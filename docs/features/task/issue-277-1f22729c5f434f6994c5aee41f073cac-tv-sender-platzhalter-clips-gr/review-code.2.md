# Code-Review

Basisbranch: `staging` (`git merge-base HEAD staging` = `3bbbd490`). Zweite Review-Runde — Runde 1 (`review-code.1.md`) nur als Kontext. Geprüft wurden alle im Branch geänderten und neuen Quelldateien inklusive der uncommitteten/untracked Dateien; Dokumentation (docs/, README.md, RELEASE_NOTES) wurde ausgenommen. `dotnet build` für `VideoWebPlayer` und `VideoWebPlayer.Tests` läuft fehlerfrei (0 Warnungen, 0 Fehler).

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### VideoWebPlayer/Services/ProgramClipGroupService.cs (ProgramClipGroupService)

- **Fehlerbehandlung / Eingabevalidierung** — `AddClipsToGroupAsync` (Zeilen 183–185): `var missing = ids.FirstOrDefault(id => !found.Contains(id)); if (missing != 0)` verwendet `0` als „nichts fehlt"-Sentinel. Übergibt ein Aufrufer die Id `0` (oder generell eine Id, die dem Defaultwert entspricht), wird sie als „vorhanden" behandelt: die `KeyNotFoundException` entfällt und es wird eine `ProgramClipGroupItem`-Zeile mit `MediaItemId = 0` angelegt, die erst beim `SaveChangesAsync` als kryptische FK-Verletzung scheitert statt als sauberer Fachfehler.

  Empfehlung: Sentinel-frei prüfen, z. B. `var missing = ids.Except(found).ToList(); if (missing.Count > 0) throw new KeyNotFoundException(...)` oder `ids.FirstOrDefault(id => !found.Contains(id), -1L)` mit `missing >= 0`-Vergleich.

### VideoWebPlayer/Services/ProgramRuleService.cs (ProgramRuleService)

- **Fehlerbehandlung / Eingabevalidierung** — `ValidateClipGroupInputAsync` (Zeilen 329–335): `input.PlaceholderGrace` wird für Regeln ohne `SlotDuration` kommentarlos akzeptiert und persistiert, obwohl die Karenzzeit nur bei einer Sendeplatz-Länge wirkt — `ClipGroupIds` wird im selben Validierungsblock ohne `SlotDuration` dagegen korrekt abgelehnt. Die UI bietet das Karenzzeit-Feld zudem für alle Nicht-Füller-Regeln an (auch ohne Länge), sodass wirkungslose Werte gespeichert werden; die Untergrenze `grace < SlotDuration` greift entsprechend nie.

  Empfehlung: Asymmetrie auflösen — `PlaceholderGrace` ohne `input.SlotDuration` entweder ablehnen (parallel zu `ClipGroupIds`) oder bewusst ignorieren und den Default speichern; ggf. das UI-Feld an `editRule.DurationMinutes > 0` koppeln.

### VideoWebPlayer/Services/ProgramScheduleService.cs (ProgramScheduleService)

- **Long Parameter List** — `FillAiringSegments` (Zeilen 2552–2554) trägt 8 Parameter (`airing, state, rest, periodEnd, tailLimit, ref roundRobin, rebuilt, ctx`), obwohl `rest` und `periodEnd` vollständig aus `airing` + `state.Rule.SlotDuration` ableitbar sind und `tailLimit` aus `blocked` + `ctx.BoundStarts`/`ctx.HorizonEnd` — die Datei nutzt für genau dieses Problem bereits das Bündel-Muster (`ClipFillContext`, `ReproContext`).

  Empfehlung: `rest`/`periodEnd` intern aus `airing` und `state.Rule` berechnen und `blocked` statt `tailLimit` durchreichen (oder ein kleines Airing-Kontext-Record bilden), sodass die Signatur auf ~5 Parameter schrumpft.

### VideoWebPlayer.Tests/TVSenderAdminClipGroupsE2ETests.cs (TVSenderAdminClipGroupsE2ETests)

- **Doppelter Code** — `SwitchTabAsync` (Zeilen 164–177) und `EnableTvSenderAsync` (Zeilen 151–159) sind wortgleiche Kopien der privaten Helfer in `TVSenderAdminE2ETests` (Zeilen 34–45 bzw. 48–56); `OpenAdminPageAsAdminAsync` (Zeilen 19–42) dupliziert dessen Login-/Grant-Ablauf und erweitert ihn nur um den MediaSourceUser-Grant-Block.

  Empfehlung: Die gemeinsamen Helfer in eine gemeinsame Basisklasse heben (z. B. `TVSenderAdminE2EBase : PlaylistsE2ETestBase` mit `SwitchTabAsync`, `EnableTvSenderAsync` und einem parametrisierbaren `OpenAdminPageAsAdminAsync`), damit weitere Sender-E2E-Dateien keine dritte Kopie erzeugen.

## Geprüfte Dateien

Geändert:
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
- `VideoWebPlayer/Data/ApplicationDbContext.cs`
- `VideoWebPlayer/Data/Configurations/ProgramRuleConfiguration.cs`
- `VideoWebPlayer/Data/ProgramRule.cs`
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`
- `VideoWebPlayer/Migrations/ApplicationDbContextModelSnapshot.cs`
- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`
- `VideoWebPlayer/Services/ProgramRuleInput.cs`
- `VideoWebPlayer/Services/ProgramRuleService.cs`
- `VideoWebPlayer/Services/ProgramScheduleService.cs`
- `VideoWebPlayer.Tests/Components/TVSenderAdminTests.cs`
- `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`
- `VideoWebPlayer.Tests/ProgramRuleCursorNullStrandKeyMigrationTests.cs`
- `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs`

Neu:
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdminClipGroups.razor`
- `VideoWebPlayer/Data/ProgramClipGroup.cs`
- `VideoWebPlayer/Data/ProgramClipGroupItem.cs`
- `VideoWebPlayer/Data/ProgramRuleClipGroup.cs`
- `VideoWebPlayer/Data/Configurations/ProgramClipGroupConfiguration.cs`
- `VideoWebPlayer/Data/Configurations/ProgramClipGroupItemConfiguration.cs`
- `VideoWebPlayer/Data/Configurations/ProgramRuleClipGroupConfiguration.cs`
- `VideoWebPlayer/Migrations/20261025112747_AddProgramClipGroups.cs`
- `VideoWebPlayer/Migrations/20261025112747_AddProgramClipGroups.Designer.cs`
- `VideoWebPlayer/Services/IProgramClipGroupService.cs`
- `VideoWebPlayer/Services/ProgramClipGroupService.cs`
- `VideoWebPlayer.Tests/Components/TVSenderAdminTests_ClipGroups.cs`
- `VideoWebPlayer.Tests/ProgramClipGroupServiceTests.cs`
- `VideoWebPlayer.Tests/ProgramGuideServiceTests_ClipGroups.cs`
- `VideoWebPlayer.Tests/ProgramRuleServiceTests_ClipGroups.cs`
- `VideoWebPlayer.Tests/ProgramScheduleServiceTests_ClipGroups.cs`
- `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests_ClipGroups.cs`
- `VideoWebPlayer.Tests/TVSenderAdminClipGroupsE2ETests.cs`
