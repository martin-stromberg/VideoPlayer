# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Zeitbasiert, zustandslos, neustartfest | `IProgramGuideService`-Methoden mit explizitem `utcNow`; reine DB-Leselogik ohne Prozesszustand (Designentscheidung, Ablauf 1) | Service-Tests (alle `GetNowAsync`-/`GetUpcomingAsync`-Tests prüfen genau dies) | Abgedeckt |
| Laufende Sendung + Position + Restdauer | Ablauf „Jetzt/Gleich": `Scheduled` + `Start <= now < End`; `PositionSeconds`/`RemainingSeconds` auf `DtoProgramGuideEntry` | `GetNowAsync_RunningSlot_ReturnsEntryWithPositionAndRemaining`, `...ExactlyAtSlotEnd...`, E2E `PairedDevice_RequestNow_ReturnsCurrentAndNext` | Abgedeckt |
| Nächste Sendung | `Next`: frühester `Scheduled`-Slot mit `Start >= now` (Ablauf 1, Schritt 4) | `GetNowAsync_RunningSlot...`, `GetNowAsync_ScheduleGap_ReturnsNullNowButNamesNext` | Abgedeckt |
| Verdrängte Slots nicht in Auskunft | Alle Queries filtern `Status == Scheduled` (Abläufe + Validierungsregeln) | `GetNowAsync_DisplacedSlotCoveringNow_IsNotReported`, `GetUpcomingAsync_ExcludesSlotsBeyondWindow_AndDisplaced` | Abgedeckt |
| Sendelücke ohne Fehler | `Now == null` wenn kein Slot `now` deckt; `Next` weiterhin (Ablauf 1) | `GetNowAsync_ScheduleGap_ReturnsNullNowButNamesNext`, `GetNowAsync_NoChannel_ReturnsEmptyNowAndNext` | Abgedeckt |
| Abgesicherte Schnittstelle für angemeldete Benutzer | `[BearerTokenCheck]` + `CheckLogedIn()` (Ablauf 1), `access_token`-Query durch Attribut | E2E `Anonymous_RequestNow_Returns401`, `PairedDevice_RequestNow...`, `...WithAccessTokenQuery...`; Controller-Tests `...Anonymous_Returns401` | Abgedeckt |
| Begrenzte Vorschau; voller Plan administrativ | `GET upcoming?hours=` mit harter Obergrenze `MaxPreviewHours` (=12) → 400 darüber; voller Plan bleibt `GetScheduleAsync`/Admin-UI | `GetUpcomingAsync_InvalidPreview_Throws`, `GetUpcoming_HoursOutOfRange_Returns400`, E2E `...ReturnsBoundedEntries` | Abgedeckt |
| Titelauflösung + weiche Referenzen | Batch-Lookup (Muster `TVSenderAdmin`), Fallback „Unbekannt" | `GetNowAsync_EpisodeSlot_ReturnsFormattedEpisodeTitle`, `GetNowAsync_DeletedMovie_FallsBackToUnknownTitle` | Abgedeckt |
| Kein Schema-Zwang (keine Migration/Backup nötig) | Abschnitt „Datenbankmigrationen: Keine" — nur additive Code-Artefakte | — (kein DB-Test nötig) | Abgedeckt |
| Namensabgrenzung (kein „Playlist"-Naming) | `ProgramGuide*`/`api/program-guide`, expliziter Hinweis in „Seiteneffekte" + Review-Pflicht | — (Konventionsprüfung im Code-Review) | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine — jedes Akzeptanzkriterium hat mindestens einen konkreten Testnachweis; Berechtigungs- und
Filterlogik ist zusätzlich auf Service-/Controller-Ebene abgesichert.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Anonymer Aufruf → 401 | `ProgramGuideE2ETests.Anonymous_RequestNow_Returns401` | Abgedeckt |
| Angemeldeter Benutzer (gekoppeltes Gerät) → Jetzt/Gleich | `ProgramGuideE2ETests.PairedDevice_RequestNow_ReturnsCurrentAndNext` | Abgedeckt |
| `access_token`-Abfrageparameter | `ProgramGuideE2ETests.PairedDevice_RequestNowWithAccessTokenQuery_Returns200` | Abgedeckt |
| Begrenzte Vorschau + Servergrenze | `ProgramGuideE2ETests.PairedDevice_RequestUpcoming_ReturnsBoundedEntries` (inkl. `hours=99` → 400) | Abgedeckt |
| UI-Fluss | — | Nicht erforderlich: Schritt ist reine API; Live-TV-Ansicht ist Projekt-Schritt 7. Begründet im Plan („Seiteneffekte"). |

## Fehlende oder unvollständige Planbestandteile

Keine.

## Hinweise

- `ProgramGuideService.MaxPreviewHours` liegt als `const` auf der Implementierungsklasse; der
  Controller darf den Wert in Fehlermeldungen referenzieren (über Interface erreichbar, wenn als
  `public const` deklariert — statischer Member, kein Instanzzugriff nötig).
- `hours` als `int` (Default 6) deckt den fachlichen Bedarf („kommende Stunden") ab; gebrochene
  Stunden sind nicht verlangt.
