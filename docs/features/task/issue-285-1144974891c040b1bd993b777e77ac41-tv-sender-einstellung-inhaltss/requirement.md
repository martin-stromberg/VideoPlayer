# Übersetzte Anforderung: TV-Sender — Einstellung „Inhaltsspektrum" (Freigabe-Prüfung der Planung optional aufheben)

## Fachliche Zusammenfassung

Der automatisierte TV-Sender plant heute ausschließlich Inhalte ein, die für die gesamte
Nicht-Admin-Zuhörerschaft freigegeben sind (Medienquellenzugriff für alle Nicht-Admin-Benutzer
oder Einzelfreischaltung der Serie/Filmsammlung für alle). Diese Prüfung ist zentral im
`AccessContext` des `ProgramScheduleService` gebündelt (`LoadAccessContextAsync` +
`AccessContext.Allows`). Die Anforderung fügt eine senderweite Admin-Einstellung
„Inhaltsspektrum" mit zwei Optionen hinzu: „Nur für alle Benutzer freigegebene Inhalte"
(Default = bisheriges Verhalten) und „Alle Inhalte, die sich aus den Regeln ergeben" — im
zweiten Modus wird die Zuschauer-Freigabe bei der Planung vollständig ignoriert. Die Einstellung
wirkt serverseitig und einheitlich auf alle Expansionspfade: Titel-Expansion (Filme, Episoden —
inkl. Genre- und Playlist-Material), Füller-Material „Clip-Quelle" sowie Clip-Gruppen
einschließlich der System-Gruppe „Trailer" und der Füller-Materialart „Trailer anstehender
Filme".

## Akzeptanzkriterien

