# Anforderung — Ständige Wiederholungen: faire Strang-Rotation bei der Interleaved-Ausstrahlungsbelegung

Quelle: Abschnitt „Korrekturanforderung" in `issue.md` (Aufgaben-ID `d6abca7c-7d38-4715-9995-22f7e835ca22`). Folgekorrektur zur Strang-Umstellung aus Issue #334.

## Fachliche Zusammenfassung

Die Round-Robin-Belegung einer Ausstrahlung über die Stränge einer interleaved `ProgramRule` (`SelectAiringItemsInterleaved` in `ProgramScheduleService.cs`) muss korrigiert werden: Beginnt die Stranganzahl die pro Ausstrahlung platzierende Titelzahl zu übersteigen — bei `SlotDuration`-Regeln regelmäßig der Fall, seit Issue #334 jede Filmsammlung einen eigenen Strang `collection:{id}` bildet —, darf nicht mehr jede Ausstrahlung beim ersten Strang der festen `StrandOrderKey`-Reihenfolge starten. Der Startstrang muss über aufeinanderfolgende Ausstrahlungen rotieren (fairer Reihum), sodass alle Stränge der Reihe nach zum Zug kommen. Die innere Ordnung geordneter Stränge (`PreservesOrder`), die Cursor-Fortschreibung (`ProgramRuleCursors`) und die titelweise Slot-Validierung bleiben unverändert gültig.

## Fachlicher Scope

- **Verarbeitungseinheit:** Die Titel-Belegung einer einzelnen Ausstrahlung (Occurrence) einer `ProgramRule` mit mehreren Strängen (`RuleState.IsInterleaved`), konkret die Strang-Auswahl in `SelectAiringItemsInterleaved`. Scope-Grenze: betroffen sind nur interleaved Regeln; flache Ein-Strang-Regeln und Filler bleiben unverändert.
- **Auslösende Ereignisse:** Kein Nutzertrigger. Der Planungslauf — `EnsureCoverageAsync` (Erstbefüllung/Lückenfüllung über `ProgramScheduleWorker` und Service-Aufrufe) sowie `ReconcileAsync`/`ReconcileWindow` (Re-Bewertung bestehender Ausstrahlungen) — wendet die korrigierte Belegungslogik an.
- **Erwartete Wirkung:** Der geschriebene `ProgramSlot`-Plan zeigt über aufeinanderfolgende Ausstrahlungen wechselnde Stränge. Auf der lokalen Instanz konkret: wechselnde Filmsammlungen/Filme statt der täglichen Wiederholung derselben zwei Titel; die bereits falsch belegten kommenden Slots werden bei der nächsten Re-Bewertung als invalid erkannt und korrekt neu geplant.
- **Umgang mit fehlenden Objekten:** Es werden keine neuen fachlichen Objekte benötigt. `ProgramRuleCursors`-Zeilen je (Regel, Strang) existieren bereits oder werden wie bisher über `PersistCursors` angelegt und fortgeschrieben — sie sind wiederzuverwenden, nicht neu einzuführen.
- **Scope-Ausschlüsse:**
  - Keine Änderung der inneren Strang-Ordnung: geordnete Stränge (Filmsammlungen in Releasedatum-Reihenfolge, Serienepisoden in Folgenreihenfolge) setzen an ihrem Fortsetzungscursor fort; es wird keine Folge/kein Sammlungsteil übersprungen oder vor dem Vorgänger gesendet.
  - Keine Änderung an der Abbruch-Semantik der Auswahl (Clip-Grace/`PlaceholderGrace`-Restzeit-Abbruch, Bound-Prüfung, Altersfenster-Deferral/Skip) — nur die Wahl des Startstrangs bzw. die Garantie des Reihums ist Gegenstand.
  - Keine Änderung am deterministischen `StrandOrderKey`-Hash-Mix und an `MixUnorderedRing`/`ItemOrderKey` (Streuung der Ringköpfe bleibt).
  - Keine neue Konfiguration, keine UI-Änderung, keine Auswahl-/Identifikationsinteraktion des Anwenders.
  - Nebenbefund Altdaten (Issue-Text): Der vom alten Flachring-Algorithmus geschriebene, inhaltlich gültige Plan, den die Ring-Kontiguitätsprüfung als `StrandSequenceBroken` verwarf, ist **kein** eigener Korrekturauftrag — nur falls im Rahmen der Korrektur ein robusterer Umgang mit Altdaten möglich ist, kann er berücksichtigt werden.

## Betroffene Klassen und Komponenten

- `VideoWebPlayer/Services/ProgramScheduleService.cs`:
  - `SelectAiringItemsInterleaved` (~Z. 1806): Round-Robin über `strands` ab `startOffset` — zentrale Korrekturstelle.
  - `StrandStartOffset` (~Z. 1421): liefert die Tagesrotation nur bei `wanted < strandCount`; bei `SlotDuration` ist `wanted = int.MaxValue`, daher stets 0 — unmittelbare Fehlerursache.
  - `RuleState.ClipGrace` (~Z. 1279) und `SelectItems` (~Z. 1761): bestimmen die faktisch platzierende Titelzahl pro Ausstrahlung — relevant für die Rotationsbedingung „Stranganzahl > platzierende Titelzahl".
  - `StrandState`/`CommitScan`/`CommitTaken`/`PersistCursors`/`NextIndexFor` (~Z. 1190, 1782, 2150, 1597): Cursor-Fortschreibung je Strang — muss unverändert korrekt bleiben, ist Regressionstest-Gegenstand.
  - `ReconcileWindow.Validate`/`SlotStillValid` (~Z. 700 ff., `AiringInvalidReason.StrandSequenceBroken`): titelweise Slot-Validierung mit Ring-Kontiguität geordneter Stränge — muss mit der neuen Belegungsreihenfolge konsistent bleiben (korrekt geplante Ausstrahlungen validieren weiter, falsch belegte werden invalidiert).
