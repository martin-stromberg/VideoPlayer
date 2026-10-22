# Maskottchen „Funki, das Glühbirnchen" — Interstitial-Clips

Dieses Verzeichnis enthält alles rund um die Sender-Kennzeichen-/Füller-Clips (N12, Schritt 18):

- `drehbuecher/` — die kleinen Drehbücher je Clip (deutsch, ca. 10–30 s).
- `clips/` — die fertig produzierten MP4-Dateien (1280×720, 25 fps, H.264 + AAC).
- `generator/generate_clips.py` — das Produktionsskript (Python 3 + Pillow → rawvideo →
  ffmpeg). Aufruf: `python generate_clips.py all` bzw. `python generate_clips.py <clipname>`;
  benötigt `ffmpeg`/`ffprobe` im PATH und Arial unter `C:\Windows\Fonts` (Pfad im Skript oben
  konfigurierbar). Jeder Clip hat ein eigenes kleines Soundlogo (Sinus-Jingle).

## Figur

„Funki" ist eine freundliche Glühbirne: leuchtend gelber Kolben mit warmem Schein, grauer
Sockel, große Kulleraugen, kleiner Mund, dünne Arme und Beine. Das Erscheinungsbild wird von
`draw_funki()` im Generator zentral gezeichnet — Änderungen wirken auf alle Clips.

## Einbindung in den Sender

Siehe `docs/help/tv-sender.md`, Abschnitt „Maskottchen-Clips (Interstitials)": dedizierte
lokale Medienquelle anlegen, Clips ohne `.nfo` hineinlegen, Quelle für alle Benutzer teilen,
Füller-Regel mit Material „Clip-Quelle" anlegen.
