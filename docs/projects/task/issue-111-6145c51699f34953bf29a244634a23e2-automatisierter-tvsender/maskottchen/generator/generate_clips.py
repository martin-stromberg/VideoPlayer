#!/usr/bin/env python3
"""Prozedurale Clip-Produktion fuer Maskottchen 'Funki, das Gluehbirnchen'.

Rendert jede Szene framegenau mit Pillow (1280x720, 25 fps), streamt RGBA-Rawvideo per
stdin-Pipe an ffmpeg (libx264/yuv420p) und mischt einen kleinen Sinus-Jingle (WAV) dazu.

Aufruf:
    python generate_clips.py all            # alle Clips
    python generate_clips.py begruessung    # einen Clip

Voraussetzungen: Python 3.11+, Pillow, ffmpeg + ffprobe im PATH.
"""
import math
import os
import subprocess
import sys
import tempfile
import wave

from PIL import Image, ImageDraw, ImageFont

W, H, FPS = 1280, 720, 25
FONT_DIR = os.environ.get("FUNKI_FONT_DIR", r"C:\Windows\Fonts")
FONT_BOLD = os.path.join(FONT_DIR, "arialbd.ttf")
FONT_REG = os.path.join(FONT_DIR, "arial.ttf")
OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "clips")
TMP_DIR = tempfile.gettempdir()


def font(size, bold=True):
    return ImageFont.truetype(FONT_BOLD if bold else FONT_REG, size)


def clamp(v, lo=0.0, hi=1.0):
    return max(lo, min(hi, v))


def ease(t):
    return t * t * (3 - 2 * t)


def fade(t, a, b):
    """0->1 Uebergang zwischen Sekunde a und b."""
    if t <= a:
        return 0.0
    if t >= b:
        return 1.0
    return ease((t - a) / (b - a))


def lerp(a, b, k):
    return a + (b - a) * k


def gradient_bg(top=(10, 18, 46), bottom=(30, 54, 110)):
    img = Image.new("RGB", (W, H))
    px = img.load()
    for y in range(H):
        k = y / (H - 1)
        c = tuple(int(lerp(top[i], bottom[i], k)) for i in range(3))
        for x in range(W):
            px[x, y] = c
    return img.convert("RGBA")


def draw_text(draw, xy, text, size=56, fill=(245, 245, 250, 255), bold=True,
              anchor="mm", alpha=1.0, shadow=True):
    if alpha <= 0:
        return
    f = font(size, bold)
    col = (fill[0], fill[1], fill[2], int(255 * alpha))
    x, y = xy
    if shadow:
        draw.text((x + 3, y + 3), text, font=f, fill=(0, 0, 0, int(160 * alpha)), anchor=anchor)
    draw.text((x, y), text, font=f, fill=col, anchor=anchor)


def draw_stars(draw, count, seed_offset=0, alpha=1.0):
    """Deterministische Sterne (kein echter Zufall, reproduzierbar)."""
    for i in range(count):
        sx = (i * 137 + 97 * seed_offset + 31) % W
        sy = ((i * 211 + 53 * seed_offset + 17) % (H - 240))
        r = 1 + (i * 7 + seed_offset) % 3
        tw = 0.55 + 0.45 * math.sin(i * 1.7)
        a = int(200 * alpha * tw)
        draw.ellipse([sx - r, sy - r, sx + r, sy + r], fill=(255, 255, 230, a))


