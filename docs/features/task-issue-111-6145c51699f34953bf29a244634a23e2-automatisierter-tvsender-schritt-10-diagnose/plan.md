# Umsetzungsplan: Planungsdiagnose + manueller Generierungsanstoß (N5)

## Übersicht

Die Admin-Seite `TVSenderAdmin.razor` bekommt pro Programmregel eine Diagnose-Spalte
(„Planbar"), die on-demand aus der bestehenden Expansionslogik von `ProgramScheduleService`
berechnet wird: Anzahl planbarer Inhalte bzw. Grund der Leere (Material verwaist, keine
Laufzeit — differenziert in „Probe ausstehend" / „Probe dauerhaft gescheitert" / „keine
Mediendatei" — bzw. nicht für alle freigegeben). Zusätzlich ein „Jetzt generieren"-Button auf
der Sendeplan-Karte, der `IProgramScheduleSignal.NotifyScheduleChanged()` auslöst. Keine neue
Persistenz, keine Migration.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Diagnose-Datenquelle | On-demand pro Regel über `IProgramScheduleService.GetRuleDiagnosesAsync` beim Seitenladen — Wiederverwendung von `ExpandMaterialAsync` mit frischem `SummaryCounters` je Regel | Kein neuer State, kein Persistieren des letzten `ProgramScheduleRunSummary`; identische Ergebnisse zur echten Planung, weil dieselbe Expansionslogik läuft (kein zweiter Wahrheitsweg, der driften könnte). Kosten: wenige Regeln × die ohnehin vom Worker ausgeführten Abfragen — für eine Admin-Seite akzeptabel. |
| Detailzähler | `SummaryCounters` intern erweitern (`Candidates`, `MaterialMissing`, `NoMediaItem`, `ProbePending`, `ProbeExhausted`); öffentliche Run-/Reconcile-Summaries unverändert | Minimale Änderung am bestehenden Zählpfad; die Expand-/Try-Methoden ändern ihre Semantik nicht, sie zählen nur genauer. |
| „Verwaist"-Erkennung | Neuer Counter `MaterialMissing`, gesetzt in den Expand-Methoden an den Stellen, wo die Referenz nicht auflösbar ist (Entity fehlt; bei Genre: keine gespeicherten Genres / keine kanonischen Namen / keine wirksamen Genre-Ids). `OrphanedRules` (items==0) bleibt für die Run-Summary unverändert. | „items==0" allein kann verwaist nicht von „alles übersprungen" trennen; die Early-Return-Stellen wissen es dagegen genau. |
| Probezustand | `MovieMediaItemsAsync`/`EpisodeMediaItemsAsync` liefern alle Links inkl. `Duration`/`DurationProbedAt`/`DurationProbeAttempts` (statt nur `Duration != null`); die Try-Methoden prüfen `Duration` selbst und klassifizieren | Sonst sind „kein MediaItem" und „keine Laufzeit" nicht trennbar (bisher beides `SkippedNoDuration`). Semantik von `SkippedNoDuration` bleibt (alle drei Fälle zählen weiter dort mit). |
| Erschöpfungsschwelle | `IOptions<MediaProbeOptions>` als optionaler Konstruktor-Parameter von `ProgramScheduleService`; Default `new MediaProbeOptions().EffectiveMaxAttempts` | `MediaProbeOptions` ist bereits via `services.Configure` registriert; optionaler Parameter hält `ProgramScheduleTestDb.CreateService()` und andere manuelle Konstruktionen kompilierfähig. |
| Serverseitige Absicherung | `GetRuleDiagnosesAsync(string userId, long channelId, …)` mit privatem `RequireAdminAsync` nach `ProgramRuleService`-Muster; Button-Handler nur auf der Admin-Seite gerendert (Seiten-Gate `isAdmin`) | Konsistent mit dem Projekt-Grundsatz „Berechtigungen serverseitig erzwungen". Der Signal-Aufruf selbst ist folgenlos schädlich nicht möglich (er weckt nur den Worker); er bleibt hinter dem Seiten-Gate, da nur Admin-Blazor-Circuits die Komponente rendern. |
| Aktualisierung nach „Jetzt generieren" | Statusmeldung + verzögertes einmaliges Neu-Laden von Sendeplan und Diagnosen (kurze Wartezeit, damit der Worker-Run durchlaufen kann) | Der Worker-Lauf ist asynchron; ein Sofort-Reload würde den alten Stand zeigen. Ein kurzer Delay + Reload ist für die Admin-Ansicht ausreichend und einfach. |

## Programmabläufe

### Diagnose beim Seitenladen

1. `TVSenderAdmin.razor` `OnInitializedAsync` → nach `ReloadRulesAsync` zusätzlich
   `ReloadDiagnosesAsync`.
2. `ReloadDiagnosesAsync` ruft `ProgramScheduleService.GetRuleDiagnosesAsync(userId, channel.Id)`
   und speichert das Ergebnis als `Dictionary<long, ProgramRuleDiagnosis>` (`ruleDiagnoses`).
3. `GetRuleDiagnosesAsync` (Service): `RequireAdminAsync(userId)` → Regeln des Senders laden
   (inkl. `Genres`, `MediaSourceFilters`, auch inaktive) → `LoadAccessContextAsync` einmal →
   pro Regel frisches `SummaryCounters` + `ExpandMaterialAsync` → `ProgramRuleDiagnosis`
   aus den Zählern aufbauen.
4. Die Tabelle rendert pro Zeile `DiagnosisLabel(rule.Id)` in einer neuen Spalte.
5. Nach Regel-Änderungen (`SaveRuleAsync`, `ToggleActiveAsync`, `DeleteRuleAsync`) lädt
   `ReloadRulesAsync` die Diagnosen mit neu — sie spiegeln dann den aktuellen Stand.

Beteiligte Klassen/Komponenten: `TVSenderAdmin.razor`, `IProgramScheduleService`,
`ProgramScheduleService`, `ApplicationDbContext`.

### „Jetzt generieren"

1. Admin klickt `#tvsender-generate-now` auf der Sendeplan-Karte.
2. Handler `GenerateNowAsync`: `ScheduleSignal.NotifyScheduleChanged()` (injiziertes
   `IProgramScheduleSignal`) → Statustext „Plan-Aktualisierung wurde angestoßen…" →
   `Task.Delay` (einige Sekunden) → `ReloadScheduleAsync` + `ReloadDiagnosesAsync` →
   aktualisierter Stand sichtbar; Hinweistext weist darauf hin, dass große Bibliotheken
   länger brauchen und ein erneutes Laden der Seite den Endstand zeigt.

Beteiligte Klassen/Komponenten: `TVSenderAdmin.razor`, `IProgramScheduleSignal`,
`ProgramScheduleWorker` (konsumiert das Signal, unverändert).

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ProgramRuleDiagnosis` | `readonly record struct` (in `IProgramScheduleService.cs`, neben den bestehenden Summaries) | Pro-Regel-Diagnose: `RuleId`, `MaterialMissing`, `CandidateCount`, `SchedulableCount`, `NoMediaItemCount`, `ProbePendingCount`, `ProbeExhaustedCount`, `NoAccessCount`. |

## Änderungen an bestehenden Klassen

### `IProgramScheduleService` (Interface)

- **Neue Methoden:** `GetRuleDiagnosesAsync(string userId, long channelId, CancellationToken cancellationToken = default)` → `Task<IReadOnlyDictionary<long, ProgramRuleDiagnosis>>` — admin-only; Diagnose je Regel des Senders.

### `ProgramScheduleService` (Service)

- **Neuer Konstruktor-Parameter:** `IOptions<MediaProbeOptions>? probeOptions = null` — für
  `EffectiveMaxAttempts` (Probe-Erschöpfungsschwelle); `null` → Default-Optionen.
- **Neue Methoden:** `GetRuleDiagnosesAsync` — Ablauf wie oben; `RequireAdminAsync` (privat,
  nach `ProgramRuleService`-Muster).
- **Geänderte Methoden:**
  - `SummaryCounters`: neue Felder `Candidates`, `MaterialMissing`, `NoMediaItem`,
    `ProbePending`, `ProbeExhausted` (nur intern; `ToSummary()` unverändert).
  - `MovieMediaItemsAsync`/`EpisodeMediaItemsAsync`: Rückgabetyp wird ein Record
    (`MediaItemRef` o. ä. mit `MediaItemId`, `Duration`, `DurationProbedAt`,
    `DurationProbeAttempts`); Filter `Duration != null` entfällt, Auswahl des Items pro
    Inhalt bevorzugt Items mit Laufzeit (gleiche `OrderBy(MediaItemId)`-Wahl wie bisher:
    erstes Item — bei mehreren Links ist das deterministisch wie zuvor).
  - `TryMovieItem`/`ToItem`/`TryEpisodeItem`: `Candidates++`; fehlender Link → `NoMediaItem++`
    (und weiterhin `SkippedNoDuration++`); `Duration == null` → `ProbePending++` oder
    `ProbeExhausted++` je nach `DurationProbedAt`/`DurationProbeAttempts` vs.
    `maxAttempts` (und `SkippedNoDuration++`); danach unverändert `access.Allows` →
    `SkippedNoAccess++`.
  - `ExpandShowAsync`/`ExpandMovieAsync`/`ExpandMovieCollectionAsync`/`ExpandPlaylistAsync`:
    bei nicht gefundener Referenz `MaterialMissing++` vor `return []`.
  - `ExpandGenreAsync`: `MaterialMissing++` bei den drei Early-Returns ohne wirksames Material
    (keine gespeicherten Genres, keine kanonischen Namen, keine wirksamen Genre-Ids).
  - `ExpandMaterialAsync`: unverändert (Log + `OrphanedRules` bei `items.Count == 0` bleibt).

### `TVSenderAdmin.razor` (Komponente)

- **Neu injiziert:** `IProgramScheduleSignal ScheduleSignal`.
- **Neue Felder:** `Dictionary<long, ProgramRuleDiagnosis> ruleDiagnoses`,
  `string? scheduleStatus`, `bool generatePending`.
- **Neue Methoden:** `ReloadDiagnosesAsync`, `DiagnosisLabel(ProgramRule)` (Textbaustein),
  `GenerateNowAsync`.
- **Markup:** neue `<th>Planbar</th>`-Spalte in der Regel-Tabelle mit
  `.tvsender-rule-diagnosis`-Zelle; „Jetzt generieren"-Button `#tvsender-generate-now` im
  Header der Sendeplan-Karte neben dem Einträge-Chip; Statuszeile
  `#tvsender-schedule-status` unter der Karte.
- `ReloadRulesAsync` ruft nach dem Regel-Reload `ReloadDiagnosesAsync` auf (beide in
  `OnInitializedAsync` bereits verkettet).

## Datenbankmigrationen

Keine — die Diagnose wird zur Laufzeit berechnet; kein neues Feld, keine neue Tabelle, keine
`OptionalRestoreColumns`-Erweiterung.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `userId` in `GetRuleDiagnosesAsync` | Benutzer muss existieren und Admin sein | `UnauthorizedAccessException` / `ForbiddenAccessException` |

## Konfigurationsänderungen

Keine — `MediaProbeOptions` wird nur gelesen.

## Seiteneffekte und Risiken

- **Expand-Pfade:** `MovieMediaItemsAsync`/`EpisodeMediaItemsAsync` laden jetzt alle Links
  (inkl. Probezustand) statt nur erfolgreicher — geringfügig mehr selektierte Spalten/Zeilen,
  unveränderte Semantik. `SkippedNoDuration` zählt weiterhin alle drei Fälle (kein Item,
  Probe ausstehend, Probe erschöpft), sodass die öffentlichen Summaries und alle
  Bestandstests unverändert bleiben.
- **Worker-Lauf:** unverändert; die erweiterten Zähler sind intern.
- **Admin-Seite Ladezeit:** eine Expand-Abfragemenge pro Regel zusätzlich — bei wenigen
  Regeln unkritisch; Risiko bei sehr vielen Regeln akzeptabel (Admin-Seite).
- **`ProgramScheduleTestDb.CreateService()`:** muss den neuen optionalen Parameter nicht
  zwingend übergeben (Default), kann aber für Schwelle-Tests `IOptions<MediaProbeOptions>`
  mit kleinem `MaxAttempts` injizieren.

## Umsetzungsreihenfolge

1. **`ProgramRuleDiagnosis` + `GetRuleDiagnosesAsync` in `IProgramScheduleService`**
   - Voraussetzungen: keine.
   - Record + Schnittstellenmethode mit XML-Doku.
2. **`ProgramScheduleService` erweitern**
   - Voraussetzungen: Schritt 1.
   - `IOptions<MediaProbeOptions>`-Parameter, `SummaryCounters`-Felder,
     `MediaItemRef`-Record + geänderte Item-Lookups, `MaterialMissing`-Markierungen,
     `GetRuleDiagnosesAsync` + `RequireAdminAsync`.
3. **Unit-/Integrationstests `ProgramScheduleServiceTests_Diagnosis.cs`**
   - Voraussetzungen: Schritt 2; `ProgramScheduleTestDb` (unverändert nutzbar, optionaler
     Parameter).
   - Neue Testklasse mit den in „Tests" gelisteten Fällen.
4. **UI in `TVSenderAdmin.razor`**
   - Voraussetzungen: Schritt 2.
   - Spalte, Label-Helper, Button + Handler, Statusanzeige, Diagnose-Reload.
5. **E2E-Erweiterung `TVSenderAdminE2ETests`**
   - Voraussetzungen: Schritt 4.
   - Bestehenden konsolidierten E2E-Test um Diagnose-Spalte + „Jetzt generieren" erweitern.
6. **Dokumentation `docs/help/tv-sender.md` + Release Notes**
   - Voraussetzungen: Schritt 4.
   - Abschnitt Planungsdiagnose + manueller Anstoß.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Diagnosis_SchedulableMovie_CountsSchedulable` | `ProgramScheduleServiceTests_Diagnosis` | Regel mit Film + Laufzeit → `SchedulableCount == 1`, keine Fehlgründe, `CandidateCount == 1`. |
| `Diagnosis_MovieWithoutMediaItem_CountsNoMediaItem` | dto. | Film ohne `MovieMediaItem`-Link → `NoMediaItemCount == 1`, `SchedulableCount == 0`. |
| `Diagnosis_UnprobedItem_CountsProbePending` | dto. | `Duration == null`, `DurationProbedAt == null` → `ProbePendingCount == 1`. |
| `Diagnosis_FailedProbeBelowMax_CountsProbePending` | dto. | `DurationProbedAt` gesetzt, `DurationProbeAttempts < max` → pending (Retry steht aus). |
| `Diagnosis_ExhaustedProbe_CountsProbeExhausted` | dto. | `DurationProbeAttempts >= EffectiveMaxAttempts` → `ProbeExhaustedCount == 1`. |
| `Diagnosis_UnsharedContent_CountsNoAccess` | dto. | Nicht-Admin-User ohne Quellzugriff → `NoAccessCount == 1`; zusätzlich freigegebener Kandidat → `SchedulableCount` stimmt. |
| `Diagnosis_DeletedMaterial_MarksMissing` | dto. | Regel verweist auf nicht existierende Serie → `MaterialMissing == true`. |
| `Diagnosis_GenreResolvingToNothing_MarksMissing` | dto. | Genre-Regel, deren Genres nicht existieren/auf nichts auflösen → `MaterialMissing == true`. |
| `Diagnosis_InactiveRule_StillDiagnosed` | dto. | Inaktive Regel erscheint ebenfalls im Ergebnis (UI zeigt Diagnose auch inaktiv). |
| `Diagnosis_NonAdmin_Throws` | dto. | `ForbiddenAccessException` bei Nicht-Admin; `UnauthorizedAccessException` bei unbekannter userId. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ProgramScheduleTestDb.CreateService()` | Neuer optionaler Konstruktor-Parameter — kompilierfähig ohne Änderung; für einen Erschöpfungs-Test ggf. `IOptions<MediaProbeOptions>` mit kleinem `MaxAttempts` übergeben (Hilfsmethoden-Überladung). |
| Sonstige | Keine — öffentliche Summaries und Expand-Semantik unverändert. |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Regel-Tabelle zeigt pro Zeile die Diagnose-Zelle (bei geseedeter Regel ohne planbares Material: Begründung sichtbar, z. B. „keine Mediendatei"/„Laufzeit"), und „Jetzt generieren" ist klickbar und quittiert mit Statustext | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` (Erweiterung) | Diagnose sichtbar pro Regel; manueller Anstoß auslösbar | Benutzerfluss in der UI — Unit-Tests decken die Render- und Klickstrecke nicht ab. |
| Pflicht | Nicht-Admin sieht weiterhin „Nicht autorisiert" (bereits vorhanden, unverändert) | `TVSenderAdminE2ETests.NonAdmin_SeesNotAuthorized` | Berechtigungsgate | Bestehender Nachweis, bleibt gültig. |

Bestehende E2E-Tests werden **erweitert, nicht dupliziert** (Konsolidierungsregel: minimale
Testmethodenzahl, gleiche Instanz): in `Admin_CreateEditDeactivateDeleteRule` wird nach dem
Regel-Reload die Diagnose-Zelle geprüft und vor dem Löschen der „Jetzt generieren"-Button
geklickt + Statustext erwartet.

## Offene Punkte

Keine.
