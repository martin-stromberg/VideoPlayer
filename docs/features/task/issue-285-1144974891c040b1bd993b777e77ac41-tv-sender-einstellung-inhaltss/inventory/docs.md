# Dokumentation — Bestandsaufnahme

Bestehende Hilfe-/Projektdokumente, die die Anforderung berührt.

## `docs/help/tv-sender.md` (602 Zeilen)

| Stelle | Inhalt |
|--------|--------|
| Z. 32–40 | Tab-Übersicht; „Einstellungen" führt die Gruppen Sender, Planung und Segmentierung |
| Z. 48–53 | „Bundesland für Feiertage" — Auswahlfeld im Tab Einstellungen, persistiert in `Setups.HolidayFederalState` |
| Z. 104–109 | Gruppe „Planung" im Tab „Einstellungen": „Planungshorizont (Stunden)" (48 h), „Generierungsintervall (Minuten)" (15 min, greift ohne Neustart) — hier würde die neue Einstellung dokumentarisch einordnen |
| Z. 125–129 | Planungsregel: nur Inhalte mit bekannter Laufzeit, die für das gesamte (Nicht-Administrator-)Publikum freigegeben sind (Medienquellenzugriff oder Einzelfreischaltung) — **dieser Satz beschreibt das bisherige, abschaltbare Verhalten** |
| Z. 150–164 | Maskottchen-/Clip-Quelle: „Die Quelle für alle Anwender freigeben — Füller werden nur eingeplant, wenn sie für die gesamte Nicht-Admin-Zuhörerschaft sichtbar sind." |
| Z. 191–202 | Clip-Gruppen: gleiche Freigabe-Aussage für Gruppen-Clips |
| Z. 204–214 | Reservierte Gruppe „Trailer" (automatisch befüllt, Vorlauffenster `ProgramSchedule:TrailerLeadDays`) |
| Z. 313–317 | Regel-Diagnose: Zähler u. a. „nicht für alle freigegeben" |
| Z. 421 ff. | Karte „Segmentierung" im Tab „Einstellungen" |
| Z. 471 ff. | Standby-Clip-Karte |
| Z. 525 ff. | Fehlerbehebung/Zugriff (Sender-Freigabe pro Benutzer) |

## `docs/help/tv-sender-ablauf-technisch.md` (259 Zeilen)

| Stelle | Inhalt |
|--------|--------|
| Z. 75–111 | Plangenerierung `EnsureCoverageAsync`: `BuildClipGroupStates`, `FilterClipCandidatesAsync` (mit `access.Allows`-Erwähnung Z. 82), `InsertClipFill`, `FillGaps`, `PersistCursors` |
| Z. 113–126 | `ReconcileAsync`: Clip-bewusste `AiringReproducible`-Variante, Mitgliedschafts-Check gegen die aktuelle Expansion (Grundlage des „wirkt auf die nächste Planerstellung") |
| Z. 145–156 | Fehlerbehandlung inkl. „`RequireAdminAsync` in jeder Service-Methode" |
| Z. 158–164 | Alt-Backup-Restore-Konvention (`OptionalRestoreTables`/`OptionalRestoreColumns`/`OptionalRestore*Defaults`) |
| Z. 166–259 | Standby-Stream: `Setup.LiveStreamStandbyMediaItemId`, Freigabeprüfungen `GetTvSenderEnabledAsync`/`IsTvSenderAccessGrantedAsync` in `LiveStreamController.GetFile` (Z. 193–194) |

## `docs/help/tv-sender-datenmodell.md` (121 Zeilen)

| Stelle | Inhalt |
|--------|--------|
| Z. 107–109 | Migrationskonvention (`AddProgramClipGroups` samt Seed) |
| Z. 111–121 | Abschnitt „`Setup` (erweitert)": Tabelle der `Setups`-Zusatzspalten (aktuell nur `LiveStreamStandbyMediaItemId`), Hinweis auf `OptionalRestoreColumns` + Regressionstest — Vorlage für den Eintrag der neuen Spalte |

## `README.md` / `docs/RELEASE_NOTES.md`

`RELEASE_NOTES.md` (EN+DE) existiert und wird je Feature gepflegt; `README.md` verweist auf
die Hilfe. Beide sind bei der Umsetzung mitzupflegen (Definition of Done).

## Migrationen (Konvention)

`VideoWebPlayer/Migrations/` — Namensmuster `AddSetup<Property>` für neue `Setups`-Spalten,
zuletzt `20261026154346_AddSetupLiveStreamStandbyMediaItem`. Der Model-Snapshot
`ApplicationDbContextModelSnapshot.cs` kollidiert bei parallelen Migrationen
(AGENTS.md: später mergender Schritt erzeugt die Migration nach Rebase neu).
