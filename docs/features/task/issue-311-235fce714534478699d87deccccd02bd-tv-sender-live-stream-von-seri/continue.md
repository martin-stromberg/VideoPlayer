# Offene Aufgaben

Erstellt am: 2026-10-29
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — Plan-Review meldet „Vollständig umgesetzt".

## Code-Review-Befunde

- [ ] `LiveStreamCoordinator.cs:59-61,130-151` — Eskalationsgedächtnis (`_inputFailureSlotId`/`_slotHttpInputFailed`/`_slotSftpInputFailed`) liegt in Instanzfeldern des per `AddScoped` registrierten Koordinators, den der Worker je Tick in einem frischen Scope auflöst (`LiveStreamWorker.cs:92`) — das Gedächtnis überlebt keinen Tick, die HTTP↔sftp-Alternation bleibt produktiv bestehen und `pipe:0` wird nie erreicht (Mutationsprobe bestätigt). Fix: Gedächtnis in einen Singleton verlagern (z. B. `FfmpegLiveStreamSegmenter` neben `SftpInputSupported`/`LastUnexpectedExit`, oder über `LiveStreamExitInfo` mitführen).
- [ ] `LiveStreamCoordinatorTests.cs:456/486/509` — die Kaskaden-Tests prüfen mehrere „Ticks" auf derselben Koordinator-Instanz (Lebensdauer existiert produktiv nicht) — Befund 1 blieb dadurch unentdeckt. Tests auf frischen Koordinator pro Tick umstellen (Gedächtnis liegt dann im Singleton/Fake, wo es produktiv hingehört).
- [ ] `FfmpegLiveStreamSegmenter.cs:297-349` — zwischen `ReferenceEquals`-Check und `_lastUnexpectedExit`-Write liegt das ≤10-s-Pump-Await; ein dazwischen laufender `StopCoreAsync`/`StartAsync` setzt das Feld zurück und die verspätete Klassifikation überschreibt es mit Alt-Diagnose. Empfehlung: Re-Check vor dem Write.

## Usability-Befunde

Keine.

## Fehlgeschlagene Tests

Keine — volle Suite grün (2818 bestanden, 2 übersprungen, 0 fehlgeschlagen).