- Tests (`VideoWebPlayer.Tests`, echtes SQLite über `ProgramScheduleTestDb`): voraussichtlich neue oder erweiterte `ProgramScheduleServiceTests_*`-Klasse plus Eintrag in `ProgramScheduleConventionTests.StepTestFiles`; betroffen zu prüfen sind `ProgramScheduleServiceTests_Interleaving`, `_CollectionOrder`, `_ClipGroups`, `_Reconcile`, `_Boundary`.
- Dokumentation bei Bedarf: `docs/help/tv-sender-ablauf-technisch.md`, `docs/help/tv-sender.md`, `docs/RELEASE_NOTES.md`.
- Kein Datenmodell-, Interface-, Enum- oder UI-Eingriff erwartet (keine neue Tabelle/Migration nötig — `ProgramRuleCursors` trägt die Strang-Cursor bereits).

## Implementierungsansatz

- Erweiterungspunkt ist die Bestimmung des Startstrangs in `SelectAiringItemsInterleaved`. Die Anforderung nennt die bestehende Tagesrotation `StrandStartOffset` (`(startUtc.Date - UnixEpoch).Days % strandCount`) als Referenzmechanismus; sie lässt ausdrücklich auch andere Wege zu, solange jeder Strang reihum drankommt. Verbindlich ist das Ergebnis (faire Rotation über aufeinanderfolgende Ausstrahlungen), nicht die Mechanik.
- Die Bedingung der Rotation ist zu verallgemeinern: Heute `wanted < strandCount` (nur `EpisodeCount`-Regeln); gefordert ist die Rotation immer dann, wenn die Stranganzahl größer ist als die pro Ausstrahlung platzierende Titelzahl — was bei `SlotDuration` erst zur Laufzeit durch Laufzeiten/Clip-Grace/Bounds entsteht. Zu prüfen ist, ob die Rotation faktisch bei `strandCount > 1` ohnehin unschädlich ist (siehe Offene Fragen).
- Die Rotation muss für die Re-Bewertung reproduzierbar bleiben: Die Re-Simulations-Machinerie (`InterleavedAiringTailReproducible` u. a.) wurde inzwischen durch die titelweise Validierung ersetzt; zu klären ist, ob `SlotStillValid`/`ReconcileWindow` die Strang-Pick-Reihenfolge überhaupt querschnittlich prüft (aktuell nur Mitgliedschaft + Ring-Kontiguität je Strang) und ob die geänderte Belegung dort zu Fehlinvalidierungen führen kann.
- Cursor-Semantik bleibt: je Strang `CommitScan(scanned, lastTaken)` — gescannte Positionen verbraucht, persistierter Cursor folgt `LastPlaced`. Der rotierte Startstrang ändert nichts an diesem Mechanismus; Tests müssen die korrekte Fortschreibung über die Rotation hinweg belegen.
- Neue Tests nach Konvention: Reproduktion des Fehlers (Genre-Regel mit vielen `collection:{id}`-Ein-Film-Strängen + `SlotDuration` + Clip-Grace → über mehrere Tage wechselnde Sammlungen statt immer derselben ersten Stränge), Erhalt der Release-/Episodenreihenfolge je Strang über die Rotation hinweg, Re-Bewertung ersetzt falsch belegte kommende Tage.

## Konfiguration

Keine. Das Verhalten ist eine Korrektur der Planungssemantik und wird nicht schaltbar gemacht.

## Offene Fragen

- **Rotationsanker:** Rotation pro Ausstrahlungstag (analog `StrandStartOffset`, alle Occurrences eines Tages teilen den Startstrang) oder pro Occurrence (aufeinanderfolgende Ausstrahlungen desselben Tages rotieren ebenfalls)? Die Anforderung nennt beide Lesarten („über die Tage/Ausstrahlungen rotieren"); bei Regeln mit mehreren Occurrences pro Tag ist im Umsetzungsplan die faire Variante festzulegen.
- **Rotationsbedingung:** Soll die Rotation pauschal bei `strandCount > 1` greifen, oder nur nachweislich bei „Stranganzahl > platzierende Titelzahl" (womit sie bei `EpisodeCount`-Regeln mit `EpisodeCount >= strandCount` und bei `SlotDuration`-Regeln, deren Fenster alle Stränge bedienen, deaktiviert bliebe)? Letzteres ist fachlich präziser, ersteres einfacher — im Plan zu entscheiden.
- **Re-Bewertung/Altdaten:** Kann die titelweise Validierung Ausstrahlungen, die mit dem alten Offset-0-Verhalten geschrieben wurden, als „nicht mehr reproduzierbar" ablehnen, sobald der Startstrang rotiert — und ist das erwünscht (ersetzt genau die falsch belegten Tage) oder kann es übermäßige Verdrängung korrekt geplanter Ausstrahlungen auslösen? Der issue-seitig erwartete Ersatz der falsch belegten Tage muss vom Mechanismus abgedeckt sein.
- **Nebenbefund:** Soll der optionale, robustere Umgang mit vom alten Flachring-Algorithmus geschriebenen Reihenfolgen (`StrandSequenceBroken` auf Altdaten) in diesem Schritt mit erfasst oder als Folgeaufgabe abgetrennt werden?
