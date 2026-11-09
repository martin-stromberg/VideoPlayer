# Folgepunkte — Issue #315

## Fachlich offen (in diesem Change nicht adressiert)

1. **`-re` + inputseitiges `-ss` — Seek-Latenz auf nicht-seekbaren Eingängen
   (Mittel, Folgeissue-Kandidat).**
   `BuildArguments` (FfmpegLiveStreamSegmenter.cs:492-498) erzeugt
   `-re -ss {pos} -i {spec}`. Der Eingang (`SegmenterProcessSupport.InputSpec`,
   Kaskade in `LiveStreamMediaResolver.cs:116-175`) ist nur bei `pipe:0`
   prinzipiell nicht seekbar; `ForFile`/`ForHttp` (Range-Endpunkt) und `ForSftp`
   sind es — wobei sftp je Range-Request einen neuen SSH-Handshake zahlt.
   Für seekbare Inputs greift der Demuxer-Seek (`avformat_seek_file`) *vor* dem
   Streaming; `-re` drosselt ihn nicht. Latenz entsteht nur, wenn kein
   Zeit→Byte-Mapping existiert: `pipe:0` (jeder Byte vor dem Ziel wird in
   Echtzeit gelesen, Accurate-Seek decodiert+verwirft — Verzögerung ≈
   `PositionSeconds`, kann die Restlaufzeit übersteigen) oder seekbarer
   Container ohne brauchbaren Index (z. B. MKV ohne Cues → `-re`-gedrosselter
   Vorwärtsscan).
   **Nicht blind eingeführt:** `-noaccurate_seek` hilft nur nach erfolgreichem
   Demux-Seek (Ausgabe startet bis zu einer GOP zu früh → Drift-Wächter
   restartet ggf. in Schleife) und ist auf `pipe:0` kontraproduktiv (kein
   Discard → Stream beginnt bei Inhaltsposition 0); output-seitiges `-ss` ist
   strikt schlechter (gleiche Drosselung plus Volldekodierung); `-seekable 0`
   zerstörte den funktionierenden HTTP-Range-Seek.
   **Beobachtbarkeit (heute blind):** `ExpectedPositionSeconds` ist reine
   Wanduhr-Projektion — der 30-s-Drift-Wächter
   (LiveStreamCoordinator.cs:154-164) sieht einen hängenden Seek nie; stderr
   läuft auf `-loglevel warning`. Messgröße: Wandzeit vom Start-Log
   (`_startedAt`) bis zum ersten `seg_*_{slotId}_00000.ts` bzw. zur ersten
   Playlist-Rewrite; Diag-Lauf mit `-loglevel info` gegen die betroffenen
   Quellen zeigt, ob `avformat_seek_file` greift oder Byte-Scan/Discard läuft.
   **Nächster Schritt:** Messung an realen Quellen; erst bei Befund Optionen
   differenziert je Eingangsart ändern.

2. **NextIndex-Rekonstruktionsgrenze + Deferral-Lücke in
   `InterleavedAiringTailReproducible` (Mittel, aus review-code.md vertieft).**
   Erreichbarkeit bestätigt: `headBase[s]` (ProgramScheduleService.cs:1073)
   nutzt für Stränge ohne geschriebenen Beitrag den persistierten Cursor
   (`PersistCursors` schreibt `LastPlaced` = letzter Take des *gesamten* Plans,
   Z. 2408-2440; `NextIndexFor` Z. 1873-1880). Ein Strang mit ungeschriebenen
   Prefix-Picks in der Coverage-überspannenden Occurrence *und* Takes in einer
   beliebigen späteren Occurrence derselben Regel (bei täglichen Genre-Regeln
   mit Mehrtages-Horizont der Normalfall) erhält eine gegen Plan-Ende
   verzerrte Basis → Hypothesen landen auf falschen Ringpositionen →
   `HeadPickInconsistent` → unnötige Verdrängung (konservativ, strikte
   Tail-Verifikation verhindert false-positive-Stabilität).
   **Zweite, bisher nicht benannte Lücke:** Die Prefix-Abbildung
   `(startOffset + q) % m` setzt dichte Rotation voraus; in
   `SelectAiringItemsInterleaved` können geordnete Stränge mid-Occurrence
   deferieren (blockierter Kopf / Bound, Z. 2114-2132). Eine reale Prefix-
   Sequenz mit Deferral ist für *keine* Hypothese darstellbar → garantierte
   Fehlinvalidierung. Genau dafür wäre der Hypothesen-Arm „Strang beitrug 0
   Picks" nötig; beim Cursor-Bias hilft er nicht (dort scheitert die
   0-Hypothese weiterhin an der Verifikation — nötig wäre eine occurrence-
   lokale Basis, z. B. Begrenzung durch den ersten geschriebenen Pick des
   Strangs späterer Gruppen).
   **Trade-off:** Jeder zusätzliche Arm weitet den Raum, in dem eine stale
   Airing konsistent wirken könnte; abmilderbar über „provable deferral"
   (Deferral nur bei beweisbar zum Pick-Zeitpunkt blockiertem Strang-Kopf —
   dieselbe `IsAllowedAt`-Prüfung wie im Skip-Walk).
   **Nächster Schritt:** Integrationstest, der die Erreichbarkeit beweist
   (Genre-Regel, Coverage beginnt mid-Occurrence, Strang mit geblocktem Kopf
   bzw. Take in späterer nicht-kontigüer Gruppe); bei Reproduktion zuerst den
   provable-deferral-Arm. Schwere: Mittel — kein false-retention, aber
   Verdrängungs-Churn; verdrängte Duplikate sammeln sich bis zur manuellen
   Bereinigung.

