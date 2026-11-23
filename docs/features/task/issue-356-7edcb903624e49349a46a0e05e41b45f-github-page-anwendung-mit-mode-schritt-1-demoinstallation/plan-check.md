# Plan-Gegenprüfung

Geprüft: `requirement.md` (AK-1 bis AK-5, Fachlicher Scope) gegen `plan.md`
unter Einbeziehung der Bestandsaufnahme (`inventory/`).

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

|| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
||--------------------|-------------------|----------------------|--------|
|| AK-1: Bestand zur Auswahl bei Erstregistrierung; `LocalDirectory`-Quelle + Zugriffsrecht | `Demobibliothek.json` in `DemoDataSets/` → `GetAvailableAsync` liefert Bestand → bestehender `InputSelect` zeigt ihn; `ApplyAsync` setzt `SourceType` + `MediaSourceUser` | `RegisterPage_FirstUser_ShowsDemoDataSetOption`, `RegisterFirstUser_WithDemoDataSet_FillsLibrary`, `ApplyAsync_CreatesLocalDirectorySourceWithAccess`, `GetAvailableAsync_ReturnsDemobibliothek` | Abgedeckt |
|| AK-2: Bibliothek sofort gefüllt (Filme, Serien, Staffeln/Episoden, Bilder), kein manueller Scan, kein Intervall-Warten | `ApplyAsync` stößt nach Commit `RunFullScanAsync` an; NFO-/Bild-Konventionen belegen Klassifizierung (`LocalMediaSourcePipelineE2ETests` als Beweis) | `ApplyAsync_LibraryIsFilledAfterScan`, `RegisterFirstUser_WithDemoDataSet_FillsLibrary` | Abgedeckt |
|| AK-3: Dummy-Videos im Browser abspielbar, echtes Containerformat, klein | Beigelegte `demo-video.mp4` (h264/faststart) wird pro Videoeintrag kopiert | `DemoVideoFile_IsValidMp4Container` + reale Abnahme (Browser-Wiedergabe) | Abgedeckt |
|| AK-4: Bestand im Repo, bei Start/Auslieferung mitgeliefert; ohne Auswahl bleibt Installation leer | `DemoDataSets/` im Projekt, csproj-Copy-Regel für `DemoDataSets\**` inkl. Publish-Verifikation; ohne Auswahl kein `ApplyAsync` | `RegisterFirstUser_WithoutDemoDataSet_StaysEmpty`; Publish-Verifikation im Umsetzungsschritt 6 | Abgedeckt |
|| AK-5: Tests belegen Quelltyp, Medienverzeichnis, gefüllte Bibliothek, Auswahlpfad | Testabschnitt deckt alle vier Punkte mit echter SQLite/HTTP-Ebene ab | siehe Tabelle „Neue Tests" im Plan | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine — zu jedem AK existiert mindestens ein Testnachweis; der
Formular-Benutzerfluss hat E2E-Abdeckung auf HTTP-Ebene (echtes Formular,
Antiforgery, POST, DB-Nachweis). Dialog-/Overlay-Geometrie-Assertions sind
nicht erforderlich: es wird kein Dialog/Overlay/Panel geändert
(`Register.razor` bleibt unverändert).

## E2E-Abdeckung

|| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
||------------------------------------|--------------------|--------|
|| Erstregistrierung mit Demodaten-Auswahl → gefüllte Bibliothek (AK-1/AK-2) | `DemoDataRegistrationE2ETests.RegisterFirstUser_WithDemoDataSet_FillsLibrary` | Abgedeckt |
|| Sichtbarkeit des Auswahlfelds mit Bestand (AK-1) | `DemoDataRegistrationE2ETests.RegisterPage_FirstUser_ShowsDemoDataSetOption` | Abgedeckt |
|| Registrierung ohne Auswahl → leer (AK-4) | `DemoDataRegistrationE2ETests.RegisterFirstUser_WithoutDemoDataSet_StaysEmpty` | Abgedeckt |
|| `ApplyAsync`-Wirkung auf Quelle/Verzeichnis/Bibliothek (AK-1/AK-2/AK-5) | `FileSystemDemoDataSetServiceE2ETests.*` (echter Host + SQLite + Dateisystem) | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

Keine. Im Einzelnen verifiziert:

- **Scope-Klärung** vollständig; alle Scope-Ausschlüsse tragen Status
  `bestätigt` mit Begründung (Projekt-Festlegungen 1–5 sind eingearbeitet).
- **Happy Path** als Akzeptanzszenario 1 vom Formular-POST bis zur gefüllten
  Bibliothek vorhanden, inkl. Ausgangszustand ohne Benutzer/Quellen und ohne
  `Data/DemoMedia/`.
- **Wartesituationen** (paralleler Hintergrundscan) mit Zuständen und
  Wiederaufnahmeweg adressiert (Szenario 5, `_classificationRunning`,
  Upsert-Idempotenz).
- **Materialisierungsziel:** `Data/` ist die belegte schreibbare Ablage
  (`Data/WebVideoPlayer.db`, `Data/Backups`); `path` relativ unter
  ContentRoot aufgelöst erzeugt den für `LocalDirectory` nötigen absoluten
  Pfad (Validierungsregel aus `media-sources.md` erfüllt).
- **Voraussetzungen der Umsetzungsreihenfolge** vollständig: ffmpeg lokal
  vorhanden (9.0.2, verifiziert), SkiaSharp-Dependency vorhanden,
  `MediaSourceScanCoordinator` scoped registriert (Zeile 240) — `ApplyAsync`
  kann ihn injizieren, keine Zirkularität (Koordinator hängt nicht von
  `IDemoDataSetService` ab).
- **Publish-Auslieferung:** csproj kopiert bisher nur `*.json` flach — Plan
  enthält expliziten Schritt inkl. Publish-Verifikation.
- **Berechtigungen:** `MediaSourceUser`-Anlage bleibt serverseitig im
  Service; keine neuen Berechtigungsregeln nötig.

## Hinweise

- `StandardSources.json`-Mechanismus bleibt bewusst unberührt (anderer Zweck;
  Target springt weiterhin „nicht gefunden" übergangen).
- Die Copy-Regel muss Nicht-JSON-Dateien (`*.nfo`, `*.mp4`) und
  Unterverzeichnisse erfassen — bei der Umsetzung Publish-Output prüfen,
  nicht nur Build-Output.