1. **Neue Einstellung in der Sender-Verwaltung:** Im Tab „Einstellungen" von
   `TVSenderAdmin.razor` (Gruppe „Planung") wählt der Administrator zwischen den beiden
   Optionen; Default bleibt die restriktive Variante.
2. **Global und einheitlich wirksam:** Die Einstellung gilt senderweit und beeinflusst alle
   Planungspfade gleichermaßen — Titel-Expansion (Filme/Episoden über `TryMovieItem`/`ToItem`),
   Füller „Clip-Quelle" (`ExpandMediaSourceClipsAsync`), Clip-Gruppen
   (`ExpandClipGroupItemsAsync`) und Trailer (`ExpandTrailersAsync` — Materialart und
   System-Clip-Gruppe).
3. **„Alle Inhalte"-Modus:** Die Planung darf Items aus Quellen ohne (vollständige)
   Zuschauer-Freigabe einplanen; `SkippedNoAccess` tritt in diesem Modus nicht mehr auf.
4. **UI-Hinweis:** Die UI macht die Konsequenz deutlich — Zuschauer sehen im Stream Inhalte,
   die sie in der Mediathek nicht sehen dürfen (Hinweistext an der Einstellung).
5. **Wirkung:** Änderung wirkt auf die nächste Planerstellung; das Verhalten im Default bleibt
   unverändert.
6. **Serverseitig und persistiert:** Kein reines UI-Flag — die Auswertung liegt in der
   Planungslogik; die Einstellung wird persistent gespeichert (Setup/Settings) und
   serverseitig durchgesetzt (Admin-Pfad wie üblich über `RequireAdminAsync` bzw. den
   Admin-Service).
7. **Tests:** Beide Modi sind getestet — Planung respektiert Freigaben bzw. ignoriert sie;
   Default unverändert.

## Betroffene Klassen und Komponenten

- **Datenmodell:** `Setup` (neue Spalte für die Inhaltsspektrum-Einstellung, z. B. Bool oder
  Enum-Int — Default restriktiv) — alternativ `TVChannel`, siehe „Offene Fragen". EF-Migration
  für die neue Spalte.
- **Logik/Services:**
  - `ProgramScheduleService` — `LoadAccessContextAsync` liest die Einstellung zusätzlich und
    liefert im „Alle Inhalte"-Modus einen Kontext, der `Allows(...)` stets `true` beantworten
    lässt (der vorhandene `Everything`-Zweig des `AccessContext` ist genau dieser Mechanismus).
    Betroffene Prüfstellen: `TryMovieItem` (~Z. 1877), `ToItem` (~Z. 1921),
    `ExpandTrailersAsync` (~Z. 2132), `FilterClipCandidatesAsync` (~Z. 2438). Alle drei
    Einstiegspunkte — `EnsureCoverageAsync` (~Z. 89), `ReconcileAsync` (~Z. 167) und
    `GetRuleDiagnosesAsync` (~Z. 412) — laden den Kontext über dieselbe Methode und erhalten
    das Verhalten damit automatisch konsistent (inkl. Regel-Diagnose).
  - `ProgramSettingsService` — neuer Getter/Updater nach dem Muster
    `GetHolidayFederalStateAsync`/`UpdateHolidayFederalStateAsync` bzw.
    `UpdateProgramScheduleSettingsAsync` (Validierung, `GetOrCreateSetupAsync`,
    `SaveChangesAsync`).
- **Enums:** ggf. ein Enum für die zwei Spektrum-Optionen (oder Bool — Entscheidung in der
  Planung; die UI zeigt zwei benannte Optionen, ein Enum bildet sie sauberer ab).
- **UI:** `TVSenderAdmin.razor` — Auswahlfeld mit zwei Optionen und Hinweistext in der
  Unterkarte „Planung" des Tabs „Einstellungen" (Muster `#tvsender-horizon-hours`/
  `#tvsender-federal-state`); Verdrahtung in `SaveChannelSettingsAsync` (~Z. 1611) und Laden im
  Initialisierungspfad (~Z. 1035). Nach dem Speichern ist
  `IProgramScheduleSignal.NotifyScheduleChanged()` zu feuern (Konvention für planrelevante
  Änderungen, vgl. `ProgramSettings.razor` Z. 234).
- **Persistenz/Backup:** `VideoWebPlayerBackupData` — neue `Setups`-Spalte in
  `OptionalRestoreColumns` plus Eintrag in der Defaults-Tabelle (Muster
  `(Setups, TvSenderEnabled, false)`, ~Z. 134), damit Alt-Backups ohne die Spalte restriktiv
  restauriert werden.
- **Tests:**
  - `ProgramScheduleServiceTests_Access` (bzw. neue Testdatei): „Alle Inhalte" plant Items aus
    nicht freigegebenen Quellen ein; Default filtert weiter (Film, Episode, Clip-Quelle,
    Clip-Gruppe, Trailer-Gruppe/Trailer-Material); Wechsel der Einstellung wirkt auf den
    nächsten Lauf.
  - `ReconcileAsync`-Verhalten beim Umschalten (zurück auf restriktiv → nicht mehr zulässige
    Slots werden verdrängt).
  - `ProgramSettingsServiceTests` (Getter/Updater, Default).
  - `VideoWebPlayerBackupDataTests_*` (Alt-Backup-Regression ohne die neue Spalte).
  - bUnit/UI-Test für die neue Auswahl inkl. Hinweistext.
- **Dokumentation:** `docs/help/tv-sender.md` (Abschnitt Sendeplan/Einstellungen),
  `docs/help/tv-sender-ablauf-technisch.md`, `docs/help/tv-sender-datenmodell.md` (neue
  `Setup`-Eigenschaft), `RELEASE_NOTES.md` (EN+DE).

## Implementierungsansatz

- Der gemeinsame Sichtbarkeits-Pfad existiert bereits: `AccessContext` ist der zentrale
  „IsVisibleToAudience"-Mechanismus; `Allows(sourceId, showId, collectionId)` wird an genau
  vier Stellen ausgewertet (siehe oben). Der „Alle Inhalte"-Modus lässt sich minimal-invasiv
  umsetzen, indem `LoadAccessContextAsync` die persistierte Einstellung liest und bei
  „Alle Inhalte" einen `AccessContext` mit `Everything = true` zurückgibt — analog zum heutigen
  Fall „keine Nicht-Admin-Benutzer". Damit greifen Titel-, Clip- und Trailer-Pfade ohne
  Einzeländerung; `SkippedNoAccess` bleibt im zweiten Modus zwangsläufig 0.
- Persistenz über eine neue `Setup`-Spalte und `ProgramSettingsService`-Methoden; die
  Planungsdienste lesen `Setup` bereits über denselben Service (`_settings`), sodass keine
  neue Abhängigkeit nötig ist. Die Auswertung liegt ausschließlich serverseitig in der
  Planungslogik — das UI-Flag allein verändert nichts.
- `ReconcileAsync` expandiert über denselben Kontext: Zurückstellen auf restriktiv lässt Slots
  mit nicht mehr zulässigen Inhalten beim nächsten Lauf als nicht reproduzierbar verdrängen
  (Mitgliedschafts-Check gegen die aktuelle Expansion) — das ist das erwünschte „wirkt auf die
  nächste Planerstellung". `GetRuleDiagnosesAsync` reflektiert die Einstellung automatisch
  (gleicher Kontext).
- Berechtigungen: Die Einstellung ist admin-only über die bestehende Admin-Seite; die
  Absicherung der UI-Methoden erfolgt über den gewohnten `RequireAdminAsync`-Pfad der
  Admin-Services bzw. die Seite selbst (`/admin/tv-sender` ist bereits Admin-only).
- Die Durchsetzung betrifft nur die Planung — nicht die Stream-Auslieferung: Der Hinweistext in
  der UI weist ausdrücklich darauf hin, dass Zuschauer im Stream dann Inhalte sehen, die sie in
  der Mediathek nicht sehen dürfen (Annahme: Stream-Zugriff über `TvSenderUserAccess` bleibt
  unverändert und wird nicht verschärft).

## Konfiguration

- Neue Admin-Einstellung (zwei Optionen) — Vorschlag `Setup`-Spalte, z. B.
  `Setup.ProgramScheduleContentScope` (Enum/int) oder Bool `…IncludeUnreleasedContent`,
  Default = restriktiv (`false`/`SharedOnly`). **Nicht** als `ProgramScheduleOptions`/
  appsettings-Schlüssel (wie `TrailerLeadDays`): Die Anforderung verlangt eine in der
  Admin-Oberfläche änderbare, persistierte Einstellung — alle vergleichbaren senderweiten
  Admin-Einstellungen (`TvSenderEnabled`, `HolidayFederalState`, `ProgramScheduleHorizonHours`,
  `LiveStreamStandbyMediaItemId`) liegen in `Setups` und werden über
  `ProgramSettingsService` gepflegt.
- Konfigurationsebene: global/anwendungsweit (`Setups` ist eine Ein-Zeilen-Tabelle) — faktisch
  senderweit, da genau ein Sender ausgeliefert wird.

## Offene Fragen

- **Ablageort der Einstellung:** `Setup`-Spalte (Konvention aller bisherigen
  Sender-Einstellungen, einfache Ein-Zeilen-Persistenz) oder `TVChannel`-Spalte (das Modell
  ist kanalfähig; „global für den Sender" ließe sich wörtlich am Kanal verankern)? Empfehlung:
  `Setup` — konsistent zu `HolidayFederalState`/`ProgramScheduleHorizonHours`/Standby-Clip, die
  ebenfalls senderlogisch, aber global in `Setups` liegen. In der Planung zu entscheiden
  (Annahme: `Setup`).
- **Datentyp:** Bool (zwei Modi, einfacher Restore-Default `false` = restriktiv) oder Enum
  (sprechende Werte, erweiterbar)? Beide erfüllen die Anforderung; Entscheidung in der Planung.
- **Reconcile-Semantik beim Umschalten:** Beim Wechsel zurück auf „nur freigegebene Inhalte"
  werden bereits geplante Slots mit nicht freigegebenen Inhalten beim nächsten
  `ReconcileAsync`-Lauf verdrängt und neu geplant — ist dieses sofortige Aufräumen des Plans
  gewünscht (Annahme: ja, es entspricht „wirkt auf die nächste Planerstellung"), oder soll die
  Änderung nur neu erzeugte Slots betreffen?
- **Diagnose-Anzeige:** `GetRuleDiagnosesAsync` nutzt denselben `AccessContext` — im
  „Alle Inhalte"-Modus zeigt die Regel-Diagnose keine Zugriffs-Ausschlüsse mehr. Annahme:
  gewünscht/konsistent; ggf. kurz bestätigen, dass kein zusätzlicher Hinweis in der Diagnose
  nötig ist.
- **Per-Regel-Override:** Der Issue-Hinweis nennt eine mögliche regelspezifische Option,
  stuft sie aber als „erstmal global" ein — hiermit als bewusst nicht Teil dieser Anforderung
  bestätigt?
