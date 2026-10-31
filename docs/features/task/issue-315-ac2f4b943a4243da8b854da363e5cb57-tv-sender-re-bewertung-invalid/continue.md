# Folgepunkte — Issue #315

## Fachlich offen (in diesem Change nicht adressiert)

1. **`-re` + inputseitiges `-ss`:** ffmpeg drosselt den Seek-Discard auf Echtzeit;
   bei nicht-suchbaren/schlecht indizierten Containern (HTTP ohne Range, schwacher
   Index) entsteht merkliche Latenz bis zur Zielposition. Nicht geändert, weil ein
   Optionsset wie `-noaccurate_seek` die Positionskorrektheit verändern kann —
   bedarf eigener Messung/Suche gegen die betroffenen Quellen. Beobachtungsansatz:
   FfmpegLiveStreamSegmenter `DrainStandardErrorAsync`-Zeit bis zum ersten Segment.

2. **NextIndex-Rekonstruktionsgrenze (Mittel, aus review-code.md):** Die Basis
   ist der Plan-End-Cursor, nicht der Occurrence-Stand. Ergänzend wäre die
   Hypothese `p_s = 0` (Strang beigetragen hatte nichts — war deferriert)
   zulässig zu prüfen; ein Integrationstest mit zwei nicht-kontigüren Gruppen
   derselben Interleaved-Regel würde den Fall absichern.

3. **E2E-Flake `Admin_CreateEditDeactivateDeleteRule`:** Die Tageswahl wählt nur
   den Tag des ersten Seed-Slots (+2h); der verdrängte Seed (+3h) liegt ab ~21:00
   Ortszeit am Folgetag → deterministischer Timeout am Abend. Pre-Existing seit
   c30cbba; Behebung im Test: Tag des verdrängten Slots separat wählen oder Seed-
   Zeiten auf feste Tageszeit umstellen.

## Bewusste Designentscheidungen (keine Folgeaufgaben)

- Evict-Veto-Drop: geschützter Slot mitten in einer Occurrence verwirft deren
  ganze Nachmaterialisierung — Fenster geht an Füller, nächste Occurrence holt
  den Inhalt über RewindCursor nach.
- Laufender Slot ohne auflösbares Rating bleibt im Modus „Jederzeit planbar"
  geschützt, auch wenn die Mediendatei weg ist (fehlendes Rating ist kein
  Mid-Air-Kill-Grund).

## Entscheidung zum Feature-Verzeichnis

Das Verzeichnis bleibt bestehen — es trägt requirement/inventory/plan-check/
review-code/test-results als Abnahme-Nachweise, auf die der PR verweist.
