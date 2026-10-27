# Übersetzte Anforderung — TV-Sender: Altersgerechte Sendezeiten (Jugendschutzfenster)

Quelle: `issue.md` (GitHub-Issue #271), baut auf #269 (Altersfreigabe-Erfassung) auf.

## Fachliche Zusammenfassung

Die Sendeplan-Erzeugung (`ProgramScheduleService.EnsureCoverageAsync`/`ReconcileAsync`) wird um eine **Jugendschutzfenster-Regelung** erweitert: Ein Titel darf nur dann in einem Zeitfenster materialisiert werden, wenn seine Altersfreigabe (`IAgeRated.AgeRatingMinAge` auf `Movie`/`TVShow`) zur lokalen Sendezeit passt. Nicht passende Titel werden in ein zulässiges Zeitfenster verschoben bzw. durch passendes Material/Füller ersetzt — der Plan bleibt lückenlos. Das Fensterschema folgt der deutschen Jugendschutz-Konvention (FSK 12 ab 20:00, FSK 16 ab 22:00, FSK 18 ab 23:00–06:00; MPAA-Werte werden über das bereits normalisierte `AgeRatingMinAge` analog abgebildet) und ist konfigurierbar. Die Regel-Diagnose in der Admin-UI muss „kein altersgerechtes Material im Fenster" als eigenen Grund ausweisen.

## Betroffene Klassen und Komponenten

- `VideoWebPlayer/Services/ProgramScheduleService.cs`
  - `SchedulableItem` (private Record, Zeile ~1061): um die Freigabe-Information des Titels zu erweitern (voraussichtlich `AgeRatingMinAge`/`AgeRating`, ggf. Marker „unbekannte Freigabe"), damit die Auswahl das Fenster ohne Nachabfragen prüfen kann.
  - `SelectItems`, `SelectAiringItems`, `SelectAiringItemsInterleaved` (Zeilen ~1472–1600): Filterpunkt pro Occurrence — Titel, deren Freigabe zur lokalen Air-Time nicht passt, werden übersprungen; Cursor/Rotation dürfen dabei nicht dauerhaft verklemmen (übersprungener Titel wird beim nächsten zulässigen Fenster erneut angeboten).
  - `MaterializeOccurrence` und `MaterializeOccurrenceEvicting` (Reconcile-Pfad, Zeilen ~987, ~1407): beide Materialisierungspfade müssen die Fensterprüfung anwenden.
  - Expansionsmethoden `ExpandShowAsync`, `ExpandMovieAsync`, `ExpandMovieCollectionAsync`, `ExpandGenreAsync`, `ExpandPlaylistAsync` (Zeilen ~1799–2094): müssen die Freigabe mitladen — bei Serien gilt die Freigabe der `TVShow` (Episoden haben keine eigene; `IAgeRated` ist nur auf `Movie`/`TVShow` implementiert).
  - `SummaryCounters`: Diagnosezähler für „wegen Jugendschutzfenster nicht planbar" ergänzen.
  - `FillGaps`/Füller-Pfad: Ersatz durch passendes Füllmaterial muss das Fenster ebenfalls beachten (Annahme: Füller unterliegen derselben Regelung).
- `VideoWebPlayer/Data/ProgramRule.cs`
  - Evtl. neues Feld (z. B. maximale Freigabe der Regel oder ein Freigabe-Fenster-Profil) — abhängig von der Klärung, ob die Regelung senderweit oder zusätzlich je Regel gilt. Bei neuem Feld: EF-Migration + Eintrag in `OptionalRestoreColumns` (`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`, Zeile ~51) samt Alt-Backup-Regressionstest.
- `VideoWebPlayer/Data/Setup.cs` / `VideoWebPlayer/Services/ProgramSettingsService.cs`
  - Neue senderweite Einstellungen: Fenster-Grenzen (Freigabe → früheste lokale Sendezeit) und Standardverhalten bei unbekannter Freigabe — Muster `Setup.ProgramScheduleContentScope` mit `Get…`/`Update…`-Methoden in `ProgramSettingsService` (Zeilen ~345–413). Bei neuen `Setups`-Spalten: Migration + `OptionalRestoreColumns`.
- Interfaces / Records
  - `VideoWebPlayer/Services/IProgramScheduleService.cs`: Record `ProgramRuleDiagnosis` (Zeile ~64) um den Diagnosegrund „kein altersgerechtes Material im Fenster" erweitern.
  - Neuer Enum für das Verhalten bei unbekannter Freigabe (z. B. `ProgramScheduleUnknownRatingMode`: konservativ = nur Nachtfenster vs. freigeben) — Muster `ProgramScheduleContentScope`.
  - Ggf. neue Hilfsklasse/Record für das Fensterschema (Freigabe → früheste lokale Sendezeit), abgeleitet aus `AgeRatingMinAge` — `AgeRatingNormalizer` (`VideoWebPlayer/Utils/AgeRatingNormalizer.cs`) liefert bereits die kanonischen Mindestalter (FSK 0/6/12/16/18; MPAA `G`→0, `PG`→8, `PG-13`→13, `R`→17, `NC-17`→18) und bleibt die einzige Quelle der Stufennormalisierung.
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
  - `DiagnosisLabel` (Zeile ~1632): neuen Grund „kein altersgerechtes Material im Fenster" anzeigen.
  - Reiter „Einstellungen" (Planungs-Karte, Zeilen ~547–565): Konfiguration der Fenster und des Verhaltens bei unbekannter Freigabe.
  - Regel-Editor: nur falls ein regelbezogenes Freigabe-Feld entschieden wird.
- Tests (`VideoWebPlayer.Tests/`)
  - Neue Service-Tests (Muster `ProgramScheduleServiceTests*.cs`) gegen echtes SQLite: gemischte Freigaben über Fenstergrenzen, MPAA-Mapping auf `AgeRatingMinAge`, konfigurierbares Verhalten bei fehlender Freigabe, Cursor-Verhalten (kein dauerhaftes Verklemmen), Tages-/Wochenübergang (begonnener Beitrag läuft zu Ende), Diagnose-Grund, ggf. Regel-Feld-Migration + Alt-Backup-Regression.
  - `TVSenderAdminE2ETests`: Diagnose-Anzeige und Einstellungs-UI.
- Dokumentation: `docs/help/tv-sender.md` (Fensterschema, Konfiguration, Diagnose), `docs/help/tv-sender-datenmodell.md`/`tv-sender-ablauf-technisch.md`, `README.md` und `docs/RELEASE_NOTES.md` bei Bedarf.

## Implementierungsansatz

- Zentrales „Jugendschutzfenster"-Konzept: eine konfigurierbare Abbildung Freigabe → früheste lokale Sendezeit. Die lokale Zeit wird über `TimeZoneInfo.Local` bestimmt — konsistent zur Auswertung von `ProgramRule.TimeOfDay` in `BuildOccurrences` (Zeile ~1373). Referenzrahmen: FSK 0/6 jederzeit, FSK 12 ab 20:00, FSK 16 ab 22:00, FSK 18 ab 23:00 (bis 06:00). MPAA-Werte werden über `AgeRatingMinAge` auf dieselben Fenster gemappt (z. B. `R`→17 ≙ FSK 16-Fenster, `NC-17`→18 ≙ FSK 18-Fenster; Zwischenwerte wie `PG-13`→13 fallen in das nächsthöhere bzw. passende Fenster — Annahme, Schwellen werden an den Mindestaltern festgemacht).
- Filterpunkt bei der Material-Auswahl pro Occurrence: `SelectItems` erhält die lokale Air-Time der Sendeplätze und überspringt Titel, deren Freigabe das Fenster nicht erlaubt. Da die Auswahl ringbasiert ist (`StrandState`/`RuleState`-Cursor), muss ein übersprungener Titel beim nächsten zulässigen Fenster wieder angeboten werden — der Cursor darf nicht dauerhaft auf einem unzulässigen Head klemmen, aber die Ausstrahlungsreihenfolge bleibt gewahrt.
- Tages-/Wochenübergang: konsistent zur bestehenden Boundary-Regel „a started item always completes" läuft ein bereits begonnener Beitrag über die Fenstergrenze hinaus zu Ende; die Grenze gilt für neu eingeplante Sendeplätze (Startzeitpunkt im Fenster).
- Diagnose: `GetRuleDiagnosesAsync`/`ExpandMaterialAsync` zählen zusätzlich, ob eine Regel wegen des Jugendschutzfensters in ihrem Zeitfenster nichts planen kann; `TVSenderAdmin.DiagnosisLabel` gibt den Grund aus.
- Unbekannte Freigabe (`AgeRatingMinAge == null`): Standardverhalten konfigurierbar — konservativ (wie die höchste Stufe behandeln, d. h. nur Nachtfenster) oder freigeben; der Default ist zu definieren.
- `ProgramGuideService`/Live-TV bleiben unverändert — sie zeigen nur den fertig geplanten Plan.
- Clips/Trailer/Standby (`ProgramClipGroup`, `ExpandTrailersAsync`, `ExpandMediaSourceClipsAsync`, `LiveStreamStandbyMediaItemId`) tragen keine `IAgeRated`-Freigabe — ihr Verhalten im Fenster ist zu definieren (Annahme: sie gelten als „ohne bekannte Freigabe" und folgen der konfigurierten Unbekannt-Regel).
- Neue Tabellen/Spalten (soweit entschieden): EF-Migration, `OptionalRestoreColumns`-Einträge und Regressionstest mit Alt-Backup — siehe Definition of Done in `AGENTS.md`.

## Konfiguration

- **Senderweit** über `Setup`-Spalten + `ProgramSettingsService` (Admin-UI, Reiter „Einstellungen" in `TVSenderAdmin.razor`) — gleiche Ebene wie `ProgramScheduleHorizonHours`, `HolidayFederalState` und `ProgramScheduleContentScope`:
  - Fenstertabelle: früheste lokale Sendezeit je Freigabestufe (Default 20:00/22:00/23:00 für FSK 12/16/18).
  - Verhalten bei unbekannter Freigabe: Enum (z. B. `ProgramScheduleUnknownRatingMode`) mit konservativem Standard (nur Nachtfenster) — Default explizit festzulegen.
- **Je `ProgramRule`**: nur falls die Klärung der offenen Frage eine regelspezifische Begrenzung/Ausnahme ergibt (z. B. explizite „Abend-Regel" ohne Begrenzung) — dann als neue `ProgramRule`-Spalte im Regel-Editor.
- Einstellungen müssen dokumentiert werden (`docs/help/tv-sender.md`), da das Akzeptanzkriterium dokumentiertes, konfigurierbares Verhalten mit definiertem Standard verlangt.

## Offene Fragen

- Gilt die Fensterregelung senderweit einheitlich oder zusätzlich je `ProgramRule` (z. B. explizite „Abend-Regel" ohne Begrenzung)?
- Standardverhalten bei unbekannter Freigabe: nur Nachtfenster (konservativ) oder uneingeschränkt?
- Sind die Grenzzeiten (20/22/23 Uhr) fest oder je Installation konfigurierbar?
