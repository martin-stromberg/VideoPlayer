# Bestandsaufnahme: Sendeplan aus Regeln erzeugen (Schritt 3)

Analyse des Projektstands bezogen auf die Anforderung, aus den aktiven `ProgramRule`-Datensätzen eines `TVChannel` einen rollierenden, lückenlos belegten Sendeplan aus Sendeplätzen zu erzeugen.

## Zusammenfassung

- **Bereits vorhanden (Schritte 1+2):** `MediaItem.Duration`/`DurationProbedAt` (Laufzeit je Datei), `TVChannel` (kanalfähig, genau ein Sender via `ProgramRuleService.GetOrCreateChannelAsync`), `ProgramRule` mit allen Regelarten (`ProgramRuleType`), Wiederholungsmustern (`ProgramRuleRecurrence`), Materialarten (`ProgramRuleMaterialType`, polymorphe `MaterialType`/`MaterialId`-Referenz ohne DB-FK) und `Priority`; Admin-CRUD in `ProgramRuleService` mit serverseitiger Admin-Erzwingung; `Setup.HolidayFederalState` + `ProgramSettingsService` für das konfigurierbare Bundesland.
- **Freigabe-Infrastruktur:** `MediaSourceUser` (Quellenzugriff je Benutzer), `UnlockedMediaEntry` (Einzelfreischaltung auf `TVShow`-/`MovieCollection`-Ebene), `IUnlockedMediaService.IsAccessible(hasSourceAccess, isUnlocked)` als kanonische Zugriffsregel; Admins umgehen die Quellenfilterung faktisch (Claim-basiert in Controllern).
- **Hintergrundverarbeitung:** zwei etablierte Muster — einfacher Poll-Worker (`MediaDurationProbeWorker`: Scope-Factory + `IBackgroundProcessingGate` + `TimeProvider`) und Signal-getriebener Worker (`PlaylistBackfillWorker` + `IPlaylistBackfillSignal` + `PlaylistBackfillCoordinator` mit Scope-Factory, Gate-Lease pro Arbeitseinheit, `Setup`-persistierter Laufzeitstempel).
- **Fehlt noch:** Sendeplatz-Entität (`ProgramSlot`), Fortsetzungsstand je Regel, Feiertagskalender-Auswertung, Planerzeugungs-Logik (Expansion der Materialien, Konfliktauflösung, Lückenfüllung), Hintergrund-Fortschreibung, Konfiguration von Horizont/Intervall.

Test-Ausgangszustand: siehe [inventory/tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
