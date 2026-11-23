# Abnahmeprüfung – Entwicklungsschritt 1

## Ergebnis

**Status:** Erfüllt

## Abweichungen

- Keine.

## Hinweise

Prüfung durch frischen Prüfer (nicht der Implementierer), gegen Commit `406db4e` auf `task/issue-356-...-schritt-1-demoinstallation`. Arbeitsverzeichnis war/ist sauber, es wurden keine Codeänderungen vorgenommen.

**Codeprüfung (Akzeptanzkriterien einzeln):**

- *Auswahl bei Erstregistrierung:* `Register.razor` Z. 47–59 blendet den Bestand nur für den ersten Benutzer ein (`isFirstUser && demoDataSets.Count > 0`, Z. 82–84); `RegisterUser` ruft `ApplyAsync` nur bei nicht-leerer Auswahl auf (Z. 111–123). Dialog war bereits vorhanden und wurde bewusst nicht geändert — Anforderung „auswählbar" erfüllt.
- *Quelltyp + Zugriffsrecht:* `FileSystemDemoDataSetService.ApplyAsync` parst `sourceType` (`LocalDirectory`, Z. 118/171–179), neutralisiert die SFTP-Felder (Z. 141–145), legt `MediaSourceUser` für den Erstbenutzer an (Z. 150–154). Pfad-Traversal-Schutz in `ResolveDemoDataPath` (Z. 191–204) geprüft.
- *Sofort sichtbar ohne manuellen Scan:* `RunFullScanAsync("Demodaten-Scan", null)` wird nach dem Commit synchron awaited (Z. 167–168) — dieselbe Pipeline wie der manuelle Komplettscan inkl. Klassifizierung (`MediaSourceScanCoordinator.cs` Z. 79–118). Der Hintergrund-`MediaSourceScanService` kann das Ergebnis in den Tests nicht vortäuschen (10 s Initial-Delay + Intervall ≥ 1 h, `MediaSourceScanService.cs` Z. 51–53, 76–98).
- *Materialisierung:* `DemoMediaContentMaterializer` kopiert den Template-Baum, schreibt je `<movie>`-/`<episodedetails>`-NFO eine `.mp4` aus dem Dummy-Video und erzeugt JPEGs per `DemoMediaImageGenerator` (SkiaSharp, 800×1200 Poster, 1280×720 Fanart, 1000×185 Banner, 640×360 Episodenbilder); idempotent bei bestehendem Zielverzeichnis.
- *Scanner-Konventionen:* Dateinamen passen zum `MediaSourceClassifier`: `tvshow.nfo` in Serien-Collection (Z. 455), `<episodedetails>` mit `season`/`episode` (Z. 611–614), `{basisname}-poster.jpg` für Filme (Z. 1081–1094) und Episoden (Z. 1362–1406), `poster.jpg`/`fanart.jpg`/`banner.jpg` für Serien (Z. 1222–1240), `seasonNN-poster.jpg` für Staffeln (Z. 1293–1310).
- *Dummy-Video:* `demo-video.mp4` per ffprobe/ffmpeg verifiziert — echtes H.264/avc1 in ISO-BMFF, 640×360, 3 s, 134.226 Bytes, `moov` direkt nach `ftyp` (faststart, browser-streaming-tauglich), Dekodierung fehlerfrei.
- *Auslieferung:* `dotnet publish -c Release` tatsächlich ausgeführt — kompletter `DemoDataSets`-Baum (JSON + 11 NFOs + MP4) liegt im Publish-Output; csproj-Regeln `Content Update="DemoDataSets\**\*.json"` + `None Update="DemoDataSets\**"` mit `CopyToPublishDirectory` greifen. `Data/DemoMedia/` ist git-ignoriert.
- *Bestand im Repo:* 3 fiktive Filme (`Der Sternenfalz`, `Nachtzug nach Marienfluss`, `Bienen ueber Berlin`), 2 Serien (`Nebelwerk`, `Stadtfunk`) à 1 Staffel/3 Episoden — mehr als gefordert.
- *Dokumentation:* `docs/help/einrichtung.md` (eigener Abschnitt inkl. Fehlerverhalten und Löschhinweis), `README.md` (Feature-Bullet), `RELEASE_NOTES.md` (EN + DE) — vorhanden und inhaltlich zutreffend.
- *Backup/Migration:* keine neuen Tabellen/Spalten in diesem Schritt → `VideoWebPlayerBackupData`-Erweiterung und EF-Migration entfallen zutreffend.

**Selbst ausgeführte Tests (Debug):** `DemoMediaImageGeneratorTests` 4/4, `FileSystemDemoDataSetServiceE2ETests` + `DemoDataRegistrationE2ETests` 8/8, `MediaSourceScanCoordinator*` 9/9, `LocalMediaSourceReader`/`MediaSourceClassifier` 54/54 — alles grün. Die Demo-E2E-Tests belegen exakt die geforderten Punkte: Quelltyp `LocalDirectory` + Zugriffsrecht, angelegtes Verzeichnis mit 9 MP4/≥11 NFO/≥15 JPG, gefüllte Bibliothek (3 Filme/2 Serien/2 Staffeln/6 Episoden/Poster), Auswahlpfad im Registrierungsformular, leere Installation ohne Auswahl, valider MP4-Container. Test-Dispose räumt `Data/DemoMedia` wieder ab (verifiziert, kein Rest im Working Tree).

**Negativ-Stichprobe (Code lesen, klarer Fall):** Ohne den `RunFullScanAsync`-Aufruf (Z. 168) wären alle Bibliotheks-Counts 0 → `ApplyAsync_LibraryIsFilledAfterScan` und `RegisterFirstUser_WithDemoDataSet_FillsLibrary` schlügen fehl; ohne `sourceType`-Parsing (Default `Sftp`) schlägt `ApplyAsync_CreatesLocalDirectorySourceWithAccess` fehl; ohne Materialisierung liefert `ApplyAsync_MaterializesMediaDirectory` 0 Dateien; `DemoVideoFile_IsValidMp4Container` schlägt bei Nicht-MP4 fehl. Asserts sind damit wirksam.

**Kleinigkeiten (keine Abweichung):** `DemoMediaSourceDefinition.Icon` wird deserialisiert, aber nicht verwendet (vor dieser Änderung bereits vorhanden). Datei-IO läuft innerhalb der DB-Transaktion; bei Fehler können Dateireste liegen bleiben — das nicht-atomare Verhalten ist dokumentiert und vom Auftrag akzeptiert. Das optionale `CopyStandardSourcesDemoData`-Target kann eine betreiberseitige `StandardSources.json` nach `DemoDataSets/` publizieren, die `GetAvailableAsync` dann als Bestand anböte, falls sie `name` enthält — pre-existing, nicht Teil dieses Schritts. Die Registrierung blockiert für die Dauer des Vollscans (beim Demobestand wenige Sekunden) — für „sofort sichtbar" gewollt.
