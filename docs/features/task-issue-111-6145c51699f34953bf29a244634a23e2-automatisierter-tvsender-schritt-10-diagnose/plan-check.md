# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Pro Regel Diagnose: Anzahl planbarer Inhalte | `ProgramRuleDiagnosis.SchedulableCount` via `GetRuleDiagnosesAsync` (Wiederverwendung `ExpandMaterialAsync`) + Spalte in `TVSenderAdmin.razor` | `Diagnosis_SchedulableMovie_CountsSchedulable`; E2E Diagnose-Zelle | Abgedeckt |
| Grund „keine Laufzeit ermittelt" | `NoMediaItemCount`/`ProbePendingCount`/`ProbeExhaustedCount` über `MediaItemRef`-Lookup mit Probezustand; `SkippedNoDuration`-Semantik bleibt | `…NoMediaItem`, `…UnprobedItem`, `…FailedProbeBelowMax`, `…ExhaustedProbe` | Abgedeckt |
| Grund „nicht für alle freigegeben" | `NoAccessCount` aus bestehendem `AccessContext` | `Diagnosis_UnsharedContent_CountsNoAccess` | Abgedeckt |
| Grund „Material verwaist" | `MaterialMissing` an allen Early-Returns der Expand-Methoden (Entity fehlt; Genre-Auflösung leer) | `Diagnosis_DeletedMaterial_MarksMissing`, `Diagnosis_GenreResolvingToNothing_MarksMissing` | Abgedeckt |
| „Jetzt generieren"-Button löst `IProgramScheduleSignal.NotifyScheduleChanged()` aus | Button `#tvsender-generate-now` + `GenerateNowAsync` in `TVSenderAdmin.razor` | E2E: Klick + Statustext; Signal selbst durch `ProgramScheduleSignalTests`/`ProgramScheduleWorkerTests` bereits abgesichert | Abgedeckt |
| Feedback, dass Generierung läuft | Statustext `#tvsender-schedule-status` + verzögertes Reload von Sendeplan/Diagnosen | E2E Statustext | Abgedeckt |
| Admin-only (serverseitig) | `RequireAdminAsync` in `GetRuleDiagnosesAsync`; Seiten-Gate unverändert | `Diagnosis_NonAdmin_Throws`; `NonAdmin_SeesNotAuthorized` (bestehend) | Abgedeckt |
| Keine Migration / kein neuer State | ausdrücklich im Plan (on-demand) | — | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

— (Status `Plan vollständig`)

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Diagnose-Zelle in der Regel-Tabelle sichtbar und begründet | Erweiterung `Admin_CreateEditDeactivateDeleteRule` (geseedete Regel ohne MediaItems → „keine Mediendatei"-Begründung) | Abgedeckt |
| „Jetzt generieren" auslösbar + Rückmeldung | Erweiterung `Admin_CreateEditDeactivateDeleteRule` (Klick + `#tvsender-schedule-status`) | Abgedeckt |
| Nicht-Admin-Zugriff verweigert | `NonAdmin_SeesNotAuthorized` (bestehend, unverändert) | Abgedeckt |

Konsolidierungsregel eingehalten: kein neuer Testmethoden-Launch pro Aspekt, Erweiterung des
bestehenden konsolidierten Laufs.

## Fehlende oder unvollständige Planbestandteile

— (Status `Plan vollständig`)

## Hinweise

- Detail für die Implementierung: `MovieMediaItemsAsync`/`EpisodeMediaItemsAsync` müssen bei
  mehreren MediaItem-Links je Inhalt weiterhin das niedrigste `MediaItemId`-Item **mit**
  Laufzeit wählen (bisherige Semantik); Links ohne Laufzeit nur als Fallback für die
  Probezustands-Diagnose — sonst ändert sich die Slot-Zuordnung.
- Playlist-Einträge auf gelöschte Medien werden still übersprungen (bestehendes Verhalten) →
  `Candidates == 0` bei `MaterialMissing == false` → UI-Text „keine Inhalte im Material" muss
  diesen Fall abdecken.
