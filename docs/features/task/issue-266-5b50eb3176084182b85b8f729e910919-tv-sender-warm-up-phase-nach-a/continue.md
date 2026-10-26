# Offene Aufgaben

Erstellt am: 2026-10-26
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen (12 offene Punkte in Iteration 1 → 13 in Iteration 2)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine. (`review.md`: Vollständig umgesetzt)

## Code-Review-Befunde

- [ ] `LiveStreamStandbySegmenter.cs` — Toter Code: `using System.Globalization;` (Z. 4) ungenutzt → Using entfernen.
- [ ] `LiveStreamStandbySegmenter.cs` — Fehlerbehandlung/Logik: Alt-Segment-Löschung (Z. 111–115) schluckt `IOException`/`UnauthorizedAccessException` lautlos; `Directory.GetFiles(…, "standby_*.ts")` (Z. 186) nimmt nicht löschbare Altsegmente unbemerkt in den neuen Satz auf → Löschfehler loggen bzw. Aufnahme auf die produzierten Dateien beschränken.
- [ ] `LiveStreamStandbySegmenter.cs` — Fehlerbehandlung/Timeout: `stderrTail = await stderrTask` (Z. 160) unbegrenzt; bei fehlgeschlagenem `Kill()` (Catch ohne Log, Z. 156) hängt `TranscodeAsync` endlos → `stderrTask` begrenzen (`WaitAsync`) und Kill-Fehler loggen.
- [ ] `SegmenterProcessSupport.cs` — Fehlerbehandlung: `catch (Exception) { return null; }` in `StartRealProcess` (Z. 47–50) verschluckt die Startursache → Exception mitloggen oder an den Aufrufer weiterreichen.
- [ ] `LiveStreamStandbyProvider.cs` — Toter Code: `StandbyCacheMarker.PreparedAtUtc` (Z. 490) write-only; wird zudem mit `DateTime.UtcNow` statt `_timeProvider` gesetzt → Feld entfernen oder fachlich nutzen (bei Behalten `_timeProvider` verwenden).
- [ ] `LiveStreamController.cs` — Toter Filter/falsche Statuszuordnung: `or UnauthorizedAccessException` im Catch-Filter (Z. 120) unerreichbar (Z. 115 fängt zuerst); echte Datei-Zugriffsfehler werden als 401 statt 500 gemeldet → Filterteil entfernen und 401-Behandlung auf den Anmeldefehler beschränken.
- [ ] `FfmpegLiveStreamSegmenterE2ETests.cs` — Doppelter Code: dritte `FindBinary`-Kopie (Z. 200, Aufrufe Z. 21/77) besteht weiter → löschen und `E2EBinaryLocator.Find("ffmpeg")` verwenden.
- [ ] `LiveTvE2ETests.cs` — Dokumentation: `<summary>`-Block (Z. 41–46) hängt vor der falschen Methode; `SeedChannelWithRunningProgramAsync` hat keinen Doc-Kommentar → Kommentar zurück über `SeedChannelWithRunningProgramAsync` verschieben.

## Usability-Befunde

- [ ] `TVSenderAdmin.razor` — Sackgasse beim Speichern: Bei gelöschtem Standby-Clip suggeriert der Chip „Standard-Clip wird verwendet", die verwaiste Auswahl bleibt aber gesetzt und jedes „Speichern" scheitert mit „Das gewählte Standby-Video wurde nicht gefunden." → verwaiste Auswahl beim Anzeigen des Fallback-Textes still zurücksetzen oder „Auswahl verwerfen" anbieten; „Speichern" muss im angezeigten Zustand funktionieren.
- [ ] `TVSenderAdmin.razor` — Fachjargon: Überschrift „Standby-Clip (Warm-up-Phase)" und Hilfetext mit „Wiedergabeliste"/„produzierte Segmente"/„nicht klassifiziert" → anwenderorientiert formulieren, z. B. „Wird gezeigt, bis der Live-Stream nach einem Server-Start wirklich läuft. Auswählbar sind Videodateien ohne Film-/Serien-Zuordnung."
- [ ] `TVSenderAdmin.razor` — Ungespeicherte Auswahl ohne Hinweis: „Auswählen"/„Standard-Clip verwenden" wirken erst nach „Speichern"; anders als bei Clip-Gruppen (sofort wirksam) → Hinweis am Chip (z. B. „Auswahl wird mit ‚Speichern' übernommen") oder sofort speichern.
- [ ] `TVSenderAdmin.razor` — Picker-Bedienung: Knopf bleibt bei geöffnetem Picker „Ändern", kein „Schließen", kein `aria-expanded` → Beschriftung wechseln („Auswahl schließen") und `aria-expanded` setzen.
- [ ] `TVSenderAdmin.razor` — Leerzustand-Flash/kein Lade-Feedback: „Keine zuordenbaren Clips gefunden" erscheint, während die erste Abfrage läuft; „Weitere laden" ohne Rückmeldung → „Clips werden geladen…" anzeigen und „Weitere laden" sperren.

## Fehlgeschlagene Tests

Keine. (`test-results.md`: Keine Fehler — 2486/2488 grün, 2 bekannte Skips; 2 einmalige Playwright-Flacker bei Einzelwiederholung bestanden.)
