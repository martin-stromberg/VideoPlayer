# Usability-Review (Endanwendersicht)

Geprüft nur anhand von `requirement.md` und den tatsächlichen UI-Dateien
(`LiveTv.razor`, `LiveTv.razor.css`, `NavMenu.razor`, `LiveTvErrorMessages.cs`, `liveTv.js`).

## Ergebnis

**Status:** Usability erfüllt

## Befunde

| Aspekt | Beurteilung |
|--------|-------------|
| Auffindbarkeit | „Live-TV" steht als eigener Punkt in der Hauptnavigation, für angemeldete Benutzer sichtbar (bUnit + Playwright-Klickpfad). |
| Erste Ladephase | „Lade Daten..." als verständlicher Zwischenzustand statt leerer Fläche. |
| Nicht angemeldet | Login-Dialog statt Fehlerbild (gleiches Muster wie Startseite). |
| Sendelücke | „Aktuell läuft keine Sendung." plus Hinweis „Nächste Sendung um HH:mm: Titel" — verständlich, ohne Player-Platzhalter mit Fehler. |
| Stream-Ausfall | „Der Live-Stream ist momentan nicht verfügbar. Es wird automatisch erneut versucht." — erklärt Zustand und was passiert, ohne technische Begriffe (kein „404", kein „hls.js"). |
| Browser ohne HLS/MSE | „Live-TV wird von diesem Browser leider nicht unterstützt." — klar, nicht technisch. |
| API-Fehler/keine Berechtigung | „Die Programminformationen konnten nicht geladen werden. Bitte versuchen Sie es später erneut." bzw. „Sie haben keinen Zugriff auf den TV-Sender." — verständlich. |
| Programminfo | „Jetzt läuft" mit Titel, Uhrzeiten, Restlaufzeit und Fortschrittsbalken; „Gleich" mit nächster Sendung; „Die nächsten Stunden" als begrenzte Vorschau — deckt „was läuft / was kommt gleich / Vorschau" ab. |
| Zeitformat | Uhrzeiten lokal `HH:mm`, Restdauer „… Min."/„… Std. … Min." — keine UTC-/ISO-Strings sichtbar. |
| Kein technischer Ballast | Markup enthält keine Controller-/Klassennamen oder IDs im Nutztext; nur ruhige Hinweis-Boxen (`alert`-Klasse, zentriert im Player). |
| Konsistenz | Gleiche Karten-/Border-/Farbsprache wie restliche Medienansichten (`--vp-*`-Tokens, `livetv-card` analog zu Panels). |
| Zugänglichkeit | `<video controls autoplay muted playsinline>` (muted nötig für Autoplay); `aria-hidden` nur auf dekorativen Icon/Fortschrittsbalken; Bedienbarkeit über Standard-Videosteuerung. |
| Namensabgrenzung | Sender-Vokabular („Live-TV", „Sendung", „läuft") — keine Playlist-Begriffe. |

## Offene Punkte

- Der Fortschrittsbalken ist `aria-hidden` (nur visuell) — akzeptabel, da Restlaufzeit textlich nebensteht.
- „Gleich"-Karte erscheint nur, wenn eine Folgesendung existiert — sinnvoll (kein Leerelement).

**Status:** Usability erfüllt
