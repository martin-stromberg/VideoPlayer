# Handover – Projekt GitHub Page mit Demoinstallation

Projektbranch: `task/issue-356-7edcb903624e49349a46a0e05e41b45f-github-page-anwendung-mit-mode`
Issue: martin-stromberg/VideoPlayer#356

## Bearbeiter

| Schritt | Bearbeiter | Branch | Status | Seit |
|---------|------------|--------|--------|------|
| 1 | Devin | `task/issue-356-7edcb903624e49349a46a0e05e41b45f-github-page-anwendung-mit-mode-schritt-1-demoinstallation` | pausiert | 2026-11-23 |

## Übergabestand je Schritt

### Schritt 1: Demoinstallation — mitgelieferter Demodatenbestand bei der Erstregistrierung

- **Stand vom:** 2026-11-23, Implementierung abgeschlossen und committed; unabhängige Abnahme steht noch aus
- **Bearbeiter / Übernehmer:** Devin / offen (Abnahme)

**Erledigt**
- Projektartefakte: `requirement.md`, `inventory.md` (+Detaildokumente), `project-plan.md`, `project-plan-check.md` (Status: Projektplan vollständig), `steps.md`.
- Anwender-Entscheidungen: Empfehlungen zur Page bestätigt; Scope-Erweiterung (Demoinstallation, lokale Quelle, Screenshots aus Demoinstallation) per Issue-Kommentar.
- Lifecycle vollständig durchlaufen: Anforderung, Bestandsaufnahme, Planung, Plan-Gegenprüfung, Implementierung, Plan-/Usability-/Code-Review, Tests inkl. realer E2E-Abnahme mit Playwright und Sichtprüfung der Screenshots, Dokumentation (help/README/RELEASE_NOTES). Feature-Verzeichnis mit Abschluss-Commit gelöscht; `…-tasks.md` enthält Ergebnis-Tabelle.
- Umgesetzt: `FileSystemDemoDataSetService` (erweitert um `LocalDirectory`-Quellen, Pfad-Validierung, sofortiger Scan via `MediaSourceScanCoordinator`), `DemoMediaContentMaterializer` + `DemoMediaImageGenerator` (SkiaSharp-JPEGs), versionierter Bestand `Demobibliothek` unter `VideoWebPlayer/DemoDataSets/` (3 Filme, 2 Serien à 1 Staffel/3 Episoden, NFOs, h264-MP4 `demo-video.mp4`), Auswahl im unveränderten Registrierungsdialog, `MediaSourceUser`-Zugriff für Erstbenutzer, `.gitignore` für `Data/DemoMedia/`, 12 neue Tests, Doku in `docs/help/einrichtung.md`, README- und Release-Notes-Eintrag (EN/DE).

**Offen**
- Unabhängige Abnahme durch anderen Agenten (`acceptance-schritt-1.md` im Projektordner) — Implementierer darf keine eigene Abnahme schreiben.
- Danach Merge des Schritt-Branches in den Projekt-Branch.

**Entscheidungen**
- Offene Planpunkte 1–4 behandelt: Dummy-mp4 beilegen, Anzeigebilder per SkiaSharp zur Laufzeit, 3 Filme + 2 Serien à 3 Episoden, bestehendes Fehlerverhalten bei ApplyAsync behalten (Benutzer bleibt, Fehler im Formular).
- `MediaSourceType.LocalDirectory` ist bereits released — kein eigener Schritt, nur Voraussetzung.
- Poster-Maß gegenüber Plan von 500×750 auf 800×1200 erhöht (Screenshot-Tauglichkeit).
- Keine Unteragenten in der Umgebung verfügbar — der Orchestrator hat alle Lifecycle-Schritte selbst ausgeführt (im `todo.md` dokumentierte Abweichung).

**Außerhalb des Auftrags**
- Keine.

**Tests**
- Debug- und Release-Build `VideoPlayer.sln`: 0 Fehler.
- Volle Suite (Release): 2983 bestanden, 0 Fehler, 2 begründete Skips (gesamt 2985). Demo-Fokus: 12/12 grün.
- Reale Abnahme `dotnet run` + Playwright: Registrierung mit Demobibliothek → Bestätigung → gefüllte Bibliothek, Quelle, Film-/Serien-Details, Episoden; MP4 über HTTP abrufbar (`ftypisom…avc1`, 134.226 Bytes, `MP4-PLAYABLE: True`). Screenshots sichtgeprüft.
- Bekannter Nebeneffekt: E2E-Test-Dispose räumt `Data/DemoMedia` unter dem Projekt-ContentRoot ab — während parallelem `dotnet run` führt das zu 404 auf Streams (kein Produktbefund).
- Debug-Lauf zeigt bei 7 bestehenden Playwright-Tests `404 /_framework/dotnet-browser-tools/hot-reload-settings.json` (Dev-Only-Middleware-Artefakt, Release-Lauf grün).

**Achtung**
- `DemoDataSets/` und `StandardSources.json` existieren noch nicht; csproj-Regel kopiert bisher nur `DemoDataSets\*.json` — Medien-Assets in Unterordnern gehen beim Publish sonst verloren. → Erledigt: csproj kopiert jetzt `DemoDataSets\**` (JSON + Inhalte) nach Build und Publish; im Output verifiziert.
- EF-Migrationen kollidieren im Model-Snapshot: bei späterem Merge nach Rebase neu erzeugen (siehe AGENTS.md). Dieser Schritt enthält keine Migration.
