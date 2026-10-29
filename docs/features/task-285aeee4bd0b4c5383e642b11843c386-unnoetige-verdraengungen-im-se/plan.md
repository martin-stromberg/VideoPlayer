# Umsetzungsplan: Unnötige Verdrängungen im Sendeplan vermeiden (Issue #310)

## Übersicht

`ReconcileAsync` erklärt fachlich zulässige Sendeblöcke in Einzelfällen fälschlich für
unreproduzierbar und verdrängt sie — die Neu-Materialisierung plant dann denselben Inhalt
wieder ein („durch sich selbst ersetzt", angehäufte `Displaced`-Duplikate). Umgesetzt wird
die fail-first-Strategie aus der Anforderung: zuerst ein Reproduktionstest gegen echtes
SQLite, der eine tatsächliche Selbst-Verdrängung nachweist, dann die Behebung des
diagnostizierten Restfehlers in der Reproduzierbarkeits-Simulation. Ergänzend entschieden:
(1) bereits angehäufte `Displaced`-Duplikate werden **automatisch** bereinigt — als eigener
Wartungsschritt `CleanupSelfDisplacedSlotsAsync` in der `ProgramScheduleWorker`-Kette vor
`ReconcileAsync`, mit strengem Zwillings-Kriterium, das Evidenz-Verlust konstruktionsbedingt
ausschließt; (2) minimaler Diagnose-Ausbau — der Ablehnungsgrund der
Reproduzierbarkeitsprüfung wird intern erfasst und pro invalidem Block geloggt (kein UI).
Betroffen sind `VideoWebPlayer/Services/ProgramScheduleService.cs` (Reconcile-Interna plus
neue Wartungsmethode), `IProgramScheduleService`, `ProgramScheduleWorker` sowie die
Reconcile-/Worker-Testklassen. Kein Datenmodell, keine Migration, kein UI-Eingriff, keine
neuen NuGet-Pakete.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|------------------|------------|
| Prüfkriterium (`ReconcileWindow.Validate` / `AiringReproducible`) | Richtung (a): exakte Reproduzierbarkeit beibehalten, Restfehler im Hypothesenraum beheben — **nicht** auf reine Zulässigkeit/Mitgliedschaft lockern (Richtung b) | Code-Verifikation: Die Prüfung liest den persistierten Cursor (`state.NextIndex`, `ProgramRuleCursor`) überhaupt nicht — Kopfpositionen werden in `FlatAiringTailReproducible` (Z. 694–741) bzw. `InterleavedAiringTailReproducible` (Z. 877–944) frei per Rückwärts-Hypothesen rekonstruiert. „Zulässiger Inhalt an freier Ring-/Cursor-Position" ist also bereits heute der Prüfmaßstab; die offene fachliche Frage aus der Anforderung ist damit beantwortet: Eine abweichende Ring-Position allein macht einen Block nicht invalid. Die beobachteten Fehlklassifikationen sind Lücken im Hypothesenraum bzw. Evidenzmodell, keine Kriteriums-Probleme. Eine reine Mitgliedschaftsprüfung würde zudem Blöcke behalten, deren Neu-Materialisierung anders ausfiele — Oszillationsrisiko und Verlust der No-Op-Garantie. |
| Verschiebe-Evidenz (`_invaderStarts`) | Evidenzmodell an die `ShiftPastInvaders`-Semantik angleichen: belegte Intervalle (Scheduled ∪ DisplacedEvidence) statt `End → min Start`-Map; Beweis `airingStart == ShiftPastInvaders(occurrenceStart, occupied)` | Primärhypothese für den Restfehler (vor Reproduktion zu verifizieren): `ShiftPastInvaders` (Z. 1498–1514) läuft bei der Materialisierung als Fixpunkt über **alle** belegten Intervalle und behandelt damit verkettete Belegungen (Invader A deckt den Occurrence-Start, Invader B schließt lückenlos an, realer Start = `B.End`). Die Prüfung verlangt dagegen einen **einzigen** Beleger, der den nominalen Start überdeckt **und** exakt am realen Start endet (`ProgramScheduleService.cs` Z. 746–748, Z. 951–954). Eine über ≥2 verkettete Belegungen verschobene Ausstrahlung ist heute unbeweisbar → invalid → die Neu-Materialisierung landet (Invader sind weiter `Scheduled`) an identischer Position → Selbst-Verdrängungs-Schleife, passend zum gemeldeten Duplikat-Bild. |
| Akzeptanzkriterium „durch sich selbst ersetzt" | Inhaltsidentität auf Inhaltsebene: gleiches `MovieId`/`TVShowEpisodeId` (bei Rohmaterial `MediaItemId`) im selben Zeitintervall; übergeordnetes Kriterium: Reconcile ohne Eingabeänderung = No-Op (`SlotsDisplaced == 0`, `SlotsCreated == 0`) | Konsistent mit `FindItemIndex` (Z. 997–1007: `MediaItemId`, dann `MovieId`/`TVShowEpisodeId`-Fallback) und `SlotMatchesItem`/`MediaItemAlternates` aus Issue #290: ein Repräsentantenwechsel der Datei ist keine inhaltliche Änderung. Das No-Op-Kriterium ist das in der Anforderung genannte Endergebnis und bereits über `AssertReconcileNoOpAsync` prüfbar. |
| Geltungsbereich der Prüfung | Alle Slot-Arten: Titel-Ausstrahlungen, Clip-Fill-Anteile (`IsClipFill`) und Filler-Slots | Die Anforderung nennt den „Sendeblock" allgemein. Clip-Anteile laufen bereits über den Mitgliedschafts-/Lückenlosigkeits-Vorfilter in `AiringReproducible` (Z. 609–645); Filler-Slots werden ohnehin einzeln per Mitgliedschaft geprüft (Z. 286–298) und bleiben unverändert. |
| Struktur des Prüffixes | Keine neue Klasse, kein neuer Service — Erweiterung der bestehenden privaten Reconcile-Interna | Folgt dem bestehenden Muster: alle Reconcile-Interna sind `private`/`private sealed` in `ProgramScheduleService.cs`; `Interval` und `ShiftPastInvaders` existieren bereits und werden wiederverwendet. |
| Andockpunkt der automatischen Duplikat-Bereinigung | Eigene öffentliche Wartungsmethode `CleanupSelfDisplacedSlotsAsync` an `IProgramScheduleService` (`Task<int>`, kein Admin-Scope nötig), aufgerufen im `ProgramScheduleWorker` im `TvSenderEnabled`-Block **nach** `PurgeExpiredRulesAsync` und **vor** `ReconcileAsync` (`ProgramScheduleWorker.cs` Z. 74–76) | Anwender-Entscheidung: automatische Bereinigung statt nur Admin-Aktion. Der Worker-Schritt folgt dem `PurgeExpiredRulesAsync`-Präzedenz (nicht-administrative Wartungsmethode, `Task<int>` + `LogInformation`). Alternativ erwogen und verworfen: Löschung innerhalb `ReconcileAsync` nach `SaveChangesAsync` — sie müsste frisch verdrängte Zeilen von Alt-Evidenz unterscheiden und koppelt einen fremden Zweck an die komplexeste Methode. Entscheidend für die Reihenfolge: Läuft die Bereinigung **vor** dem Reconcile, kann sie eine im selben Takt frisch erzeugte Selbst-Verdrängung nie verdecken — ein Restfehler bleibt als `Displaced`-Zeile und im `SlotsDisplaced`-Zähler sichtbar. |
| Löschkriterium der Bereinigung („nachgewiesenes Selbst-Verdrängungs-Duplikat") | Eine `Displaced`-Zeile `d` wird genau dann gelöscht, wenn ein `Scheduled`-Zwilling `s` desselben Kanals existiert mit: `s.Start == d.Start`, `s.End == d.End` (identisches Intervall), `s.ProgramRuleId == d.ProgramRuleId` (gleiche erzeugende Regel), `s.IsClipFill == d.IsClipFill` und gleichem Inhalt — `s.MediaItemId == d.MediaItemId` **oder** `d.MovieId != null && s.MovieId == d.MovieId` **oder** `d.TVShowEpisodeId != null && s.TVShowEpisodeId == d.TVShowEpisodeId` | Strenges Kriterium gegen die dokumentierte `_invaderStarts`-Evidenz-Problematik: Die Evidenz ist die Menge belegter Intervalle; das gelöschte Duplikat ist durch den `Scheduled`-Zwilling **vollständig abgedeckt** — die Belegungsmenge und damit die Beweislage des nächsten `ReconcileAsync` ist bit-identisch, Evidenz-Verlust ist konstruktionsbedingt ausgeschlossen (gilt sowohl für die heutige `End → min Start`-Map — Zwilling liefert denselben Schlüssel und denselben Start — als auch für die geplante Intervall-Evidenz). `ProgramRuleId`-Gleichheit schließt aus, dass ein echter Verdrängungsverlierer einer **fremden** Regel gelöscht wird; die Inhaltsbedingung schließt aus, dass ein Verlierer mit anderem Inhalt am selben Sendeplatz (normale Prioritäts-Eviction) entfernt wird. Repräsentantenwechsel-Duplikate (andere Datei, gleicher Inhalt) fallen über die `MovieId`-/`TVShowEpisodeId`-Disjunktion mit. Duplikat-Ketten (n `Displaced` + 1 `Scheduled`) werden vollständig getroffen, da jede Kopie denselben Zwilling hat. |
| Umsetzung der Bereinigung | Ein `ExecuteDeleteAsync` über die `Displaced`-Zeilen des Kanals mit `Any`-Subquery auf den `Scheduled`-Zwilling (SQL `EXISTS`) | Muster von `DeleteDisplacedSlotsAsync` (Z. 484–492) und `ResetScheduleAsync`: Bulk-Löschung statt Laden + `RemoveRange`. Im Worker-Aufrufpunkt sind noch keine `ProgramSlot`-Entities im DbContext getrackt (Purge lädt nur Regeln) — die bei `LoadWindowAsync` dokumentierte Tracker-Staleness nach Bulk-Operationen kann nicht entstehen. |
| Diagnose-Ausbau Ablehnungsgrund | Internes `internal enum AiringInvalidReason` (stufen-geordnet) + mutable Diagnose-Senke im `ReproContext`, die von den Tail-Checks/Vorfilter mit der „weitesten erreichten Stufe" beschrieben wird; `ReconcileWindow.Validate` ergänzt eigene Gründe (Regel fehlt/inaktiv/kein Occurrence-Anker) und loggt je invalidem Block eine `LogInformation`-Zeile (Regel-Id, Kopf-`Start`/`End`, Slot-Anzahl, Grund) | Anwender-Entscheidung: minimaler Ausbau — intern erfassen/loggen, kein UI. `internal` statt `private`, damit der Enum aus dem Testprojekt sichtbar ist (`InternalsVisibleTo`-Muster wie `SelectStrandCursor`/`StrandOrderKey`); geloggt wird über den vorhandenen `_logger` (assertbar mit `ListLogger<ProgramScheduleService>`). Die „weiteste Stufe"-Semantik ist nötig, weil die Tail-Checks viele Hypothesen durchprobieren: aussagekräftig ist nicht der erste Fehlschlag, sondern die Stufe, bis zu der die beste Hypothese kam. Die Diagnose erleichtert zugleich Schritt „Diagnose des Ablehnungspfads" dieses Plans. `ProgramScheduleReconcileSummary` bleibt unverändert — kein neuer öffentlicher Zähler. |

## Programmabläufe

### Reproduktion einer Selbst-Verdrängung (fail-first)

Schritt-für-Schritt-Ablauf des geplanten Reproduktionstests (Primärszenario „verkettete
Invader-Evidenz"); Muster nach `Reconcile_ShiftedAiring_InvaderDisplaced_StableAcrossRuns`
(`ProgramScheduleServiceTests_Boundary.cs:139`) und `Reconcile_LongAiring_Shifted_StaysScheduled`
(`ProgramScheduleServiceTests_Reconcile.cs:606`):

1. `ProgramScheduleTestDb`-Fixture; Regeln so seeden, dass eine Ausstrahlung hinter eine
   **Kette** von ≥2 lückenlos aufeinanderfolgenden fremden Belegungen verschoben wird:
   Invader-Ausstrahlung 1 überdeckt den Occurrence-Start der Zielregel, Invader-Ausstrahlung 2
   beginnt exakt am Ende von 1; der reale Start der Zielausstrahlung ist das Ende von 2.
   Aufbau über `CreateRuleAsync` + `EnsureCoverageAsync` (realer Materialisierungspfad), wie
   in den Referenztests.
2. Nachweis des verschobenen Starts (`shiftedBefore[0].Start` hinter dem nominalen
   Occurrence-Start — kein einzelner Beleger überdeckt Start *und* endet am realen Start).
3. `ReconcileAsync` ohne Eingabeänderung ausführen.
4. Erwartung (nach Fix): `AssertNoDisplacedAsync` / `AssertReconcileNoOpAsync` grün.
   Stand heute (fail-first): `AiringReproducible` lehnt den Block ab (Evidenzbedingung nicht
   erfüllt) → Suffix-Verdrängung ab `firstInvalid` → `RewindCursor` →
   `MaterializeOccurrenceEvicting` materialisiert identischen Inhalt an identischer Position
   → `Displaced`-/`Scheduled`-Duplikatpaar.
5. Optional zweiter `ReconcileAsync`-Lauf: weitere Duplikat-Häufung beweist die Schleife
   (die neuen Slots scheitern am selben Evidenzmodell).

Beteiligte Klassen/Komponenten: `ProgramScheduleService` (`ReconcileAsync`,
`ReconcileWindow.Reload`/`Validate`, `AiringReproducible`,
`FlatAiringTailReproducible`/`InterleavedAiringTailReproducible`,
`MaterializeOccurrenceEvicting`, `RewindCursor`, `ShiftPastInvaders`),
`ProgramScheduleTestDb`.

Falls das Primärszenario die Selbst-Verdrängung nicht auslöst (die Lücke liegt woanders),
stehen als weitere Reproduktionskandidaten bereit (Auswahl nach Diagnose, nicht alle
umsetzen): Evidenzverlust durch `DeleteDisplacedSlotsAsync` nach Verschiebung; doppelter
Inhalt im expandierten Ring (`FindItemIndex` liefert den ersten Treffer, der Cursor kann
auf dem zweiten stehen — `positions[k] == idx` unerfüllbar); Inhalt in mehreren
Interleaved-Strängen (Fehlzuordnung über `strandOf`, Z. 821–833); Hypothesenraum-Deckel
`k < wanted` bei mehrfach partiell materialisierten Occurrences.

### Diagnose: Ablehnungsgrund intern erfassen und loggen

1. Das neue `internal enum AiringInvalidReason` ist stufen-geordnet: Validate-Ebene
   (`RuleMissing`, `RuleInactive`, `NoOccurrenceAnchor`), Clip-Vorfilter
   (`ClipChainBroken`, `TitlelessAiring`), Tail-Stufen (`HeadItemNotInExpansion`,
   `StrandSequenceBroken`, `AnchorWithoutInvaderEvidence`, `HeadPickInconsistent`,
   `SlotSequenceMismatch`, `RemainderNotReproducible`) sowie `None`.
2. `ReproContext` erhält eine mutable Senke (Feld + Setzmethode mit Maximum-Semantik, da
   tiefere Stufen höhere Werte tragen). Clip-Vorfilter, `FlatAiringTailReproducible` und
   `InterleavedAiringTailReproducible` schreiben an jedem Abbruch-/`continue`-Punkt die
   erreichte Stufe; die Rekursion lässt den tiefsten Befund stehen (Maximum gewinnt).
3. Der Einstiegs-Overload `AiringReproducible` (Z. 607–648) gibt den Grund per
   `out`-Parameter zurück (`None` bei Erfolg).
4. `ReconcileWindow.Validate` bestimmt je invalidem Block den Grund (eigene Vorprüfungen
   oder Ergebnis von `AiringReproducible`) und schreibt eine
   `_logger.LogInformation`-Zeile: Regel-Id, Kopf-`Start`/`End`, Slot-Anzahl, Grund.
5. `ProgramScheduleTestDb.CreateService` erhält einen optionalen
   `ILogger<ProgramScheduleService>`-Parameter, damit Tests den Grund über
   `ListLogger<T>` assertieren können.

Beteiligte Klassen/Komponenten: `AiringInvalidReason` (neu), `ReproContext`,
`AiringReproducible`, `FlatAiringTailReproducible`, `InterleavedAiringTailReproducible`,
`ReconcileWindow.Validate`, `ProgramScheduleTestDb`, `ListLogger<T>`.

### Korrigierte Gültigkeitsprüfung (Zielablauf bei bestätigter Primärhypothese)

1. `ReconcileWindow.Rebuild` sammelt zusätzlich zu (bzw. statt) `_invaderStarts` die
   belegten Intervalle aller geladenen Zeilen (Scheduled ∪ DisplacedEvidence) als
   `Interval`-Menge.
2. `ReproContext` trägt die Intervall-Evidenz statt/ergänzend zur `InvaderStarts`-Map.
3. `FlatAiringTailReproducible`: die Bedingung `occurrenceStart < airingStart` wird durch
   `airingStart == ShiftPastInvaders(occurrenceStart, occupiedIntervals)` bewiesen — gleiche
   Fixpunkt-Semantik wie die Materialisierung, daher sind Einzel- und Kettenverschiebungen
   beweisbar; ohne belegte Kette bleibt der frühere Anker unzulässig (Schutz gegen
   „gerettete" veraltete Ausstrahlungen bleibt bestehen).
4. `InterleavedAiringTailReproducible`: analoge Ersetzung an der Stelle, die heute
   `invaderStarts.TryGetValue(simStart, ...)` prüft.
5. Rest des Reconcile-Ablaufs (Suffix-Verdrängung ab `firstInvalid`, `RewindCursor`,
   `MaterializeOccurrenceEvicting`, `InsertClipFill`/`FillGaps`, `PersistCursors`) bleibt
   unverändert.

Beteiligte Klassen/Komponenten: `ReconcileWindow`, `ReproContext`,
`FlatAiringTailReproducible`, `InterleavedAiringTailReproducible`, `ShiftPastInvaders`,
`Interval`, `ProgramSlot`.

### Automatische Bereinigung nachgewiesener Selbst-Verdrängungs-Duplikate

1. `ProgramScheduleWorker.ExecuteAsync` ruft im `TvSenderEnabled`-Block die neue Kette auf:
   `PurgeExpiredRulesAsync` → **`CleanupSelfDisplacedSlotsAsync`** → `ReconcileAsync` →
   `EnsureCoverageAsync` (Reihenfolge-Entscheidung s. Designentscheidungen: Bereinigung
   vor dem Reconcile, damit frische Selbst-Verdrängungen desselben Takts nicht verdeckt
   werden).
2. `CleanupSelfDisplacedSlotsAsync` lädt den Kanal (`GetOrCreateChannelAsync`) und löscht
   per einem `ExecuteDeleteAsync` alle `Displaced`-Zeilen, zu denen ein
   `Scheduled`-Zwilling nach dem strengen Zwillings-Kriterium existiert (identischer
   Kanal, `Start`, `End`, `ProgramRuleId`, `IsClipFill`, gleicher Inhalt über
   `MediaItemId`/`MovieId`/`TVShowEpisodeId`-Disjunktion) — als `Any`-Subquery, auf SQL
   `EXISTS` übersetzbar.
3. Rückgabe: Anzahl gelöschter Zeilen; `_logger.LogInformation` wie bei
   `DeleteDisplacedSlotsAsync`/`PurgeExpiredRulesAsync`.
4. Evidenz-Erhalt ohne Kompensation: gelöschte Zeilen sind durch das Intervall des
   `Scheduled`-Zwillings vollständig abgedeckt — die Belegungsmenge des folgenden
   `ReconcileAsync` (`_invaderStarts` heute, Intervall-Evidenz nach dem Fix) ist
   unverändert; es kann weder ein Anker verloren gehen noch eine echte Neuplanung
   ausgelöst werden. Gegenprobe-Tests belegen beides (s. „Tests").
5. Die Admin-Aktion `DeleteDisplacedSlotsAsync` bleibt unverändert als Vollsweep für
   verbleibende (echte) Verdrängungs-Historie.

Beteiligte Klassen/Komponenten: `IProgramScheduleService`,
`ProgramScheduleService.CleanupSelfDisplacedSlotsAsync`, `ProgramScheduleWorker`,
`ProgramSlot`, `ProgramSlotStatus`.

### Fallback bei anderem Diagnosebefund

Zeigt die Reproduktion eine andere Lücke als die Primärhypothese (s. Kandidatenliste
oben), wird der Fix analog auf den jeweiligen Check-Teil angewendet: Der Hypothesenraum
wird so erweitert, dass jeder Block, den `SelectItems` aus einem legitimierten Zustand
(Cursor, Expansion, Evidenz) tatsächlich erzeugen kann, validiert — ohne das Kriterium zu
lockern. Erweist sich die Simulation strukturell als nicht schließbar (Richtung b doch
nötig), ist das als Abweichung vom Plan im Handover auszuweisen.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `AiringInvalidReason` | `internal enum` (Namespace `VideoWebPlayer.Services`, eigene kleine Datei nach Muster von `ProgramScheduleAgeWindow.cs`) | Stufen-geordneter Ablehnungsgrund der Block-Gültigkeitsprüfung: `None`, `RuleMissing`, `RuleInactive`, `NoOccurrenceAnchor`, `ClipChainBroken`, `TitlelessAiring`, `HeadItemNotInExpansion`, `StrandSequenceBroken`, `AnchorWithoutInvaderEvidence`, `HeadPickInconsistent`, `SlotSequenceMismatch`, `RemainderNotReproducible` — intern sicht- und assertbar über `InternalsVisibleTo`, nur für Logging/Diagnose |

## Änderungen an bestehenden Klassen

### `IProgramScheduleService` (Interface, `VideoWebPlayer/Services/IProgramScheduleService.cs`)

- **Neue Methode:** `Task<int> CleanupSelfDisplacedSlotsAsync(CancellationToken cancellationToken = default)` —
  löscht `Displaced`-Zeilen, die einen beweisbaren `Scheduled`-Zwilling haben
  (Selbst-Verdrängungs-Duplikate); nicht-administrativ, wird vom Worker vor
  `ReconcileAsync` aufgerufen; Rückgabe = Anzahl gelöschter Zeilen. XML-Doku im Stil von
  `PurgeExpiredRulesAsync`. Einzige Implementierung ist `ProgramScheduleService` — keine
  Fakes/Stubs im Repo betroffen.

### `ProgramScheduleService` (Service, `VideoWebPlayer/Services/ProgramScheduleService.cs`)

Öffentliche Signaturen ändern sich nur um die neue Methode; alle Prüf-/Evidenz-Änderungen
betreffen private Reconcile-Interna.

- **Neue Methode `CleanupSelfDisplacedSlotsAsync`** (`public`): Kanal laden, ein
  `ExecuteDeleteAsync` über `ProgramSlots` mit `Status == Displaced` und `Any`-Subquery
  auf den `Scheduled`-Zwilling (Kriterium s. Designentscheidungen), `LogInformation` mit
  Anzahl, Rückgabe `int`.
- **`ReconcileWindow`** (`private sealed class`, Z. 1069–1157)
  - Geändertes Feld: `_invaderStarts` (`Dictionary<DateTime, DateTime>`) — durch eine
    belegte-`Interval`-Menge ersetzt oder um sie ergänzt; Aufbau in `Rebuild` aus
    `Scheduled` ∪ `displacedEvidence` (Bedeutung der Displaced-Zeilen als reine
    Invader-Evidenz bleibt).
  - Geänderte Methode `Validate` (Z. 1128–1156): bestimmt je invalidem Block den
    `AiringInvalidReason` (eigene Vorprüfungen bzw. `out`-Ergebnis von
    `AiringReproducible`) und loggt eine `LogInformation`-Zeile mit Regel-Id,
    Kopf-`Start`/`End`, Slot-Anzahl und Grund.
- **`ReproContext`** (`private sealed record`, Z. 572–579)
  - Geänderte Komponente: `InvaderStarts` → Intervall-Evidenz (Typ an `ShiftPastInvaders`
    angleichen, z. B. `IReadOnlyList<Interval>`/`List<Interval>`).
  - Neue Komponente: mutable Diagnose-Senke für `AiringInvalidReason`
    (Maximum-Setzsemantik — Klasse statt reinem Record-Feld bzw. Feld in eingebetteter
    Hilfsinstanz, da Records positional sind).
- **`AiringReproducible`** (Einstiegs-Overload, `private static`, Z. 607–648)
  - Geänderte Signatur: `out AiringInvalidReason` (bzw. Rückgabetupel) — `None` bei
    Erfolg; Clip-Vorfilter-Abbrüche melden `ClipChainBroken`/`TitlelessAiring`.
- **`FlatAiringTailReproducible`** (`private static`, Z. 670–783)
  - Geänderte Bedingung: Shift-Beweis (Z. 746–748) von Einzel-Beleger-Map auf
    `ShiftPastInvaders`-Fixpunkt über die Intervall-Evidenz umgestellt.
  - Neu: Abbruchpunkte schreiben ihre Stufe in die Diagnose-Senke (`FindItemIndex < 0` →
    `HeadItemNotInExpansion`, Evidenz-`continue` → `AnchorWithoutInvaderEvidence`,
    `headPick`/`positions[k]` → `HeadPickInconsistent`, `consumed`/`SlotMatchesItem` →
    `SlotSequenceMismatch`, Rekursion → `RemainderNotReproducible`).
- **`InterleavedAiringTailReproducible`** (`private static`, Z. 795–993)
  - Geänderte Bedingung: analoge Umstellung des Shift-Beweises (Z. 951–954).
  - Neu: Abbruchpunkte analog (`s < 0`/Strang-Bruch Z. 831/841–856 →
    `HeadItemNotInExpansion`/`StrandSequenceBroken`, Evidenz Z. 951–954 →
    `AnchorWithoutInvaderEvidence`, `StrandStartOffset`-Inkonsistenz Z. 948 →
    `HeadPickInconsistent`, Match/Rekursion → `SlotSequenceMismatch`/
    `RemainderNotReproducible`).
- Je nach Reproduktionsbefund zusätzlich möglich (nur bei bestätigter anderer Lücke):
  Hypothesenraum-Erweiterung in den Tail-Checks (z. B. `k`-Deckel, `FindItemIndex`-
  Duplikatbehandlung, `strandOf`-Zuordnung). Scope bleibt auf die diagnostizierte Lücke
  beschränkt.

### `ProgramScheduleWorker` (BackgroundService, `VideoWebPlayer/Services/ProgramScheduleWorker.cs`)

- **Geänderte Aufrufkette** (Z. 74–76): nach `PurgeExpiredRulesAsync` und vor
  `ReconcileAsync` `await service.CleanupSelfDisplacedSlotsAsync(stoppingToken)`
  einfügen; vorhandenen Kommentar zur Ablauf-Bereinigung um den neuen Schritt erweitern
  (Bereinigung vor dem Reconcile, damit frische Verdrängungen desselben Laufs sichtbar
  bleiben).

### `ProgramScheduleTestDb` (Testhelfer, `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`)

- **Neue Hilfsassertion:** `AssertNoSelfDisplacedAsync` — schlägt fehl, wenn zu einem
  `Displaced`-Slot ein `Scheduled`-Slot mit gleichem Inhalt (`MovieId`/`TVShowEpisodeId`,
  sonst `MediaItemId`) im selben Zeitintervall existiert.
- **Geänderte Methode:** `CreateService` — optionaler Parameter
  `ILogger<ProgramScheduleService>?` (Default `NullLogger`), damit Log-Ausgaben der
  Diagnose per `ListLogger<T>` assertierbar sind.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine neuen Eingabe-Validierungen. Die fachliche Gültigkeitsprüfung
(`AiringReproducible`-Kette) und das Zwillings-Löschprädikat der Bereinigung sind selbst
Gegenstand der Änderung (siehe „Designentscheidungen" bzw. „Änderungen an bestehenden
Klassen"); ihr Korrektheitsnachweis läuft über die neuen Tests.

## Konfigurationsänderungen

Keine. Das Vermeiden unnötiger Verdrängungen gilt unbedingt; ebenso läuft die automatische
Duplikat-Bereinigung unbedingt in jedem Worker-Takt — ein umschaltbarer
strenger/toleranter Prüfmodus oder ein Bereinigungs-Schalter ist aus der Anforderung nicht
ableitbar und wird nicht eingeführt.

## Seiteneffekte und Risiken

- **Tolerantere Verschiebe-Evidenz:** Ein früherer Occurrence-Anker wird künftig auch über
  Belegungsketten beweisbar. Risiko, veraltete Blöcke zu „retten", ist gering und bleibt
  durch denselben Mechanismus begrenzt wie heute: Der reale Start muss exakt dem Fixpunkt
  der belegten Kette entsprechen, und die Slot-Folge muss die Re-Simulation weiterhin
  exakt bestehen (`SlotMatchesItem`). Negativtests sichern ab, dass echte Änderungen
  (z. B. geänderte `TimeOfDay` ohne Belegung) weiterhin verdrängen.
- **Automatische Löschung von Historie:** Bewiesene Selbst-Verdrängungs-Duplikate werden
  künftig ohne Admin-Eingriff gelöscht. Das ist gewollt; Risiko ist durch das strenge
  Zwillings-Kriterium begrenzt: Ohne `Scheduled`-Zwilling am identischen Intervall mit
  gleicher Regel und gleichem Inhalt wird nichts gelöscht — echte Verdrängungs-Historie
  (Verlierer mit anderem Inhalt oder fremder Regel am selben Platz, verdrängte Blöcke ohne
  Ersatz) bleibt vollständig erhalten, und die Admin-Aktion `DeleteDisplacedSlotsAsync`
  bleibt für den Vollsweep bestehen.
- **Keine Neuplanung durch Bereinigung:** Das Intervall-Kriterium garantiert, dass die
  Belegungsmenge (Evidenz) des folgenden Reconcile unverändert bleibt — unter dem heutigen
  `_invaderStarts`-Modell wie unter der geplanten Intervall-Evidenz. Gegenprobe-Test
  `CleanupSelfDisplaced_PreservesInvaderEvidence_NoReplan` belegt das.
- **`ExecuteDeleteAsync` vs. Change-Tracker:** Die Bereinigung läuft in der Worker-Kette
  vor jeder Slot-Ladung des Takts, sodass keine getrackten `ProgramSlot`-Entities
  veralten können (dieselbe Problematik, die `LoadWindowAsync` bei der Status-Projektion
  adressiert). Bei künftigen anderen Aufrufstellen ist das zu beachten.
- **`DeleteDisplacedSlotsAsync`-Wechselwirkung:** Die Admin-Bereinigung entfernt weiterhin
  alle `Displaced`-Zeilen und damit Evidenz — einmalig können dadurch echte Neuplanungen
  ausgelöst werden (dokumentierte Wechselwirkung aus Issue #290, unverändert; gilt nicht
  für die neue Teilmengen-Bereinigung).
- **Suffix-Verdrängung:** Jede verbleibende Falsch-Negativ-Klassifikation zieht weiterhin
  die gesamte Regel-Zukunft nach sich; die Reproduktion muss deshalb neben dem Primär-
  szenario belegen, dass der gefundene Pfad auch der tatsächliche war (Diagnose-Ast im
  Bericht benennen — der neue Ablehnungsgrund-Log hilft dabei).
- **Log-Aufkommen:** Eine `LogInformation`-Zeile je invalidem Block pro Lauf — bei
  korrektem Verhalten null bis wenige Zeilen; im Fehlerfall (Massen-Fehlklassifikation)
  begrenzt durch die Anzahl invalider Blöcke, keine Endlosschleife.
- **Interface-Erweiterung:** `IProgramScheduleService` erhält ein Mitglied; einzige
  Implementierung ist `ProgramScheduleService` (keine Test-Fakes bekannt);
  `ProgramScheduleReconcileSummary` bleibt unverändert (Bereinigung liefert eigenes
  `Task<int>`-Ergebnis, kein neuer Summary-Zähler).
- **Keine weiteren Seiteneffekte:** Datenmodell, Backup-Registrierung
  (`VideoWebPlayerBackupData`), restliche Worker-Kette und UI bleiben unverändert; in der
  Admin-Ansicht verschwinden die Duplikate künftig automatisch — das ist der gewünschte
  sichtbare Effekt.

## Umsetzungsreihenfolge

1. **Fail-first-Reproduktionstest „verkettete Invader-Evidenz"**
   - Voraussetzungen: Keine — `ProgramScheduleTestDb` (In-Memory-SQLite, `CreateRuleAsync`,
     `CreateService`, `ScheduledAsync`, `AssertNoDisplacedAsync`, `AssertReconcileNoOpAsync`)
     existiert.
   - Beschreibung: Neuer Test in `ProgramScheduleServiceTests_Boundary.cs` (oder
     `_Reconcile.cs`), der das Primärszenario aufbaut und `AssertNoDisplacedAsync`/
     No-Op-Verhalten erwartet. Vor dem Fix muss er rot sein und dabei eine echte
     Selbst-Verdrängung zeigen (Displaced-Zeile mit identischem Inhalt/Zeit wie die
     Ersatz-Zeile). Fehlschlagt er nicht, stehen die weiteren Kandidatenszenarien aus
     „Programmabläufe" zur Auswahl (maximal so weit treiben, bis eine echte
     Selbst-Verdrängung reproduziert ist — Nachweis im Test).
2. **Diagnose-Ausbau: Ablehnungsgrund intern erfassen und loggen**
   - Voraussetzungen: Keine (unabhängig vom Prüffix; `ListLogger<T>` existiert bereits).
   - Beschreibung: `internal enum AiringInvalidReason` anlegen; Diagnose-Senke in
     `ReproContext`; `out`-Parameter am `AiringReproducible`-Einstieg; Stufenschreiben in
     Vorfilter und beiden Tail-Checks; `Validate` loggt Grund je invalidem Block;
     `CreateService`-Logger-Parameter; Test `Reconcile_InvalidAiring_LogsRejectionReason`.
     Liefert das Instrument für Schritt 3 und bleibt als Betriebsdiagnose bestehen.
3. **Diagnose des Ablehnungspfads**
   - Voraussetzungen: Fehlschlagender Test aus Schritt 1; Schritt 2 erleichtert den Befund.
   - Beschreibung: Feststellen, welcher Ast der Prüfkette (`AiringReproducible`-
     Vorfilter, `FlatAiringTailReproducible`, `InterleavedAiringTailReproducible`,
     Evidenzbedingung, `FindItemIndex`) den Block abgelehnt hat (neuer Log-Grund,
     Debug-Lauf); Befund im Testkommentar bzw. später im Bericht festhalten.
4. **Fix im Evidenzmodell / Hypothesenraum**
   - Voraussetzungen: Schritte 1–3.
   - Beschreibung: Bei bestätigter Primärhypothese `_invaderStarts` → Intervall-Evidenz
     (`ReconcileWindow.Rebuild`, `ReproContext`) und Shift-Beweis in beiden Tail-Checks auf
     `ShiftPastInvaders`-Fixpunkt umstellen. Bei anderem Befund: die diagnostizierte Lücke
     schließen (s. „Änderungen an bestehenden Klassen"), kein Kriterium-Wechsel.
5. **Selbst-Verdrängungs-Assertion als Testhilfe**
   - Voraussetzungen: Schritt 4.
   - Beschreibung: Hilfsassertion in `ProgramScheduleTestDb` (z. B.
     `AssertNoSelfDisplacedAsync`): schlägt fehl, wenn zu einem `Displaced`-Slot ein
     `Scheduled`-Slot mit gleichem Inhalt (`MovieId`/`TVShowEpisodeId`, sonst `MediaItemId`)
     im selben Zeitintervall existiert; im Reproduktionstest und im Worker-Loop-Test
     einsetzen.
6. **Negativ-Regression: echte Änderung verdrängt weiterhin**
   - Voraussetzungen: Schritt 4.
   - Beschreibung: Neuer Test, dass ein Block ohne beweisbare Evidenz (z. B. verschobener
     Slot nach `DeleteDisplacedSlotsAsync`-Bereinigung bzw. geänderter `TimeOfDay`)
     weiterhin genau einmal verdrängt und der Folgelauf stabil bleibt; vorhandene Tests
     (`Reconcile_ChangedTimeOfDay_ReplansAtNewTime`,
     `Reconcile_LongAiring_ChangedRule_StillDisplaced`,
     `Reconcile_UnlinkedMediaItem_StillDisplacesOnce`) müssen unverändert grün bleiben.
7. **Automatische Duplikat-Bereinigung: `CleanupSelfDisplacedSlotsAsync`**
   - Voraussetzungen: Keine — nutzt nur `ProgramSlot`-Felder und vorhandene Muster
     (`DeleteDisplacedSlotsAsync`, `PurgeExpiredRulesAsync`). Fachlich sinnvoll nach
     Schritt 4 einordnen (Bereinigung läuft dann gegen das gefixte Prüfmodell), technisch
     aber unabhängig umsetzbar.
   - Beschreibung: Interface-Methode + Implementierung (`ExecuteDeleteAsync` mit
     `Any`-Zwillingssubquery, `LogInformation`, `Task<int>`) sowie Worker-Einbindung vor
     `ReconcileAsync` inkl. Kommentar.
8. **Bereinigungs-Tests inkl. Gegenprobe**
   - Voraussetzungen: Schritt 7; `CreateSlotAsync`-Seed existiert; für den
     Evidenz-Erhalt-Test das Szenario aus Schritt 1.
   - Beschreibung: Positivtest (Duplikat-Kette wird entfernt, `Scheduled`-Zwilling
     bleibt), Gegenprobe (Displaced-Zeilen ohne Zwilling, mit anderem Inhalt oder fremder
     Regel bleiben), Evidenz-Erhalt-Test (gelöschtes Evidenz-Duplikat löst keine
     Neuplanung aus — `ReconcileAsync` bleibt No-Op), Worker-Ketten-Test.
9. **Volle Testsuite + Builds**
   - Voraussetzungen: Schritte 1–8.
   - Beschreibung: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` vollständig
     grün; `dotnet build VideoPlayer.sln` in Debug **und** Release fehlerfrei (DoD).
10. **Dokumentation und Abschluss**
    - Voraussetzungen: Schritt 9.
    - Beschreibung: `docs/help/tv-sender-ablauf-technisch.md` um Worker-Kette
      (neuer Bereinigungsschritt) und Duplikat-Bereinigung ergänzen; Release Notes
      (Bugfix + Auto-Bereinigung + Diagnose-Log); Tasks-Status aktualisieren;
      Übergabe-/Berichtsdokumente gemäß `AGENTS.md` pflegen (Testlauf-Protokoll, ggf.
      Commit-Trennung von auftragsfremden Änderungen).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Reconcile_ShiftedAiring_ChainedInvaders_StaysScheduled` (fail-first) | `ProgramScheduleServiceTests_Boundary` | Verschiebung über ≥2 lückenlos verkettete fremde Belegungen bleibt geplant; Reconcile ohne Eingabeänderung ist No-Op; beweist heute die Selbst-Verdrängung |
| `Reconcile_InvalidAiring_LogsRejectionReason` | `ProgramScheduleServiceTests_Reconcile` | Ein bekanntermaßen invalider Block (z. B. geänderte `TimeOfDay`) erzeugt eine Logzeile mit dem erwarteten `AiringInvalidReason`; Service über `CreateService` mit `ListLogger<ProgramScheduleService>` erzeugt |
| `Reconcile_ShiftedAiring_ChainedInvaders_InvaderDisplaced_StableAcrossRuns` (optional, Analogon zu Issue-#290-Test) | `ProgramScheduleServiceTests_Boundary` | Ketten-Evidenz bleibt beweisbar, nachdem ein Invader der Kette verdrängt wurde (Displaced-Zeile liefert Evidenz) |
| `Reconcile_ShiftedAiring_EvidenceCleaned_ReplansOnce` (Negativ-/Wechselwirkungs-Test) | `ProgramScheduleServiceTests_Boundary` | Nach `DeleteDisplacedSlotsAsync`-Evidenzverlust wird einmalig neu geplant, Folgelauf ist No-Op (dokumentiert Wechselwirkung; Erwartung ggf. an Befund anpassen) |
| `CleanupSelfDisplaced_RemovesProvenDuplicates` | `ProgramScheduleServiceTests_Boundary` (oder neue `..._Cleanup`-Klasse) | Über `CreateSlotAsync` geseedete Duplikat-Kette (1× `Scheduled` + n× `Displaced`, identisches Intervall/Regel/Inhalt): Bereinigung löscht genau die `Displaced`-Kopien, Rückgabewert = n, Zwilling bleibt |
| `CleanupSelfDisplaced_KeepsDisplacedWithoutTwin` (Gegenprobe) | dieselbe | `Displaced`-Zeilen bleiben: (a) gleiches Intervall, anderer Inhalt (echter Eviction-Verlierer), (b) kein `Scheduled`-Slot am Intervall, (c) gleiches Intervall+Inhalt, aber andere `ProgramRuleId`, (d) abweichendes `IsClipFill` |
| `CleanupSelfDisplaced_RepresentativeSwitch_Removes` | dieselbe | Zwilling mit abweichender `MediaItemId`, aber gleichem `MovieId` (Repräsentantenwechsel) wird als Duplikat erkannt und gelöscht |
| `CleanupSelfDisplaced_PreservesInvaderEvidence_NoReplan` (Sicherheits-Gegenprobe) | `ProgramScheduleServiceTests_Boundary` | Invader-Evidenz-Szenario (verschobene Ausstrahlung, deren Verschiebe-Beleg aus einem `Scheduled`/`Displaced`-Zwillingspaar stammt): Bereinigung löscht nur die `Displaced`-Evidenzkopie; anschließendes `ReconcileAsync` bleibt No-Op (`AssertReconcileNoOpAsync`) — kein Evidenz-Verlust, keine echte Neuplanung |
| `Worker_Run_CleansSelfDisplacedDuplicates` | `ProgramScheduleWorkerTests` | Geseedetes Duplikat-Paar wird durch einen Worker-Lauf vor dem Reconcile entfernt (Ketten-Integration) |
| `AssertNoSelfDisplacedAsync` (Hilfsassertion) | `ProgramScheduleTestDb` | Fehlschlag, wenn `Displaced`- und `Scheduled`-Slot mit gleichem Inhalt (`MovieId`/`TVShowEpisodeId`, sonst `MediaItemId`) im selben Zeitintervall koexistieren — direkter Nachweis „durch sich selbst ersetzt" |
| Weiterer Reproduktionstest (nur falls Primärszenario nicht reproduziert) | `ProgramScheduleServiceTests_Reconcile` | Alternativszenario aus der Kandidatenliste (Evidenzverlust, Ring-Duplikat, Strang-Doppeldeutigkeit, `k`-Deckel) — jeweils mit Selbst-Verdrängungs-Nachweis |

### Betroffene bestehende Tests

Keine Anpassung erwartet — die Änderung macht die Prüfung ausschließlich toleranter für
beweisbare Blöcke, und die Bereinigung löscht nur streng bewiesene Duplikate.
`CreateService` bleibt durch den optionalen Logger-Parameter aufrufkompatibel.
Folgende bestehende Tests sind die kritischen Regressionszeugen und müssen unverändert
grün bleiben (der Prüfer soll sie gezielt verifizieren):
`Reconcile_ShiftedAiring_InvaderDisplaced_StableAcrossRuns`,
`Reconcile_ShiftedAndOvershootingAirings_StayStableAcrossRuns`,
`Reconcile_LongAiring_Shifted_StaysScheduled`,
`Reconcile_ChangedTimeOfDay_ReplansAtNewTime`,
`Reconcile_LongAiring_ChangedRule_StillDisplaced`,
`Reconcile_UnlinkedMediaItem_StillDisplacesOnce`,
`Reconcile_IdenticalUtcNow_SecondRunIsNoOp`,
`Reconcile_GenreInterleaved_IdenticalUtcNow_SecondRunIsNoOp`,
`Reconcile_WorkerLoop_NoDisplacedAccumulationNoOverlap`,
`DeleteDisplacedSlots_RemovesOnlyDisplaced`,
`DeleteDisplacedSlots_NonAdmin_Throws`,
sowie die `ProgramScheduleWorkerTests` (neue Methode in der Worker-Kette).

### E2E-Tests (primärer Funktionsnachweis)

Keine neuen E2E-Tests erforderlich: Die Anforderung betrifft ausschließlich die interne
Bewertungslogik von `ReconcileAsync` plus eine nicht-administrative Hintergrund-
Wartungsmethode — es gibt keinen neuen oder geänderten Benutzerfluss und keine
UI-Eingabe, die einen der Pfade gezielt auslöst (die Verschiebungs-Konstellation und die
Duplikat-Bereinigung entstehen durch Regel-Zeiten, Materialisierung und Worker-Takt, nicht
durch eine Nutzeraktion in der Oberfläche). Der anwendersichtbare Effekt (keine
„Verdrängt"-Chips nach wiederholtem Generieren, keine Duplikat-Häufung) ist bereits durch
den bestehenden E2E-Regressionstest `TVSenderAdmin_RepeatGenerate_ProducesNoDisplacedSeries`
(`VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs:649`) abgedeckt, der unverändert grün
bleiben muss.

## Offene Punkte

Keine.