def draw_funki(img, cx, cy, scale=1.0, glow=1.0, wave=0.0, sleepy=0.0,
               yawn=False, wink=False, point=False, dim=0.0, mini_hue=None):
    """Zeichnet Funki: Gluehbirnen-Koerper, Sockel, Gesicht, duenne Arme/Beine.

    glow 0..1 wie hell die Birne brennt; wave 0..1 Arm-Winkphase; sleepy 0..1 Augen-
    Schlitz; dim 0..1 global abdunkeln; mini_hue optional (R,G,B) Ersatz-Fuellfarbe.
    """
    d = ImageDraw.Draw(img)
    r = 70 * scale  # Kolbenradius
    on = glow
    # Fuellfarbe: grau (aus) -> warm gelb (an)
    base = (lerp(150, 255, on), lerp(150, 204, on), lerp(140, 80, on))
    if mini_hue is not None:
        base = mini_hue
    dk = 1 - dim
    body = tuple(int(c * dk) for c in base)

    # Schein hinter der Birne
    if on > 0.05:
        halo = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        hd = ImageDraw.Draw(halo)
        for k in range(5, 0, -1):
            rr = r + k * 18 * scale * on
            a = int(10 * on * dk * k)
            hd.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=(255, 224, 130, a))
        img.alpha_composite(halo)

    # Beine (Stoeckchen + Fuesse)
    leg_y = cy + r + 26 * scale
    for sgn in (-1, 1):
        lx = cx + sgn * 22 * scale
        d.line([lx, cy + r - 6 * scale, lx + sgn * 4 * scale, leg_y],
               fill=(70, 70, 80, 255), width=int(6 * scale))
        d.ellipse([lx + sgn * 4 * scale - 12 * scale, leg_y - 4 * scale,
                   lx + sgn * 4 * scale + 12 * scale, leg_y + 4 * scale],
                  fill=(60, 60, 70, 255))

    # Arme: links locker, rechts winkt oder zeigt
    ay = cy + 6 * scale
    d.line([cx - r + 6 * scale, ay, cx - r - 26 * scale, ay + 30 * scale],
           fill=(70, 70, 80, 255), width=int(6 * scale))
    d.ellipse([cx - r - 34 * scale, ay + 26 * scale, cx - r - 18 * scale, ay + 42 * scale],
              fill=(255, 224, 130, int(255 * dk)))
    if point:
        # rechter Arm zeigt nach rechts oben (Texttafel)
        hx, hy = cx + r + 44 * scale, ay - 22 * scale
        d.line([cx + r - 6 * scale, ay, hx, hy], fill=(70, 70, 80, 255), width=int(6 * scale))
    else:
        ang = -0.5 - 0.9 * wave  # wave oszilliert -> Wedeln
        hx = cx + r * math.cos(ang) + 10 * scale
        hy = ay - r * abs(math.sin(ang)) - 8 * scale
        d.line([cx + r - 6 * scale, ay, hx, hy], fill=(70, 70, 80, 255), width=int(6 * scale))
    d.ellipse([hx - 8 * scale, hy - 8 * scale, hx + 8 * scale, hy + 8 * scale],
              fill=(255, 224, 130, int(255 * dk)))

    # Kolben (Kreis) + Sockel
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=body + (255,), outline=(60, 50, 30, 255),
              width=max(2, int(4 * scale)))
    # Gluehfaden: zwei kleine Schlaufen + Stiel, hell bei on
    fy = cy - r * 0.25
    fil = (255, 250, 190, int(255 * (0.35 + 0.65 * on) * dk))
    d.line([cx - 14 * scale, cy + r * 0.55, cx - 14 * scale, fy], fill=fil, width=int(4 * scale))
    d.line([cx + 14 * scale, cy + r * 0.55, cx + 14 * scale, fy], fill=fil, width=int(4 * scale))
    d.arc([cx - 14 * scale, fy - 10 * scale, cx + 14 * scale, fy + 10 * scale],
          start=180, end=360, fill=fil, width=int(4 * scale))
    # Sockel
    bw = r * 0.78
    by0 = cy + r - 2 * scale
    by1 = by0 + 34 * scale
    d.polygon([(cx - bw, by0), (cx + bw, by0), (cx + bw * 0.62, by1), (cx - bw * 0.62, by1)],
              fill=(96, 96, 104, int(255 * dk)), outline=(50, 50, 58, 255))
    for i in (1, 2, 3):
        yy = by0 + i * 9 * scale
        d.line([cx - bw + i * 2 * scale, yy, cx + bw - i * 2 * scale, yy],
               fill=(70, 70, 78, 255), width=int(3 * scale))

    # Gesicht: Augen + Mund
    ey = cy - r * 0.05
    ex = r * 0.38
    eye_r = 13 * scale
    if sleepy > 0.02:
        # Schlitzaugen
        for sgn in (-1, 1):
            d.line([cx + sgn * ex - eye_r, ey, cx + sgn * ex + eye_r, ey],
                   fill=(35, 35, 45, 255), width=int(5 * scale))
    else:
        for sgn in (-1, 1):
            if wink and sgn == 1:
                d.line([cx + ex - eye_r, ey, cx + ex + eye_r, ey],
                       fill=(35, 35, 45, 255), width=int(5 * scale))
                continue
            d.ellipse([cx + sgn * ex - eye_r, ey - eye_r, cx + sgn * ex + eye_r, ey + eye_r],
                      fill=(255, 255, 255, 255))
            pr = eye_r * 0.55
            d.ellipse([cx + sgn * ex - pr, ey - pr, cx + sgn * ex + pr, ey + pr],
                      fill=(35, 35, 45, 255))
    # Mund: Laecheln-Bogen oder Gaehn-Oval
    my = cy + r * 0.42
    if yawn:
        d.ellipse([cx - 12 * scale, my - 8 * scale, cx + 12 * scale, my + 16 * scale],
                  fill=(60, 40, 40, 255))
    else:
        d.arc([cx - 20 * scale, my - 14 * scale, cx + 20 * scale, my + 12 * scale],
              start=20, end=160, fill=(60, 40, 30, 255), width=int(4 * scale))


