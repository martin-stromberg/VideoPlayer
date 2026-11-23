# Testergebnisse: Schritt 2 — GitHub Page

**Status:** Keine Fehler

## Solution-Builds und Testsuite

| Prüfung | Kommando | Ergebnis |
|---------|----------|----------|
| Debug-Build | `dotnet build VideoPlayer.sln -c Debug` | OK — 0 Fehler, 0 Warnungen |
| Release-Build | `dotnet build VideoPlayer.sln -c Release` | OK — 0 Fehler, 145 Warnungen (bekannte Analyzer-Hinweise, unverändert zum Bestand) |
| Volle Testsuite | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release` | 2980 bestanden, 3 Fehlschläge, 2 übersprungen (2985 gesamt, 7 min) |
| Wiederholung der 3 Fehlschläge | `dotnet test … --filter "FullyQualifiedName~BackupUpload_NonJsonErrorResponse…|FullyQualifiedName~LiveTv_Warmup_PlaysStandby…|FullyQualifiedName~AllowedMediaTypes_FiltersResults…"` | **3/3 bestanden** in Isolation (11 s) |

Die drei Einzelfehlschläge (`BackupUpload_NonJsonErrorResponse_ShowsFriendlyMessage`, `LiveTv_Warmup_PlaysStandbyWithoutErrorHint`, `MediaSearchSelectorTests.AllowedMediaTypes_FiltersResultsToPermittedTypes`) sind umgebungs-/lastsensitive Tests (Playwright-E2E bzw. bUnit-Wartezeit) und liefen isoliert fehlerfrei — bekannte Flake-Kategorie, keine Produktcode-Änderung in diesem Schritt.

## Statische Prüfungen

| Prüfung | Ergebnis |
|---------|----------|
| SecretScan (`tools/SecretScan`) | Bestanden |
| MarkdownLinkCheck (`tools/MarkdownLinkCheck`) | Bestanden — 476 Dateien, 342 lokale Links |
| Pre-Commit-Hooks | Beim Commit ausgeführt (Übersetzung, XML-Doku, Razor-Lokalisierung, Stubs, Enum-Coverage, SecretScan, MarkdownLinkCheck) |

## Reale E2E-Abnahme (statische Seite)

Umgebung: `pages/` per `python -m http.server` ausgeliefert, Basispfad `/VideoPlayer/` per Ordnerstruktur simuliert; Playwright/Chromium, Viewports 1440×900 und 375×700, emulierte `colorScheme` hell/dunkel. Abnahme-Skript + Screenshots: Arbeitsverzeichnis (nicht repo-versioniert), Ergebnis-Protokoll unten.

| # | Check | Ergebnis |
|---|-------|----------|
| 1 | Titel „VideoWebPlayer — Deine Videobibliothek im Browser" | PASS |
| 2–5 | Abschnitte `#features`, `#screenshots`, `#download`, `#links` vorhanden | PASS ×4 |
| 6–10 | Galerie-Bilder laden (`naturalWidth > 0`): startseite, seriendetails, medienquelle, sammlung, wiedergabe | PASS ×5 |
| 11 | Releases-Link vorhanden (2×: Hero + Download-Sektion) | PASS |
| 12 | Repository-Link | PASS |
| 13 | Dokumentations-Link (`/tree/main/docs`) | PASS |
| 14 | Kein externes Webfont-CSS | PASS |
| 15 | System-Default dunkel (`prefers-color-scheme: dark`, keine gespeicherte Wahl) | PASS |
| 16 | Toggle → hell + `localStorage('vwp-theme')='light'` | PASS |
| 17 | Persistenz nach Reload (bleibt hell) | PASS |
| 18 | Frisches Profil mit `colorScheme: light` → `data-theme=light` | PASS |
| 19 | 375 px: kein horizontaler Überlauf (`scrollWidth − clientWidth = 0`) | PASS |
| 20 | Keine JS-/Konsolenfehler | PASS |

**Ergebnis: 20/20 PASS** — Abnahme-Screenshots: `01-desktop-dark.png`, `02-desktop-light.png`, `03-mobile-375.png` (Sichtprüfung: beide Themes stimmig, helles Theme kontrastreich und lesbar, Mobile-Layout stapelt sauber ohne Überlauf).

## Sichtprüfung der eingebundenen App-Screenshots (einzeln)

Aufgenommen aus der lokalen Demoinstallation (`dotnet run --project VideoWebPlayer`, Erstbenutzer mit Demodatenbestand `Demobibliothek`, Playwright, 1600×950):

| Datei | Inhalt | Sichtprüfung |
|-------|--------|--------------|
| `startseite.webp` (34 KB) | Startseite „Martins Videosammlung", Hero + „Neu im Programm" mit 3 Postern | Layout sauber, Poster geladen, kein Defekt |
| `seriendetails.webp` (43 KB) | Seriendetails „Nebelwerk": Banner, Jahr/Genre/FSK-16, Handlung, Staffel 01 mit 3 Episoden-Karten | Layout sauber, alle Elemente lesbar |
| `medienquelle.webp` (25 KB) | Quelle „Demo-Medienbibliothek": Suchfeld, Genre-Chips, 3 Medienkarten | Layout sauber, Filter und Karten vollständig |
| `sammlung.webp` (19 KB) | Filmsammlung „Filme": Banner, Genres, Play-Button, 3 Poster | Layout sauber, Abspiel-Button sichtbar |
| `wiedergabe.webp` (23 KB) | Wiedergabe-Overlay: Demo-Video läuft (`/api/items/movie/1/stream`, `readyState 4`), Schließen-Button | Player korrekt gerendert, echte Wiedergabe |

**Dokumentierte Abweichung:** Kein Live-TV-Screenshot — der TV-Sender ist in der Demoinstallation nicht freigeschaltet; `/live-tv` zeigt nur den Hinweis „Der TV-Sender ist nicht freigeschaltet." Stattdessen decken `medienquelle` und `sammlung` die geplanten Galerie-Items (plan sah „ggf. Live-TV" vor). Hinweis in `docs/GITHUB_PAGE.md` festgehalten.

## Live-Deployment

Nicht in diesem Schritt prüfbar (Scope-Ausschluss 7): Pages-Aktivierung und Live-Erreichbarkeit erfolgen erst beim `main`-Lauf von `pages.yml`; Workflow syntaktisch/manuell geprüft, Fallback dokumentiert.
