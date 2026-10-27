# Bestandsaufnahme: TV-Sender — altersgerechte Sendezeiten (Jugendschutzfenster, issue #271)

Analysiert wurde die Sendeplan-Erzeugung (`ProgramScheduleService`), das Datenmodell der Altersfreigabe (`IAgeRated`), Einstellungen (`Setup`/`ProgramSettingsService`), Admin-UI (`TVSenderAdmin.razor`), Backup-Mechanik und die Testlandschaft — bezogen auf die Anforderung `requirement.md` (Jugendschutzfenster-Regelung in `EnsureCoverageAsync`/`ReconcileAsync`).

## Zusammenfassung

- **Freigabe-Daten vorhanden:** `Movie`/`TVShow` implementieren `IAgeRated` mit `AgeRating`, `AgeRatingRaw`, `AgeRatingMinAge` (vergleichbares Mindestalter, `null` bei unbekannter Stufe), `AgeRatingClassifiedAt`. `AgeRatingNormalizer` liefert kanonische Stufen (FSK 0/6/12/16/18; MPAA G→0, PG→8, PG-13→13, R→17, NC-17→18) und ist die einzige Normalisierungsquelle. `TVShowEpisode` trägt keine Freigabe — sie gilt von der `TVShow` (muss bei Episoden-Expansion nachgeladen werden).
- **Materialisierung:** zwei parallele Pfade — `MaterializeOccurrence` (Extend-only) und `MaterializeOccurrenceEvicting` (Reconcile) — beide über `SelectItems` → `SelectAiringItems`/`SelectAiringItemsInterleaved`. `SchedulableItem` trägt **keine** Freigabe — hier ist die zentrale Erweiterungsstelle. Ringbasierte Cursor (`StrandState`/`RuleState`, `ProgramRuleCursor` mit `StrandKey`) werden nur für real platzierte Items committed (`CommitTaken`) — ein übersprungener Titel bleibt im Ring und wird später erneut angeboten, solange der Skip **vor** dem Commit bleibt.
- **Fenster-Quelle für lokale Zeit:** `BuildOccurrences` rechnet `ProgramRule.TimeOfDay` über `TimeZoneInfo.Local` in UTC — dieselbe Basis, die das Jugendschutzfenster braucht.
- **Einstellungen:** Muster `Setup.ProgramScheduleContentScope` + `Get/UpdateProgramScheduleContentScopeAsync` (`Enum.IsDefined`-Normalisierung/-Validierung) + Select in `TVSenderAdmin.razor` (Planungs-Karte) + `OptionalRestoreColumns`-Eintrag — direktes Vorbild für Fenstertabelle und Unbekannt-Freigabe-Modus.
- **Diagnose:** `GetRuleDiagnosesAsync` → `ProgramRuleDiagnosis` (8 Felder) → `DiagnosisLabel` — Grund «kein altersgerechtes Material im Fenster» existiert **nicht**, muss ergänzt werden (Record-Feld + `SummaryCounters`-Zähler + UI-Text).
- **Füller/Clips/Trailer:** `FillGaps`, `InsertClipFill`/`NextClipItem`, `ExpandTrailersAsync`, `ExpandMediaSourceClipsAsync`, `ProgramClipGroup`, `LiveStreamStandbyMediaItemId` tragen keine `IAgeRated`-Freigabe — Verhalten im Fenster muss über die «unbekannte Freigabe»-Regel definiert werden.
- **Reconcile-Falle:** die Airing-Reproduzierbarkeit (`AiringReproducible`/`FlatAiringTailReproducible`/`InterleavedAiringTailReproducible`) simuliert die Auswahl nach — Fenster-Logik in der Selektion muss mit der Validierung konsistent bleiben, sonst werden bestehende Slots massenhaft displaced.
- **Fehlend:** Fenstertabelle (Freigabe → früheste Sendezeit), Enum für unbekannte Freigabe, Freigabe in `SchedulableItem`, Fenster-Diagnosegrund, regelspezifisches Feld in `ProgramRule` (offene Frage), Hilfe-Abschnitt zum Fensterschema.
- **Test-Ausgangszustand:** Build (Debug) fehlerfrei; volle Testsuite **2526 erfolgreich / 0 fehlgeschlagen / 2 übersprungen** (beide beabsichtigt — Unix-only Test und dokumentierter bekannter Defekt ohne Bezug zum Issue). Nachweis: [tests.md](inventory/tests.md). Jeder nach der Umsetzung rote Test ist neu eingeführt.

## Details

- [Datenmodell](inventory/models.md) — `ProgramRule`, `Setup`, `Movie`/`TVShow` (`IAgeRated`), `ProgramSlot`, `ProgramRuleCursor`
- [Logik](inventory/logic.md) — `ProgramScheduleService` (Materialisierung, Auswahl, Expansion, Cursor, FillGaps, Diagnose), `ProgramSettingsService`, `TVSenderAdmin.razor`, `AgeRatingNormalizer`
- [Enums](inventory/enums.md) — `ProgramScheduleContentScope` (Vorbild), `ProgramRuleType`, `ProgramRuleRecurrence`, `ProgramRuleMaterialType`, `ProgramCursorContentType`, `ProgramSlotStatus`
- [Interfaces](inventory/interfaces.md) — `IProgramScheduleService` + `ProgramRuleDiagnosis`, `IAgeRated`, `ITmdbAgeRatingService`
- [Tests](inventory/tests.md) — Ausgangszustand (2526/0/2), Testklassen, `ProgramScheduleTestDb`-Helfer, E2E
