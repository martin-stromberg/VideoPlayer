# Drehbuch 07 — „Gleich geht's los!" (Warm-up-Standby)

**Ziel-Datei:** `clips/funki-gleich-gehts-los.mp4` — ca. 10 s, 1280×720, 25 fps
**Einsatz:** Standby-Clip der Warm-up-Phase des Live-Streams (Issue #266): Er läuft,
solange nach einem Server-/App-Start noch keine echte `live.m3u8` mit referenziertem
Segment existiert — bewusst sichtbarer Hinweis statt Fehlerbild. Der Clip ist zudem
fest eingebetteter Standard, wenn in den Sender-Einstellungen kein eigener
Standby-Clip gewählt ist.

## Szene

Etwas hellerer Verlaufshintergrund als die Pausen-Clips. Funki läuft von links ins
Bild und hüpft aufgeregt — er „macht den Sender warm".

| Zeit | Bild | Ton |
|------|------|-----|
| 0–1,6 s | Funki läuft von links herein (Birne brennt schon). | leiser Auftakt |
| 1,2–8,5 s | Funki hüpft aufgeregt auf der Stelle und winkt. | fröhliches, kurzes Jingle |
| 2–3 s | Einblendung groß: **„Gleich geht's los!"** | — |
| 4,5–5,3 s | Zweite Zeile klein: „Funki macht den Sender schon mal warm…" | — |
| 9–10 s | langsames Abdunkeln (sauberer Schleifen-Übergang). | Ausklang |

## Texte

- „Gleich geht's los!"
- „Funki macht den Sender schon mal warm…"

## Technik

Szene `gleich_gehts_los` (Schlüssel `gleich-gehts-los` in `CLIPS`): Lauf- herein
(`ease`), Hüpf-Animation über `abs(sin)`-Beats, Glow-Puls, Ausblenden am Ende wie
bei den anderen Clips. Die eingebetteten HLS-Segmente des Servers werden aus dieser
Datei einmalig erzeugt (siehe `VideoWebPlayer/Assets/LiveStreamStandby/` und den
dort dokumentierten ffmpeg-Befehl).
