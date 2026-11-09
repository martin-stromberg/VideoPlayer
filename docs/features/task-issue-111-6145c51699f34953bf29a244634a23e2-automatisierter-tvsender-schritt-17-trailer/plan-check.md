# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| 1. Trailer-Erkennung (-trailer, trailers/) | Classifier-Änderungen, Programmablauf 1 | `ClassifyAllAsync_TrailerSuffix_*`, `…TrailersFolder_*` | Abgedeckt |
| 2. Kein regulärer Inhalt | Skip in Movie-/Episode-Pfad + `!IsTrailer`-Filter an allen Hauptdatei-Konsumenten | `…_NotLinkedAsMainItem`, implizit Schedule-Tests | Abgedeckt |
| 3. Duration-Probing | MediaItem bleibt unverändert (automatisch) | `…_TrailerWithoutDuration_NotAired` | Abgedeckt |
| 4. Füller-Bevorzugung + Vorlauf + Fallback | `ExpandTrailersAsync`, `FillGaps`-Lead-Check, `ProgramScheduleOptions` | `…_AirsBeforeScheduledMovie`, `…_NotAired`, `…FallsBackToNormalFiller` | Abgedeckt |
| 5. UI-Materialart | TVSenderAdmin-Änderungen | bUnit + E2E-Regel-Editor-Fall | Abgedeckt |
| 6. Migration + Backup | Migration + OptionalRestore* | Legacy-Backup-Regressionstest | Abgedeckt |
| 7. Dokumentation | Schritt 7 der Reihenfolge | — | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Regel-Editor: Trailer-Materialart wählen + speichern (AK 5) | `TVSenderAdminE2ETests` | Abgedeckt |

## Hinweise

- Verzögerte Expansion in `EnsureCoverageAsync` ist der kritische korrekte Pfad (Erstlauf);
  `ReconcileAsync` expandiert früh mit persistierten Slots (Validierungskonsistenz).
- `ProgramSlot.MovieId` auf Trailer-Slots erfordert Ausschluss der Trailer-MediaItem-Menge beim
  Aufbau der Ausstrahlungskarte — im Plan benannt.
