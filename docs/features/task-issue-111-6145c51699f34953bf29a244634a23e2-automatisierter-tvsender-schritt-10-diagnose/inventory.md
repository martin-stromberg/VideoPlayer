# Bestandsaufnahme: Planungsdiagnose + manueller Generierungsanstoß (N5)

Analyse des TV-Sender-Bereichs (Sendeplan-Erzeugung, Regelverwaltung, Admin-UI) bezogen auf die
Anforderung, pro Regel eine Planungsdiagnose in `TVSenderAdmin.razor` anzuzeigen und einen
„Jetzt generieren"-Button über `IProgramScheduleSignal` anzubieten.

## Zusammenfassung

- `ProgramScheduleService` expandiert Regel-Material bereits vollständig (Genre-Union mit
  kanonischer Auflösung + Quell-Filter seit Schritt 9, Freigabe-Filter via `AccessContext`,
  Laufzeit-Filter via `MediaItem.Duration != null`). Die Begründungszähler existieren als
  interne `SummaryCounters` (`SkippedNoDuration`, `SkippedNoAccess`, `OrphanedRules`) — sie
  werden aber nur läufübergreifend aggregiert und nur ins Log geschrieben, nicht pro Regel
  ausgegeben und nicht differenziert (keine Mediendatei vs. Probe ausstehend vs. Probe
  dauerhaft gescheitert).
- `MovieMediaItemsAsync`/`EpisodeMediaItemsAsync` liefern aktuell nur Links mit
  `Duration != null` — „kein MediaItem" und „keine Laufzeit" sind so nicht trennbar; für die
  Diagnose muss der Probezustand (`DurationProbedAt`, `DurationProbeAttempts`) mitgeladen
  werden.
- `MediaProbeOptions.EffectiveMaxAttempts` (Default 10) ist die Erschöpfungsschwelle, bereits
  via `services.Configure<MediaProbeOptions>` im DI-Container; `ProgramScheduleService`
  injiziert sie bislang nicht.
- `IProgramScheduleSignal` ist Singleton (`ProgramScheduleSignal`), wird bereits von
  `ProgramRuleService` (CRUD) und `MediaDurationProbeService` genutzt; der
  `ProgramScheduleWorker` wacht bei Signal sofort auf. Der Button ist damit ein einfacher
  Injekt + Aufruf.
- `TVSenderAdmin.razor` lädt Regeln in `OnInitializedAsync`/`ReloadRulesAsync`; Admin-Gate
  seitenbasiert (`isAdmin`-Claim), Service-Methoden prüfen zusätzlich serverseitig
  (`RequireAdminAsync`-Muster in `ProgramRuleService`).
- Es fehlt: eine öffentliche Diagnose-API am `IProgramScheduleService`, die pro Regel die
  Expand-Ergebnisse mit Begründungszählern liefert, sowie die UI-Anzeige und der Button.

Test-Ausgangszustand: gefilterter Lauf der betroffenen Suiten (ProgramSchedule*, ProgramRule*,
ProgramGuide*, TVSenderAdmin*) — **113 erfolgreich, 0 fehlgeschlagen, 0 übersprungen**
(Debug-Build, 0 Fehler). Details: [Tests](inventory/tests.md). Der volle Suite-Lauf
(~1998 Tests) steht für den Abschluss aus.

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
