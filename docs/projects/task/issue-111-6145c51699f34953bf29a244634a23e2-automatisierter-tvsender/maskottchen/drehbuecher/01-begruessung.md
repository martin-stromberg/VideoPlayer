# Drehbuch 01 — „Hallo! Ich bin Funki." (Begrüßung)

**Ziel-Datei:** `clips/funki-begruessung.mp4` — ca. 12 s, 1280×720, 25 fps
**Einsatz:** Füller/Begrüßung am Programmanfang oder nach längeren Lücken.

## Szene

Dunkelblauer Studiohintergrund mit weichem Verlauf und leichtem Leuchtschleier.

| Zeit | Bild | Ton |
|------|------|-----|
| 0–1 s | Alles dunkel. Funki steht mittig, Kolben aus (grau). | Stille |
| 1–2 s | Funki „schaltet sich ein": Glühfaden blitzt kurz zweimal, dann leuchtet der Kolben warm auf; kleiner Schein um ihn herum. | kurzes „Ding-Ding", dann warme Tonleiter aufsteigend |
| 2–6 s | Funki winkt mit dem rechten Arm (sanftes Pendeln), wippt leicht. Titelzeile fährt von unten ein: **„Hallo! Ich bin Funki —"** | freundlicher Jingle |
| 6–10 s | Zweite Zeile erscheint: **„euer Glühbirnchen!"** Funki hüpft kurz (Squash & Stretch) und strahlt heller. | Akzent-Klang |
| 10–12 s | Einblendung unten: **„Gleich geht's weiter!"** Funki winkt noch einmal, langsames Ausblenden ins Dunkel. | Jingle klingt aus |

## Texte (eingeblendet)

- „Hallo! Ich bin Funki —"
- „euer Glühbirnchen!"
- „Gleich geht's weiter!"

## Technik

Prozedural generiert (`generator/generate_clips.py`, Szene `begruessung`): Pillow-Frames →
ffmpeg. Aufleuchten = Glow-Puls, Winken = Armwinkel-Animation, Einblendungen = Fade/Slide.