3. **E2E-Flake `Admin_CreateEditDeactivateDeleteRule` (Niedrig, pre-existing
   seit c30cbba).**
   Das Fenster ist `localNow ∈ [21:00, 22:00)` — nur dann liegt
   der Geplant-Seed (localNow+2h) heute, der Displaced-Seed (localNow+3h) am
   Folgetag. Die Tagesleiste (`ScheduleDays`, TVSenderAdmin.razor:1731)
   enthält dann beide Tage; der Test klickt nur `seedDay` →
   `displacedRow`-Timeout *und* `program-card ≥ 2` scheitert (je Tag nur eine
   Karte). Fix-Optionen:
   - **Feste lokale Seed-Uhrzeit (empfohlen):** beide Slots auf einen festen
     Tag/Uhrzeit legen (z. B. morgen 12:00-13:00 Geplant / 13:00-14:00
     Verdrängt, lokal → UTC). Kleinster Diff, eliminiert das Fenster
     komplett, alle Assertions bleiben; der Mehrtage-Pfad ist bereits über
     `TVSenderAdminDayBarE2ETests` abgedeckt.
   - **Beide Tage wählen:** `seedDay` klicken + Geplant/Karte prüfen, dann
     `displacedDay` (`+3h` lokales Datum) klicken + Displaced-Zeile prüfen.
     Funktioniert in allen Fenstern (gleicher Tag → zweiter Klick harmlos),
     erfordert aber aufgesplittete Assertions und lässt den Split-Pfad nur
     im 21-22-Uhr-Fenster überhaupt ausführen.
   - **Nur verdrängten Tag wählen:** verworfen — die
     „Geplant"-First-Assertion scheitert dann dort; bedingte Verzweigung
     (nur bei Tagesdifferenz klicken) ebenfalls verworfen, weil der
     Fix-Pfad zeitabhängig ungetestet bliebe.

## Weitere bekannte Restrisiken (klein, kein eigener Folgepunkt)

- `StripEndlistFromPlaylist`: externer Reader ohne Write-Share lässt den
  Strip scheitern → ENDLIST-Leiche bis zur ersten Segment-Rewrite
  (zeitlich gebunden, nur Debug-Log).
- `SweepExpiredSegmentsAsync` löscht `seg_*.ts` rein nach mtime — eine
  Retention unter der Playlist-Fensterspanne kann noch referenzierte
  Segmente entfernen (keine Kopplungsprüfung).
- Standby→Echt-Übergang: `ConsumePendingStandbyTransition` ist prozessweit
  einmalig — nur der erste Playlist-Poll nach dem Übergang trägt
  `#EXT-X-DISCONTINUITY`, später zugeschaltete Clients keinen.
- Merksatz Wartemuster: Signal-Task über Iterationen wiederverwenden
  (`??=`, nach Verbrauch `null`) — Muster aus PlaylistBackfillWorker.cs:76-91,
  jetzt identisch in ProgramScheduleWorker.cs:104-118 und
  LiveStreamWorker.cs:157-171; bei künftigen Channel-Warteschleifen
  übernehmen (Ghost-Reader hungern sonst FIFO).

## Bewusste Designentscheidungen (keine Folgeaufgaben)

- Evict-Veto-Drop: geschützter Slot mitten in einer Occurrence verwirft deren
  ganze Nachmaterialisierung — Fenster geht an Füller, nächste Occurrence holt
  den Inhalt über RewindCursor nach (atomar — ein Teildrop ließe den Veto-
  Seiteneffekt halb materialisiert zurück).
- Laufender Slot ohne auflösbares Rating bleibt im Modus „Jederzeit planbar"
  geschützt, auch wenn die Mediendatei weg ist (fehlendes Rating ist kein
  Mid-Air-Kill-Grund).
- Reproduzierbarkeitsprüfung bleibt konservativ: unklarer Prefix führt zur
  Verdrängung (false-negative) statt zur Beibehaltung — jede Aufweitung des
  Hypothesenraums erfordert einen Beweis-Zusatz wie oben skizziert.

## Entscheidung zum Feature-Verzeichnis

Das Verzeichnis bleibt bestehen — es trägt requirement/inventory/plan-check/
review-code/test-results als Abnahme-Nachweise, auf die der PR verweist.
