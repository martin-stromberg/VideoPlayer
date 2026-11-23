# Testergebnisse – Schritt 1 „Demoinstallation"

## Ergebnis

**Status:** Keine Fehler

## Builds

| Build | Ergebnis |
|---|---|
| `dotnet build VideoPlayer.sln -c Debug` | 0 Fehler |
| `dotnet build VideoPlayer.sln -c Release` | 0 Fehler |

## Vollständige Testsuite

| Lauf | Ergebnis |
|---|---|
| `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release` (Vollausschnitt, mit neuen Tests) | **2983 bestanden, 0 Fehler, 2 übersprungen** |
| Baseline aus `inventory/` (Release, gleiche Suite ohne neue Tests) | 2969 bestanden, 2 bekannte Flaky-Fehler, 2 übersprungen |

Die zwei bekannten Flaky-Fälle der Baseline
(`FfmpegLiveStreamSegmenterE2ETests`, `TvSenderOnAirE2ETests`) traten in diesem
Lauf nicht auf.

Hinweis: Ein Zwischenlauf der Suite in **Debug** scheiterte an 7
bestehenden Playwright-Tests am Konsolenfehler `404
/_framework/dotnet-browser-tools/hot-reload-settings.json` — das Hot-Reload-
Einstellungsfile wird nur unter `dotnet watch` ausgeliefert und ist ein
Debug-Build-Artefakt; es hängt nicht mit dieser Änderung zusammen. Die
Baseline-Referenz (`inventory/test-results/inventory-baseline.trx`) wurde in
**Release** gefahren; der gleichartige Release-Lauf oben ist fehlerfrei.

## Neue Tests

| Test | Ergebnis |
|---|---|
| `DemoMediaImageGeneratorTests.Create_ReturnsDecodableJpeg` (4 Fälle) | bestanden |
| `FileSystemDemoDataSetServiceE2ETests.GetAvailableAsync_ReturnsDemobibliothek` | bestanden |
| `FileSystemDemoDataSetServiceE2ETests.DemoVideoFile_IsValidMp4Container` | bestanden |
| `FileSystemDemoDataSetServiceE2ETests.ApplyAsync_CreatesLocalDirectorySourceWithAccess` | bestanden |
| `FileSystemDemoDataSetServiceE2ETests.ApplyAsync_MaterializesMediaDirectory` | bestanden (9 MP4, 11 NFO, 20 JPG) |
| `FileSystemDemoDataSetServiceE2ETests.ApplyAsync_LibraryIsFilledAfterScan` | bestanden (3 Filme, 2 Serien, 2 Staffeln, 6 Episoden, Poster gesetzt) |
| `DemoDataRegistrationE2ETests.RegisterPage_FirstUser_ShowsDemoDataSetOption` | bestanden |
| `DemoDataRegistrationE2ETests.RegisterFirstUser_WithDemoDataSet_FillsLibrary` | bestanden |
| `DemoDataRegistrationE2ETests.RegisterFirstUser_WithoutDemoDataSet_StaysEmpty` | bestanden |

## Codeabdeckung

Coverlet (`coverlet.collector` 10.0.1) ist im Testprojekt vorhanden; eine
separate Coverage-Auswertung wurde nicht gefahren — **nicht messbar** in diesem
Lauf, dokumentiert gemäß Vorgabe.

## Reale Abnahme (dotnet run + echter Browser)

Ausgeführt: `dotnet run` (Testing-Umgebung, frische SQLite-DB,
`Data Source=Temp/vwp-acceptance.db`), Browser-Automatisierung über Playwright.

| Schritt | Ergebnis |
|---|---|
| GET `/Account/Register` als Erstbenutzer | Auswahl `Demodatenbestand` mit Option „Demobibliothek" sichtbar (`screenshots/01-register.png`) |
| POST Registrierung mit Demobibliothek | Redirect `RegisterConfirmation`, kein Fehler (`screenshots/02-after-register.png`); Log: Materialisierung + vollständiger Scan liefen synchron im Request |
| Login → Startseite | „Neu im Programm" mit den drei Kollektionen (`screenshots/03-home.png`) |
| `/mediasource/1` | Quelle „Demo-Medienbibliothek", Suche + Genre-Chips, 3 Kollektionskarten (`screenshots/04-mediasource.png`) |
| `/moviecollection/1` („Filme") | 3 Filme mit Postern (`screenshots/05-movies.png`) |
| `/tvshow/2` („Nebelwerk") | Fanart-Hero, FSK 16, Plot, „Staffel 01", 3 Episoden mit Bildern/Plots (`screenshots/06-tvshow.png`) |
| Episoden-Download per Token-Link | HTTP 200, 134226 Bytes, `ftypisom…avc1` — echte, browser-abspielbare MP4 |

### Sichtprüfung der Screenshots

Alle sechs Screenshots wurden einzeln auf Beschneidung, Überlauf, falsche
Maße und unleserlichen Text geprüft: keine Auffälligkeiten. Die generierten
Poster/Fanarts sind als Demo-Artwork gut erkennbar (deterministische
Zweiton-Verläufe mit Titelzeile), Titel-/Plot-Texte lesbar, keine
Layout-Defekte.

### Anmerkung zur Zwischenabnahme

Während eines parallel laufenden Testsuite-Laufs löschte das `Dispose` der
neuen E2E-Tests `Data/DemoMedia` unter dem Projekt-ContentRoot; der
gleichzeitig laufende `dotnet run`-Prozess lieferte daraufhin 404 auf
Stream/Download. Das ist ein dokumentierter Seiteneffekt der Test-Aufräumung,
kein Produktfehler: nach Ende des Testlaufs wurde die Abnahme mit frischer
DB wiederholt und alle Schritte (inkl. MP4-Download, HTTP 200) verifiziert.

## AK-Abgleich

| AK | Nachweis |
|---|---|
| AK-1 Auswahl im Registrierungsdialog unverändert, Bestand mitgeliefert | `Register.razor` ohne Diff; `DemoDataSets/` im Output; Screenshot 01 |
| AK-2 Inhalte sofort sichtbar (kein manueller Scan/Wartezeit) | Scan synchron in `ApplyAsync`; Home direkt gefüllt; `ApplyAsync_LibraryIsFilledAfterScan` |
| AK-3 Fehlerverhalten unverändert (User bleibt, Fehler im Formular) | `ApplyAsync` wirft weiter; `Register.razor` unverändert |
| AK-4 Registrierung ohne Auswahl bleibt leer | `RegisterFirstUser_WithoutDemoDataSet_StaysEmpty` |
| AK-5 2–3 Filme + 1–2 Serien mit Staffeln/Episoden, lokaler Zugriff, abspielbares Video, Bilder | 3+2 mit je 3 Episoden; `LocalDirectory`-Quelle + `MediaSourceUser`; MP4-Download 200; Poster/Fanart/Banner/Episodenbilder |