def typewriter(text, t, start, cps=14):
    n = int(max(0, (t - start)) * cps)
    return text[:n]


def progress_bar(d, t, a, b, x0, x1, y):
    k = clamp((t - a) / (b - a))
    d.rounded_rectangle([x0, y, x1, y + 10], radius=5, fill=(40, 50, 80, 255))
    if k > 0:
        d.rounded_rectangle([x0, y, lerp(x0, x1, k), y + 10], radius=5, fill=(255, 205, 90, 255))


# ---------------------------------------------------------------- audio ----

def synth_wav(path, notes, duration_s, sr=44100):
    """notes: Liste (freq_hz, start_s, dauer_s, lautstaerke 0..1). Sinus + weiche Huellkurve.
    Der Puffer wird auf die Clipdauer gebracht, damit '-shortest' das Video nicht kuerzt."""
    total = int(duration_s * sr)
    buf = [0.0] * total
    for f, st, du, vol in notes:
        i0, i1 = int(st * sr), int((st + du) * sr)
        for i in range(i0, min(i1, total)):
            k = (i - i0) / max(1, i1 - i0)
            env = min(1.0, k * 8) * min(1.0, (1 - k) * 6)
            buf[i] += vol * env * math.sin(2 * math.pi * f * (i - i0) / sr)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        frames = bytearray()
        for v in buf:
            frames += int(max(-1, min(1, v)) * 30000).to_bytes(2, "little", signed=True)
        w.writeframes(frames)


def jingle_up():
    return [(523, 0.0, 0.22, 0.30), (659, 0.22, 0.22, 0.30), (784, 0.44, 0.5, 0.32),
            (1046, 0.94, 0.7, 0.18)]

def jingle_ident():
    return [(784, 0.0, 0.3, 0.30), (988, 0.3, 0.6, 0.32), (659, 0.0, 0.9, 0.10)]

def jingle_down():
    return [(784, 0.0, 0.3, 0.26), (659, 0.3, 0.3, 0.26), (523, 0.6, 0.8, 0.28)]

def hum_sleepy():
    return [(196, 0.0, 2.4, 0.10), (147, 2.4, 2.4, 0.10), (196, 4.8, 2.6, 0.08)]

def jingle_ad():
    return [(660, 0.0, 0.18, 0.28), (880, 0.18, 0.18, 0.28), (660, 0.36, 0.18, 0.24),
            (990, 0.54, 0.5, 0.30)]

def jingle_soft():
    return [(392, 0.0, 0.5, 0.18), (330, 0.5, 0.5, 0.18), (262, 1.0, 1.0, 0.16)]


# --------------------------------------------------------------- scenes ----

