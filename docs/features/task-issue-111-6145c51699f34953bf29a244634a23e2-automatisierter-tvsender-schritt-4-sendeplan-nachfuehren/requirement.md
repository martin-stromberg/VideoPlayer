# Übersetzte Anforderung: Sendeplan bei Änderungen nachführen (Schritt 4)

Quelle: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/project-plan.md`, Abschnitt „### Schritt 4: Sendeplan bei Änderungen nachführen" samt den Vorgehensentscheidungen in „Grobe Vorgehensentscheidungen". Baut auf Schritt 3 auf (`ProgramSlot`, `ProgramRuleCursor`, `ProgramScheduleService`, `ProgramScheduleWorker`), der bewusst nur „extend-only" erzeugt und die Reaktion auf Änderungen hierher verweist.

## Fachliche Zusammenfassung

Der bestehende Sendeplan (`ProgramSlot`-Bestand je `TVChannel`) bleibt konsistent, wenn sich seine Rahmenbedingungen ändern: Ändert, deaktiviert oder löscht ein Administrator eine `ProgramRule`, oder ändert sich der Medienbestand (neue Episoden einer eingeplanten Serie, gelöschtes eingeplantes Video), bewertet das System den bestehenden Plan neu statt ihn nur fortzuschreiben. Ungültig gewordene Sendeplätze werden mit dem Status „verdrängt" gekennzeichnet (keine harte Löschung — die Planhistorie bleibt nachvollziehbar) und die entstehenden Lücken werden regelkonform neu befüllt. Verdrängte Serienfolgen werden zur nächst passenden Gelegenheit desselben Regelkontexts wieder eingeplant, sodass die Episodenreihenfolge ohne Sprung fortgesetzt wird. Die Nachführung erfolgt automatisiert (durch die Regel-CRUD-Aufrufe und den Medienbestand ausgelöst sowie vom `ProgramScheduleWorker` abgesichert) und nachvollziehbar — der Anwender/Administrator sieht, welche Sendeplätze verdrängt wurden.

## Akzeptanzkriterien (aus der Anforderung abgeleitet)

1. **Re-Bewertung bei Regel-Änderung:** Beim Ändern, Deaktivieren oder Löschen einer `ProgramRule` wird der betroffene Plan des Senders neu bewertet — nicht nur rollierend fortgeschrieben.
2. **Gültige Sendeplätze bleiben:** Sendeplätze, die weiterhin zu den aktiven Regeln passen, bleiben unverändert bestehen — insbesondere laufende und bereits gesendete Sendeplätze.
3. **Ungültige Sendeplätze werden verdrängt:** Sendeplätze, die durch die Änderung ungültig werden, erhalten den Status „verdrängt" und werden durch den neuen Plan ersetzt; sie werden nicht physisch gelöscht.
4. **Verdrängte Serienfolgen wieder einplanen:** Wird eine Serienfolge verdrängt, wird sie zur nächst passenden Gelegenheit desselben Regelkontexts wieder eingeplant, sodass die Episodenreihenfolge ohne Sprung fortgesetzt wird (Cursor-Logik von `ProgramRuleCursor` wird entsprechend zurückgesetzt/korrigiert).
5. **Neue Episoden berücksichtigen:** Neu hinzugekommene Episoden einer eingeplanten Serie werden bei der Fortsetzung des Plans berücksichtigt.
6. **Gelöschtes Video → verdrängt, keine Kaskade:** Wird ein bereits eingeplantes `MediaItem` gelöscht, wird der betroffene Sendeplatz als „verdrängt" gekennzeichnet — keine harte Löschkaskade; die entstehende Lücke wird regelkonform, insbesondere über die `Filler`-Regeln, neu gefüllt.
7. **Automatisiert:** Die Nachführung läuft ohne Anwendereingriff — ausgelöst durch die Regel-CRUD-Operationen (`ProgramRuleService`) und Medienbestands-Änderungen (Scan/Löschung, analog dem `PlaylistBackfillMarker`-/`IPlaylistBackfillSignal`-Muster) sowie abgesichert durch den periodischen `ProgramScheduleWorker`.
8. **Nachvollziehbar:** Der Anwender (Administrator) sieht, welche Sendeplätze verdrängt wurden — der Status „verdrängt" ist im Datenbestand geführt und in der Administration sichtbar.

## Betroffene Klassen und Komponenten

- **Datenmodell:** `ProgramSlot` — Erweiterung um Status (`ProgramSlotStatus` o. ä. mit mindestens Geplant/Verdrängt bzw. bestehender Status-Enum aus Schritt 3 erweitern); ggf. neue EF-Migration + Backup-Registrierung (`OptionalRestoreTables`/`OptionalRestoreColumns`) mit Alt-Backup-Regressionstest; `ProgramRuleCursor` (Korrektur-/Rücksetzlogik für verdrängte Serienfolgen).
- **Logik/Services:** `ProgramScheduleService` — neue Re-Bewertungslogik (Vergleich Soll-Plan vs. Ist-Bestand, Verdrängen, Lückenfüllung, Cursor-Korrektur); `ProgramScheduleWorker` — triggert die Nachführung zusätzlich zum Fortschreiben; `ProgramRuleService` — Auslösung der Re-Bewertung bei Create/Update/(De)Aktivieren/Delete; Medienbestand: `MediaSourceScanner`, `MediaUpdateHub` bzw. Media-Löschpfad — Auslösung bei neuen Episoden bzw. gelöschtem `MediaItem`; Signal-Muster analog `IPlaylistBackfillSignal`/`PlaylistBackfillMarker` (SaveChanges-Hook oder explizites Signal) prüfen und wiederverwenden oder analoges `IProgramScheduleSignal` bauen.
- **UI:** Administration des Sendeplans — sichtbare Kennzeichnung verdrängter Sendeplätze (Anzeige des Status in der Sendeplan-Ansicht des `TVSenderAdmin`-Bereichs).
- **Tests:** neue Testklassen für Re-Bewertung (Regel geändert/deaktiviert/gelöscht), Verdrängen vs. Behalten (inkl. laufender/gesendeter Sendeplätze), Serienfolgen-Wiedereinplanung, neue Episoden, Video-Löschung + Lückenfüllung, Signal-/Trigger-Pfade; Erweiterung bestehender E2E-Tests (`TVSenderAdminE2ETests`) für die Sichtbarkeit verdrängter Sendeplätze; Alt-Backup-Regressionstest falls neue Spalten.

## Implementierungsansatz

- Der `ProgramScheduleService` erhält neben dem bestehenden „extend-only"-Pfad einen Re-Bewertungspfad: Aus den aktiven Regeln wird für den Horizont ein Soll-Plan gerechnet; bestehende Sendeplätze werden dagegen abgeglichen — passende bleiben (inkl. `Start < now` laufende/gesendete), ungültige erhalten `Status = Verdrängt`, frei werdende Zeitfenster werden nach den bestehenden Regeln (Priorität, Konfliktauflösung, Füller) neu belegt.
- Verdrängte Serienfolgen: Der `ProgramRuleCursor` der betroffenen Regel wird so korrigiert, dass die verdrängte(n) Episode(n) bei der nächsten Gelegenheit desselben Regelkontexts erneut eingeplant werden (kein Episodensprung).
- Auslösung: `ProgramRuleService`-CRUD stößt die Re-Bewertung direkt an (oder über ein Signal an den Worker); Medienbestands-Änderungen (neue Episoden nach Scan, gelöschtes Video) über ein Signal analog `IPlaylistBackfillSignal` bzw. den `PlaylistBackfillMarker`-SaveChanges-Hook; der `ProgramScheduleWorker` prüft zusätzlich periodisch (z. B. verwaiste `MediaItem`-Referenzen → verdrängen).
- Status am `ProgramSlot` als persistierter Enum; laufende/gesendete Sendeplätze werden nur dann verdrängt, wenn sie fachlich ungültig sind (Anforderung: „noch gültige Sendeplätze (inkl. laufende/gesendete) bleiben" — Umkehrschluss: ungültige laufende/gesendete werden ebenfalls als verdrängt markiert, aber nicht gelöscht).
- Concurrency: Re-Bewertung hinter `IBackgroundProcessingGate` (Muster aus `ProgramScheduleWorker`/`MediaDurationProbeWorker`), damit UI-Trigger und Worker nicht konkurrierend schreiben.

## Konfiguration

Keine neuen Konfigurationseinträge zwingend vorgesehen; bestehende (`Setup.ProgramScheduleHorizonHours`, `Setup.ProgramScheduleIntervalMinutes`, `Setup.HolidayFederalState`) werden wiederverwendet. Falls ein Entprell-/Debounce-Intervall für die Medien-Trigger nötig wird: über bestehende Defaults oder `appsettings`.

## Offene Fragen

Keine — die Vorgehensentscheidungen im Projektplan sind verbindlich; Interpretationsdetails (genaue Vergleichssemantik Soll-vs-Ist, Cursor-Korrekturmechanik, Trigger-Mechanismuswahl Signal vs. Direktaufruf, UI-Darstellung des Status) werden im Umsetzungsplan als Designentscheidungen festgelegt.
