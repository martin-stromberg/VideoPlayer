# Bestandsaufnahme-Detail: App-Styling (`--vp-*`-Design-Tokens)

Quelle: `VideoWebPlayer/wwwroot/app.css` (Zeilen 1–25, `:root`).

## Farb-Tokens (dunkles Basis-Theme — die Anwendung ist ausschließlich dunkel)

| Token | Wert | Verwendung |
|-------|------|------------|
| `--vp-bg` | `#131313` | Haupthintergrund |
| `--vp-bg-deep` | `#0e0e0e` | Tieferer Hintergrund (Kontrast) |
| `--vp-surface-low` | `#1c1b1b` | Karten/Panels niedrig |
| `--vp-surface` | `#201f1f` | Karten/Panels |
| `--vp-surface-high` | `#2a2a2a` | Erhöhte Flächen |
| `--vp-surface-bright` | `#393939` | Hellste Fläche/Scrollbar |
| `--vp-text` | `#e5e2e1` | Haupttext |
| `--vp-text-muted` | `#bda9a6` | Sekundärtext |
| `--vp-text-soft` | `#e9bcb6` | Weicher Akzenttext |
| `--vp-border` | `rgba(255,255,255,0.1)` | Standard-Rahmen |
| `--vp-border-strong` | `rgba(255,180,170,0.42)` | Betonter Rahmen |
| `--vp-primary` | `#e50914` | Primärfarbe (Markenrot) |
| `--vp-primary-hover` | `#c0000c` | Hover-Zustand |
| `--vp-primary-soft` | `#ffb4aa` | Weicher Primärakzent (Links/Badges auf Dunkel) |
| `--vp-secondary` | `#00a8e8` | Sekundärfarbe (Blau) |
| `--vp-secondary-soft` | `#83cfff` | Weicher Sekundärakzent (Links auf Dunkel) |
| `--vp-danger` | `#ffb4ab` | Fehler-/Warnakzent |

## Weitere Tokens

- `--vp-radius-sm: 4px`, `--vp-radius: 8px` (Radien)
- `--vp-gutter: 24px`, `--vp-mobile-gutter: 20px` (Ränder)
- `--vp-focus: 0 0 0 3px rgba(0,168,232,0.34)` (Fokus-Ring)
- Body-Gradient: `linear-gradient(180deg, #121212 0%, var(--vp-bg) 48%, #0f0f0f 100%)`

## Typografie

| Kontext | Stack |
|---------|-------|
| Fließtext | `Inter, "Segoe UI", Roboto, Helvetica, Arial, sans-serif` |
| Überschriften | `Montserrat, Inter, "Segoe UI", sans-serif` |
| Code/Mono | `"JetBrains Mono", Consolas, monospace` |

Inter/Montserrat/JetBrains Mono sind **nicht** als Webfont eingebunden — die
Stacks fallen auf Systemfonts zurück. Die Seite darf daher denselben Stack ohne
externe Webfont-Einbindung verwenden (Vorgabe: System-Font-Stack).

## Konsequenz für die Seite

- Dunkle Palette: `--vp-*`-Werte direkt übernehmen (Erscheinungsbild-Anlehnung).
- Helle Palette: existiert in der App nicht — aus denselben Akzenten abzuleiten
  (weißer/heller Hintergrund, dunkle Texte, `#e50914`/`#00a8e8` als Akzente,
  ggf. dunklere Hover-Varianten für Kontrast).