def scene_begruessung(t):
    """12 s: Funki schaltet sich ein, winkt, begruesst."""
    img = gradient_bg()
    d = ImageDraw.Draw(img)
    glow = 0.15 + 0.85 * fade(t, 1.0, 2.0)
    if 1.0 < t < 1.7:  # Flackern beim Einschalten
        glow *= 0.4 + 0.6 * (int(t * 12) % 2)
    bob = 6 * math.sin(t * 2.2) * fade(t, 2, 3)
    hop = -34 * math.sin(math.pi * clamp((t - 6.0) / 0.6)) if 6.0 < t < 6.6 else 0
    draw_funki(img, W // 2, H // 2 - 30 + bob + hop, glow=glow,
               wave=0.5 + 0.5 * math.sin(t * 5) if 2 < t < 10 else 0.2)
    a1 = fade(t, 2.0, 3.0)
    draw_text(d, (W // 2, H - 220), "Hallo! Ich bin Funki —", 60, alpha=a1)
    draw_text(d, (W // 2, H - 140), "euer Glühbirnchen!", 60, alpha=fade(t, 6.2, 7.0),
              fill=(255, 226, 120, 255))
    draw_text(d, (W // 2, H - 60), "Gleich geht's weiter!", 40, alpha=fade(t, 10.0, 10.8),
              bold=False)
    if t > 11.0:
        overlay = Image.new("RGBA", (W, H), (8, 12, 30, int(255 * fade(t, 11.0, 12.0))))
        img.alpha_composite(overlay)
    return img


def scene_programmhinweis(t):
    """15 s: Funki zeigt auf Texttafel mit Typewriter."""
    img = gradient_bg()
    d = ImageDraw.Draw(img)
    x = lerp(-200, 260, ease(clamp(t / 1.4)))
    bob = 4 * math.sin(t * 2.4) if t > 1.4 else 0
    point = 1.5 <= t < 13.0
    nod = 8 * math.sin(t * 6) if 4.5 < t < 5.4 else 0
    draw_funki(img, int(x), 400 + bob + nod, glow=0.9 + 0.1 * fade(t, 8, 9), point=point)
    tx = W // 2 + 140
    draw_text(d, (tx, 260), typewriter("Gleich geht's weiter", t, 1.5), 58,
              alpha=fade(t, 1.4, 1.6))
    draw_text(d, (tx, 340), typewriter("mit eurem Programm", t, 4.0), 58,
              alpha=fade(t, 3.9, 4.1))
    draw_text(d, (tx, 470), "…kurz warten, lohnt sich!", 34, alpha=fade(t, 8.5, 9.3),
              bold=False, fill=(200, 210, 235, 255))
    progress_bar(d, t, 8.5, 13.0, tx - 240, tx + 240, 530)
    if t > 14.0:
        img.alpha_composite(Image.new("RGBA", (W, H), (8, 12, 30, int(255 * fade(t, 14.0, 15.0)))))
    return img


def scene_technische_pause(t):
    """20 s: Funki schlummert, Zzz, wacht kurz auf."""
    img = gradient_bg(top=(6, 10, 30), bottom=(18, 30, 66))
    d = ImageDraw.Draw(img)
    draw_stars(d, 60, alpha=fade(t, 0, 2))
    awake = 0.2 < (t - 9.0) / 1.2 < 0.8 or (9.0 < t < 10.5)
    sleeping = 3.0 <= t < 9.0 or t >= 10.8
    breath = 3 * math.sin(t * 1.4)
    glow = 0.25 + 0.10 * math.sin(t * 1.4)
    if 9.0 < t < 10.5:
        glow = 0.95  # Aufschrecken
    elif t > 10.5:
        glow = 0.4 + 0.1 * math.sin(t * 1.2)
    draw_funki(img, W // 2 - 160, 400 + breath, glow=glow,
               sleepy=1.0 if sleeping else 0.0, wave=0)
    # Z-Partikel
    for i in range(3):
        phase = (t - 3.2 - i * 1.6) % 4.8
        if 0 <= phase <= 2.6 and sleeping:
            zx = W // 2 - 60 + i * 26 + phase * 20
            zy = 300 - phase * 55
            draw_text(d, (zx, zy), "Z", 40 - i * 6, alpha=clamp(1 - phase / 2.6),
                      fill=(200, 210, 240, 255))
    draw_text(d, (W // 2, H - 170), "Kurze technische Pause —", 54, alpha=fade(t, 14.0, 14.8))
    draw_text(d, (W // 2, H - 95), "Funki holt frische Filme…", 40, alpha=fade(t, 15.4, 16.2),
              bold=False)
    draw_text(d, (W // 2, H - 40), "Gleich geht's weiter.", 30, alpha=fade(t, 18.0, 18.6),
              bold=False, fill=(255, 226, 120, 255))
    if t > 19.0:
        img.alpha_composite(Image.new("RGBA", (W, H), (4, 6, 18, int(255 * fade(t, 19.0, 20.0)))))
    return img


def scene_senderkennzeichen(t):
    """10 s: Station-Ident — Schriftzug + Pop-in."""
    img = gradient_bg(top=(12, 20, 52), bottom=(34, 60, 122))
    d = ImageDraw.Draw(img)
    a = fade(t, 0.2, 1.0)
    draw_text(d, (W // 2, 300), "Dein TV-Sender", 96, alpha=a)
    draw_text(d, (W // 2, 380), "mit Funki, dem Glühbirnchen", 36, alpha=fade(t, 3.0, 3.8),
              bold=False)
    if t > 1.0:
        k = ease(clamp((t - 1.0) / 0.45))
        scale = lerp(0.2, 1.0, k) * (1.12 - 0.12 * ease(clamp((t - 1.5) / 0.3)))
        wink = 7.0 < t < 7.5
        draw_funki(img, W // 2, 540 - int(60 * (1 - k)), scale=scale, glow=clamp((t - 1.2) / 0.6),
                   wink=wink)
        if 7.5 < t < 8.5:
            pulse = math.sin(math.pi * (t - 7.5))
            halo = Image.new("RGBA", (W, H), (0, 0, 0, 0))
            ImageDraw.Draw(halo).ellipse(
                [W // 2 - 190, 540 - 190, W // 2 + 190, 540 + 190],
                outline=(255, 230, 140, int(200 * pulse)), width=6)
            img.alpha_composite(halo)
    if t > 9.0:
        img.alpha_composite(Image.new("RGBA", (W, H), (8, 12, 30, int(255 * fade(t, 9.0, 10.0)))))
    return img


def scene_werbepause(t):
    """12 s: Funki bewirbt 'frische Gluehbirnen' mit Mini-Birnchen."""
    img = gradient_bg(top=(14, 24, 60), bottom=(40, 66, 130))
    d = ImageDraw.Draw(img)
    # Tisch
    d.rectangle([300, 560, 980, 590], fill=(58, 48, 66, 255))
    d.rectangle([300, 590, 340, 640], fill=(46, 38, 52, 255))
    d.rectangle([940, 590, 980, 640], fill=(46, 38, 52, 255))
    draw_funki(img, 400, 360, scale=0.9, glow=0.95,
               wave=0.5 + 0.5 * math.sin(t * 3.5) if t < 2.5 else 0.15, point=t >= 2.0)
    # Mini-Ersatzbirnchen auf dem Tisch, flackert 5-8 s regenbogen
    hue = None
    if 5.0 < t < 8.0:
        k = (t - 5.0) * 120 % 360
        import colorsys
        r_, g_, b_ = colorsys.hsv_to_rgb(k / 360.0, 0.6, 1.0)
        hue = (int(r_ * 255), int(g_ * 255), int(b_ * 255))
    draw_funki(img, 640, 500, scale=0.42, glow=0.9, mini_hue=hue, sleepy=0)
    draw_text(d, (W // 2 + 60, 210), "Frische Glühbirnen!", 72, alpha=fade(t, 2.0, 2.8),
              fill=(255, 226, 120, 255))
    draw_text(d, (W // 2 + 60, 300), "Leuchten in allen Farben — garantiert!", 40,
              alpha=fade(t, 5.0, 5.8))
    draw_text(d, (W // 2 + 60, 660), "(Funkis ehrenwerte Empfehlung)", 28, alpha=fade(t, 8.0, 8.8),
              bold=False, fill=(200, 210, 235, 255))
    draw_text(d, (W // 2, H - 40), "Gleich geht's weiter!", 34, alpha=fade(t, 10.0, 10.7))
    if t > 11.0:
        img.alpha_composite(Image.new("RGBA", (W, H), (8, 12, 30, int(255 * fade(t, 11.0, 12.0)))))
    return img


def scene_gute_nacht(t):
    """18 s: Nacht, Sterne, Mond — Funki dimmt und gaahnt."""
    img = gradient_bg(top=(4, 6, 20), bottom=(14, 22, 50))
    d = ImageDraw.Draw(img)
    draw_stars(d, 90, alpha=fade(t, 0.5, 3))
    # Mond
    ma = fade(t, 0.5, 2.5)
    d.ellipse([1030, 60, 1170, 200], fill=(235, 238, 220, int(255 * ma)))
    d.ellipse([1060, 50, 1190, 190], fill=(20, 28, 56, int(255 * ma)))
    yawn = 3.0 < t < 6.5
    glow = 1.0 - 0.75 * fade(t, 4.0, 9.0) - 0.15 * fade(t, 13.0, 16.0)
    sleepy = fade(t, 8.0, 10.0) if t > 8 else (0.6 if yawn else 0.0)
    bob = 4 * math.sin(t * 1.6) if t < 8 else 0
    draw_funki(img, W // 2, 420 + bob, glow=clamp(glow, 0.08, 1.0), sleepy=sleepy,
               yawn=yawn, wave=0.4 * clamp((t - 8) / 2) * (0.5 + 0.5 * math.sin(t * 2.5)))
    draw_text(d, (W // 2, H - 160), "Das war's für heute —", 56, alpha=fade(t, 8.5, 9.3))
    draw_text(d, (W // 2, H - 80), "gute Nacht!", 48, alpha=fade(t, 10.5, 11.3),
              fill=(255, 226, 120, 255))
    if t > 16.0:
        img.alpha_composite(Image.new("RGBA", (W, H), (2, 3, 10, int(255 * fade(t, 16.0, 18.0)))))
    return img


CLIPS = {
    "begruessung": ("funki-begruessung.mp4", 12.0, scene_begruessung, jingle_up),
    "programmhinweis": ("funki-programmhinweis.mp4", 15.0, scene_programmhinweis, jingle_soft),
    "technische-pause": ("funki-technische-pause.mp4", 20.0, scene_technische_pause, hum_sleepy),
    "senderkennzeichen": ("funki-senderkennzeichen.mp4", 10.0, scene_senderkennzeichen, jingle_ident),
    "werbepause": ("funki-werbepause.mp4", 12.0, scene_werbepause, jingle_ad),
    "gute-nacht": ("funki-gute-nacht.mp4", 18.0, scene_gute_nacht, jingle_down),
}


def render_clip(key, out_dir):
    filename, duration, scene, jingle = CLIPS[key]
    os.makedirs(out_dir, exist_ok=True)
    wav = os.path.join(TMP_DIR, f"funki-{key}.wav")
    synth_wav(wav, jingle(), duration)
    out = os.path.join(out_dir, filename)
    cmd = [
        "ffmpeg", "-y",
        "-f", "rawvideo", "-pix_fmt", "rgba", "-s:v", f"{W}x{H}", "-r", str(FPS), "-i", "pipe:0",
        "-i", wav,
        "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "20", "-preset", "medium",
        "-c:a", "aac", "-b:a", "128k", "-shortest", "-movflags", "+faststart",
        out,
    ]
    errlog = os.path.join(TMP_DIR, f"funki-{key}-ffmpeg.log")
    err = open(errlog, "wb")
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE,
                            stdout=subprocess.DEVNULL, stderr=err)
    frames = int(duration * FPS)
    try:
        for f in range(frames):
            img = scene(f / FPS).convert("RGBA")
            data = img.tobytes()
            # Windows-Pipes brechen bei sehr grossen Einzel-Writes (EINVAL) — in Stuecken schreiben.
            for off in range(0, len(data), 65536):
                proc.stdin.write(data[off:off + 65536])
    except OSError:
        pass  # ffmpeg vorzeitig beendet — Fehler steht im Log
    finally:
        proc.stdin.close()
        rc = proc.wait()
        err.close()
    if rc != 0:
        with open(errlog, "r", encoding="utf-8", errors="replace") as f:
            tail = f.read()[-1500:]
        raise RuntimeError(f"ffmpeg rc={rc} fuer {filename}\n{tail}")
    probe = subprocess.run(
        ["ffprobe", "-v", "error", "-show_entries",
         "format=duration,size:stream=codec_name,width,height", "-of",
         "default=noprint_wrappers=1", out], capture_output=True, text=True)
    print(f"--- {filename} ---\n{probe.stdout.strip()}")
    os.remove(wav)


def main():
    which = sys.argv[1] if len(sys.argv) > 1 else "all"
    keys = list(CLIPS) if which == "all" else [which]
    for k in keys:
        if k not in CLIPS:
            sys.exit(f"unbekannter Clip '{k}' — bekannt: {', '.join(CLIPS)}")
        print(f"Rendere {k} …")
        render_clip(k, os.path.abspath(OUT_DIR))
    print("Fertig.")


if __name__ == "__main__":
    main()
