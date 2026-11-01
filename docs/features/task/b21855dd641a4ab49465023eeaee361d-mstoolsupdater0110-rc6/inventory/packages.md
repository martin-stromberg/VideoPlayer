# Paketdelta `msTools.Updater` 0.11.0-rc.5 → 0.11.0-rc.6

Vergleich von `lib/packages/msTools.Updater.0.11.0-rc.5.nupkg` (getrackt) und `lib/packages/msTools.Updater.0.11.0-rc.6.nupkg` (untracked). Beide Pakete enthalten identische Dateistruktur: `README.md`, `lib/net10.0/msTools.Updater.dll`, `lib/net10.0/msTools.Updater.xml`, nuspec.

## nuspec-Vergleich

| Feld | rc.5 | rc.6 | Änderung |
|------|------|------|----------|
| `id` | `msTools.Updater` | `msTools.Updater` | — |
| `version` | `0.11.0-rc.5` | `0.11.0-rc.6` | Versionsbump |
| `license` | `MIT` (Expression) | `MIT` (Expression) | — |
| `authors` | `Softwareschmiede` | `Softwareschmiede` | — |
| `repository`/`commit` | `bebedcf0862def9211361eda83d2f078e7977c1f` | `8ff437a4320ecf1d0ea72176db6d82bdffbbd6f1` | Upstream-Stand |
| `readme` | `README.md` | `README.md` | — |
| `description` | identisch | identisch | — |

**Dependencies (`net10.0`, identisch in beiden Versionen):**
`Microsoft.Extensions.Configuration.Binder` 10.0.7, `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.7, `Microsoft.Extensions.Hosting.Abstractions` 10.0.7, `Microsoft.Extensions.Http` 10.0.7, `Microsoft.Extensions.Logging.Abstractions` 10.0.7, `Microsoft.Extensions.Options` 10.0.7, `System.Management` 10.0.11 — je `exclude="Build,Analyzers"`.

**README.md:** diff leer (identisch).

## API-Delta (`lib/net10.0/msTools.Updater.xml`)

Genau ein neuer Member, nichts entfernt — rein additiv, kein Breaking Change:

| Member | Typ | Beschreibung (aus XML-Doc) |
|--------|-----|----------------------------|
| `DefaultAutoUpdateProcessRunner.MonitorStarterAsync(System.Diagnostics.Process, System.Boolean)` | Methode | Überwacht den kurzlebigen `systemd-run`-Starter unter Linux. Berührt — anders als `MonitorAsync` (Windows-Pfad) — weder Installations-Lock noch persistierten Deskriptor: Der Exit des Starters zeigt nur, ob die transiente Unit registriert wurde, nicht den Skript-Ausgang; das Skript gibt beides selbst frei, Fehlschläge zeigen sich über den `update.failed`-Marker und die Reconciliation. |

`DefaultAutoUpdateProcessRunner` wird im eigenen Code nicht direkt referenziert.

## String-Vergleich der DLLs (UTF-16-User-Strings)

Beleg für den Semantikwechsel des Linux-Pfads:

| Beobachtung | rc.5 | rc.6 |
|-------------|------|------|
| `'--wait'` (Argument für `systemd-run`) | vorhanden | **nicht vorhanden** |
| `'systemd-run'`, `'--unit='`, `'update.failed'`, `'trap cleanup EXIT INT TERM'` | vorhanden | vorhanden |
| `'Failed to monitor the systemd-run starter process.'` | — | neu |
| `'The systemd-run starter exited with code {ExitCode}; the update unit may not have started.'` | — | neu |
| `'The update unit could not be started (systemd-run exited with code '` | — | neu |
| `'Update unit registered (starter exited with code {ExitCode}).'` | — | neu |
| `MonitorAsync` / `IsInstallWithinGracePeriod` | vorhanden | vorhanden (zusätzlich `MonitorStarterAsync`) |

Interpretation: In rc.5 lief die transient Unit via `systemd-run --wait`, der Starter-Prozess wartete bis zum Unit-Ende. In rc.6 startet `systemd-run` ohne `--wait` und endet nach Unit-Registrierung; `MonitorStarterAsync` prüft nur noch den Starter-Exit-Code, Lock-Datei (`update.lock`) und Paket-Deskriptor (`.descriptor.json`) räumt das Skript selbst (im Skript weiterhin `rm -f "$lock"`/`rm -f "$descriptor"` bzw. „Files replaced; lock and descriptor released."), Fehlschläge laufen über `update.failed`-Marker + Reconciliation. Karenzfenster (`IsInstallWithinGracePeriod`) bleibt bestehen.

## Verifikation der vom Test assertierten Skript-Marker

`AutoUpdateProtectedFilesTests.GeneratedScript_ContainsBackupAndMergeInOrder` assertiert Marker im generierten **Windows**-Skript. Alle Marker sind in der rc.6-DLL weiterhin enthalten:

- `$protectedFiles = @(` ✔
- `Backup-AutoUpdateProtectedFiles -Files` ✔
- `Copy-AutoUpdateStagedFiles -Source` ✔
- `Invoke-AutoUpdateProtectedFiles -Files` ✔

## Bewertung für AGENTS.md-Regeln

- Lizenz MIT unverändert, Dependencies unverändert, kein Hauptversions-Sprung → die Regel „keine neuen NuGet-Hauptversionen ohne Prüfung von Lizenz, Release-Build und CI" ist nicht im engeren Sinn betroffen; der übliche Debug-/Release-Build-Nachweis bleibt für den Schritt verpflichtend.
