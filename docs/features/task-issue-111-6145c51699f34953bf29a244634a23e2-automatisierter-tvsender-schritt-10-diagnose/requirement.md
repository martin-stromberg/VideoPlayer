# Übersetzte Anforderung — Schritt 10: Planungsdiagnose + manueller Generierungsanstoß (N5)

Quelle: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/nachbesserungen.md` §N5.

## Fachliche Zusammenfassung

Die Admin-Seite `/admin/tv-sender` (`TVSenderAdmin.razor`) soll pro Programmregel eine
**Planungsdiagnose** anzeigen: Wie viele Inhalte sind für diese Regel aktuell planbar, und —
falls nichts planbar ist — warum nicht (Material verwaist/gelöscht, keine Laufzeit ermittelt,
nicht für alle Benutzer freigegeben). Zusätzlich erhält die Sendeplan-Karte einen
**„Jetzt generieren"-Button**, der `IProgramScheduleSignal.NotifyScheduleChanged()` auslöst,
damit der Hintergrundlauf (`ProgramScheduleWorker`) den Plan sofort neu bewertet, ohne auf das
Generierungsintervall zu warten.

Die Diagnose wird beim Laden der Admin-Seite on-demand berechnet (gleiche Expansionslogik wie
`ProgramScheduleService`), es wird kein neuer persistenter Zustand angelegt — keine Migration
erforderlich. Bei der Aufschlüsselung „keine Laufzeit" wird anhand von
`MediaItem.DurationProbedAt`/`DurationProbeAttempts` unterschieden, ob die Laufzeitermittlung
noch aussteht/erneut versucht wird oder dauerhaft gescheitert ist
(`DurationProbeAttempts >= MediaProbeOptions.EffectiveMaxAttempts`).

## Betroffene Klassen und Komponenten

- `VideoWebPlayer/Services/IProgramScheduleService.cs`
  - Neues Record `ProgramRuleDiagnosis` (pro Regel: `MaterialMissing`, `CandidateCount`,
    `SchedulableCount`, `NoMediaItemCount`, `ProbePendingCount`, `ProbeExhaustedCount`,
    `NoAccessCount`).
  - Neue Methode `GetRuleDiagnosesAsync(string userId, long channelId, CancellationToken)` —
    serverseitige Admin-Prüfung analog `ProgramRuleService.RequireAdminAsync`.
- `VideoWebPlayer/Services/ProgramScheduleService.cs`
  - `SummaryCounters` wird um Diagnosezähler erweitert (`Candidates`, `MaterialMissing`,
    `NoMediaItem`, `ProbePending`, `ProbeExhausted`); die öffentlichen Run-/Reconcile-Summaries
    bleiben unverändert.
  - `MovieMediaItemsAsync`/`EpisodeMediaItemsAsync` liefern künftig alle verknüpften
    MediaItems inkl. Probezustand (`Duration`, `DurationProbedAt`, `DurationProbeAttempts`),
    damit die Skip-Gründe differenziert werden können.
  - Die Expand-Methoden markieren „Material verwaist" explizit (Referenz nicht auflösbar bzw.
    Genre-Auswahl löst zu keiner wirksamen Genre-Menge auf).
  - Neue DI-Abhängigkeit `IOptions<MediaProbeOptions>` (optional, Default
    `EffectiveMaxAttempts` = 10) für die Erschöpfungs-Schwelle.
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
  - Neue Tabellenspalte „Planbar"/Diagnose in der Programmregeln-Tabelle
    (`.tvsender-rule-diagnosis`), Befüllung beim Seitenladen und nach Regel-Änderungen.
  - Neuer Button `#tvsender-generate-now` auf der Sendeplan-Karte; injiziert
    `IProgramScheduleSignal`, löst `NotifyScheduleChanged()` aus, zeigt Statusmeldung und
    lädt Sendeplan + Diagnosen verzögert neu.
- Tests
  - `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Diagnosis.cs` (neu): planbare Zählung,
    verwaistes Material, ohne Mediendatei, Probe ausstehend/dauerhaft gescheitert,
    nicht freigegeben, Genre-Auflösung leer, Admin-Erzwingung.
  - `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs`: bestehenden E2E-Fall erweitern
    (Diagnose-Spalte sichtbar, „Jetzt generieren" auslösbar) — konsolidiert, kein neuer
    Testlauf pro Aspekt.

## Implementierungsansatz

- `ProgramScheduleService.GetRuleDiagnosesAsync` lädt alle Regeln des Senders (inkl. inaktiver,
  für die Diagnoseanzeige), baut einmal den `AccessContext` auf und ruft pro Regel
  `ExpandMaterialAsync` mit einem frischen `SummaryCounters` auf — Wiederverwendung der
  kompletten Expand-Logik (Genre-Union, Quell-Filter, Freigabe, Laufzeit), keine Duplizierung.
- Die Expansionsmethoden liefern über die erweiterten `SummaryCounters` die Detailgründe;
  die Zählreihenfolge bleibt wie gehabt: erst Laufzeit/Mediendatei, dann Freigabe.
- Button: trivialer Signal-Aufruf; Rückmeldung per Statustext. Die Admin-Gate erfolgt wie
  bisher seitenbasiert (`isAdmin`), zusätzlich ist `GetRuleDiagnosesAsync` serverseitig per
  `RequireAdminAsync` abgesichert (Muster aus `ProgramRuleService`).

## Konfiguration

Keine neue Konfiguration. Die Probe-Erschöpfungsschwelle nutzt die bestehende
`MediaProbeOptions.EffectiveMaxAttempts`.

## Offene Fragen

Keine — die Anforderung (inkl. Design-Hinweis „on-demand zählen") ist eindeutig.
