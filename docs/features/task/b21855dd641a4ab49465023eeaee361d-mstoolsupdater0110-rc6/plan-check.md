# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| `msTools.Updater` 0.11.0-rc.5 → 0.11.0-rc.6 in `VideoWebPlayer/VideoWebPlayer.csproj` (Z. 59) | Schritt 2 | Gefilterte Update-Tests + Voll-Suite (Schritte 4–5); `project.assets.json`-Auflösung in Schritt 3 | Abgedeckt |
| `msTools.Updater` analog in `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Z. 22) | Schritt 2 | wie oben; `MsToolsUpdaterIntegrationTests` läuft direkt gegen die aufgelöste DLL | Abgedeckt |
| `msTools.Updater.0.11.0-rc.6.nupkg` committen (liegt untracked vor) und `…-rc.5.nupkg` entfernen | Schritt 1 (Voraussetzung verifiziert: Datei liegt als untracked in `lib/packages/` vor — per `git status` nachvollzogen) | indirekt: Restore muss `0.11.0-rc.6` auflösen (Schritt 3) | Abgedeckt |
| Keine Codeanpassung im eigenen Bestand (reiner Bump) | Abschnitte „Neue Klassen" / „Änderungen an bestehenden Klassen": korrekt begründet über rein additives API-Delta (`MonitorStarterAsync`) und nicht referenzierten Typ `DefaultAutoUpdateProcessRunner` | Gefilterte Tests + Voll-Suite als Negativnachweis | Abgedeckt |
| `dotnet build VideoPlayer.sln` in Debug **und** Release fehlerfrei (AGENTS.md §7) | Schritt 3 explizit beide Konfigurationen; Risiko-Abschnitt benennt Release-Abweichungen | — | Abgedeckt |
| Volle Testsuite `dotnet test VideoWebPlayer.Tests` grün | Schritt 5; Flaky-Playwright-Regel (einzeln wiederholen, ehrlich berichten) übernommen | — | Abgedeckt |
| Update-bezogene Testklassen nach dem Bump laufen lassen (`MsToolsUpdaterIntegrationTests`, `UpdateBackupCoordinatorTests`, `UpdateBackupEventBinderTests`, `UpdateSettingsServiceTests(_Iis)`, `AutoUpdateProtectedFilesTests`, `UpdatesPageE2ETests`, `UpdatesControllerAuthorizationTests`) | Schritt 4 listet alle Klassen aus der Anforderung plus `UpdateAdminServiceTests` | Schritt 4 ist der Testnachweis | Abgedeckt |
| Besondere Aufmerksamkeit für `GeneratedScript_ContainsBackupAndMergeInOrder` (Skript-Marker können sich zwischen Paketversionen ändern) | Risiko-Abschnitt + Tests-Tabelle: alle vier assertierten Marker in der rc.6-DLL verifiziert enthalten; Fallback-Vorgehen bei Fehlschlag beschrieben (Marker angleichen, Reihenfolge *Backup → Copy → Merge* erhalten) | Schritt 4 verifiziert den Test gezielt | Abgedeckt |
| `docs/TECH_Auto_Update.md` Z. 117–125: `--wait`-Beschreibung ist für rc.6 überholt und muss den neuen Sachverhalt treffen | Schritt 6 beschreibt den korrekten rc.6-Sachverhalt (kein `--wait`, kurzlebiger Starter nur bis Unit-Registrierung, `MonitorStarterAsync` prüft nur Starter-Exit, Lock/Deskriptor räumt das Skript selbst, Fehlerpfad über `cleanup`-Trap + `update.failed` + Karenz-Reconciliation); aktuelle Zeilen per `sed` verifiziert — beschreiben tatsächlich `systemd-run --wait` | `tools/MarkdownLinkCheck.Tests` (Schritt 8) | Abgedeckt |
| `docs/RELEASE_NOTES.md`: kurzer Eintrag in beiden Sprachabschnitten | Schritt 7: EN „What's New" vor Z. 39, DE „Neuerungen" vor Z. 177 — Positionen per `grep` verifiziert | `tools/MarkdownLinkCheck.Tests` (Schritt 8) | Abgedeckt |
| `docs/help/updates.md` unverändert, `README.md` ohne Versionsbezug | Schritt 7 bzw. Inventory-Begründung (kein user-sichtbarer systemd-Detailtext, keine Versionsnennung) | — | Abgedeckt |
| Keine neuen NuGet-Hauptversionen ohne Lizenz-/Release-/CI-Prüfung (AGENTS.md §7) | Regel nicht betroffen: MIT unverändert, Dependencies identisch, kein Hauptversionssprung (inventory/packages.md § Bewertung); Release-Build dennoch Pflichtschritt | — | Abgedeckt |
| Keine Datenmodell-/EF-Migrations-/Konfigurationsänderung | Plan: „Datenbankmigrationen: Keine", „Konfigurationsänderungen: Keine" (kein neuer `AutoUpdateOptions`-Member) — korrekt | — | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine — Status ist `Plan vollständig`.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Kein Benutzerfluss betroffen (reiner Versionsbump ohne UI-/API-Änderung; einzige Verhaltensänderung liegt auf dem Linux-`systemd-run`-Pfad innerhalb der Bibliothek, vom Windows-Test-Host über keinen Benutzerfluss auslösbar) | Keine neuen E2E-Tests — Begründung im Plan-Abschnitt „E2E-Tests" vorhanden und stichhaltig; bestehende `UpdatesPageE2ETests` laufen in Schritt 4 und 5 mit und müssen unverändert grün bleiben | Nicht erforderlich mit Begründung |

## Fehlende oder unvollständige Planbestandteile

Keine — Status ist `Plan vollständig`.

## Hinweise

- Die Upstream-Referenzen (Issue `martin-stromberg/msTools.Updater#78`, PR `#80`), die der Plan in den Release Notes verlinken will, ließen sich von hier nicht verifizieren (Repository von außen nicht erreichbar, HTTP 404 — vermutlich privat). Die fachliche Substanz des Eintrags ist unabhängig davon belegt (DLL-String-Vergleich in `inventory/packages.md`: `--wait` entfernt, neue Starter-Monitoring-Meldungen). Beim Schreiben des Release-Notes-Eintrags die Issue-/PR-Nummern kurz gegen das Upstream-Repo gegenchecken.
- `MsToolsUpdaterIntegrationTests` läuft in Schritt 4 im selben gefilterten Lauf mit — der Plan sollte diesen Test bewusst als Versionsnachweis führen (er läuft direkt gegen die aufgelöste DLL); im Risiko-Abschnitt ist das bereits so formuliert, in Schritt 4 nur implizit.
- Der Plan enthält keinen expliziten Schritt für die Commit-Sequenz (`plan:`/`feat:`-Muster aus PR #324) bzw. die Aktualisierung des Tasks-Dokuments — das ist Lifecycle-Prozess und keine Planlücke, sollte bei der Umsetzung aber mitgedacht werden.
