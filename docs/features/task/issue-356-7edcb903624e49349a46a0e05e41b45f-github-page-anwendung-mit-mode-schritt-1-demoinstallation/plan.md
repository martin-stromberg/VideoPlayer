# Umsetzungsplan: Demoinstallation — mitgelieferter Demodatenbestand bei der Erstregistrierung

## Übersicht

Ein versionierter Demodatenbestand wird unter `VideoWebPlayer/DemoDataSets/`
mitgeliefert (JSON-Definition + NFO-Template-Baum + Dummy-Videodatei).
`FileSystemDemoDataSetService.ApplyAsync` wird erweitert, sodass das Anlegen
das Medienverzeichnis auf der Platte materialisiert, Anzeigebilder per
SkiaSharp erzeugt, die `MediaSource` mit `SourceType = LocalDirectory`
anlegt und danach den Scan über `MediaSourceScanCoordinator.RunFullScanAsync`
anstößt — die Bibliothek ist unmittelbar nach der Registrierung gefüllt.
`Register.razor` bleibt unverändert.

## Scope-Klärung

|| Feld | Festlegung |
||------|------------|
|| Verarbeitungseinheit | Der Demodatenbestand: JSON-Definition + Medien-Template-Struktur + Dummy-Video unter `DemoDataSets/`, angewendet per `IDemoDataSetService.ApplyAsync` |
|| Auslösende Ereignisse | Registrierung des ersten Benutzers über `/Account/Register` mit ausgewähltem Bestand (`Input.DemoDataSetId`, `isFirstUser`) |
|| Erwartete Wirkung | `MediaSource` (`LocalDirectory`, absoluter Pfad) + `MediaSourceUser`; materialisiertes Medienverzeichnis auf der Platte; gefüllte Bibliothek (Filme, Serien mit Staffeln/Episoden, Anzeigebilder) unmittelbar nach der Registrierung; abspielbare Dummy-Videos |
|| Umgang mit fehlenden Objekten | `DemoDataSets/`-Bestand → erzeugen; `SourceType`-Feld im JSON-Format → erzeugen; Medienverzeichnis auf der Platte → beim Anlegen materialisieren; Dummy-Video → im Repo beilegen; Anzeigebilder → beim Anlegen per SkiaSharp erzeugen; Scan-Trigger → in `ApplyAsync` anstoßen; `MediaSourceUser` → erzeugen (bestehender Mechanismus) |

### Scope-Ausschlüsse

|| # | Einschränkung / Ausschluss | Begründung | Status |
||---|----------------------------|------------|--------|
|| 1 | Keine Atomarität mit der Benutzeranlage: schlägt `ApplyAsync` fehl, bleibt der Benutzer angelegt, Fehlermeldung im Formular | Festlegung 4 des Projekts (Anwender) | bestätigt (Anwender-Anforderung, Issue #356) |
|| 2 | Kein neuer Quelltyp — `MediaSourceType.LocalDirectory` wird nur verwendet | Festlegung 5 des Projekts (Anwender) | bestätigt (Anwender-Anforderung, Issue #356) |
|| 3 | Keine echten Mediendaten, keine externen Metadaten-Dienste | Anforderung: „ohne echte Mediendaten und ohne externe Dienste" | bestätigt (Anwender-Anforderung, Issue #356) |
|| 4 | Keine Änderung an `Register.razor` (Auswahldialog, nur erster Benutzer) | Anforderung: „Der bestehende Auswahldialog im Registrierungsformular bleibt unverändert" | bestätigt (Anwender-Anforderung, Issue #356) |
|| 5 | Kein Quellen-Icon (`Icon`-Feld der Definition bleibt ungenutzt) | Von der Anforderung nicht gefordert; `MediaSourceIcon`-Pipeline wäre Mehraufwand ohne Akzeptanzbezug | bestätigt (Anwender-Anforderung, Issue #356 — Auswahl beschränkt sich auf die geforderten Inhalte) |
|| 6 | Kein Deinstallations-/Entfernen-Pfad für den Demobestand — die Quelle ist danach eine normale lokale Medienquelle (über Medienquellen-Admin löschbar) | Anforderung verlangt nur das Anlegen; bestehender Löschweg reicht | bestätigt (Anwender-Anforderung, Issue #356) |

## Designentscheidungen

|| Komponente / Bereich | Gewählter Ansatz | Begründung |
||----------------------|-----------------|------------|
|| Inhaltserzeugung | Dateiablage + Scan (Variante (a) aus requirement.md): NFO-Template-Baum + Dummy-Video werden materialisiert, der normale Scan/Klassifizierer erzeugt die Bibliothek | Beweis im Bestand (`LocalMediaSourcePipelineE2ETests`); kein Nachbau des Verknüpfungsmodells nötig; übt den produktiven Pfad |
|| Template-Format | NFO-Dateien liegen versioniert im Template-Baum; der Service legt beim Anlegen pro Movie-/Episode-NFO die zugehörige `.mp4` aus der beigelegten Dummy-Datei an und generiert die Anzeigebilder | NFOs sind die einzige Quelle der fiktiven Metadaten — kein separates Manifest, keine Metadaten-Duplikation; Video-Bytes nur einmal im Repo |
|| Zielverzeichnis | `{ContentRootPath}/Data/DemoMedia/{dataSetId}` (Definition trägt `path` relativ, aufgelöst unter ContentRoot) | `Data/` ist die etablierte schreibbare Ablage (DB, Backups); absoluter Pfad für `LocalDirectory` entsteht erst zur Laufzeit |
|| Scan-Anstoß | Synchron in `ApplyAsync` nach dem Commit via injiziertem `MediaSourceScanCoordinator` (scoped, gleiche Lifetime) | „Unmittelbar danach sichtbar" (AK-2); bei ~9 Dateien dauert der Lauf Sekunden; Fehlerverhalten wie bisher (Fehler → Formular) |
|| Bildgenerierung | Eigene kleine Klasse `DemoMediaImageGenerator` (SkiaSharp): Farbverlauf + Titeltext, JPEG | Festlegung 2; `EpisodeBackgroundImageGenerator` ist auf Episoden-Hintergründe/`Picture`-Entitäten spezialisiert — Wiederverwendung nicht passend |
|| Dummy-Video | Eine `demo-video.mp4` (h264, ~3 s, 640×360, faststart) im Bestand; pro Videoeintrag wird sie kopiert | Festlegung 1; ffmpeg nicht gebündelt → statisch beigelegt; Dateinamen unterscheiden sich, Bytes identisch |

## Programmabläufe

### Erstregistrierung mit Demodatenbestand (Happy Path)

1. Erster Benutzer öffnet `/Account/Register`; `OnInitializedAsync` setzt
   `isFirstUser = true`, `GetAvailableAsync()` liest
   `VideoWebPlayer/DemoDataSets/Demobibliothek.json` → Auswahlfeld zeigt den
   Bestand.
2. POST des Formulars mit `Input.DemoDataSetId = "Demobibliothek"`;
   `UserManager.CreateAsync` legt den Admin-Benutzer an (unverändert).
3. `FileSystemDemoDataSetService.ApplyAsync("Demobibliothek", user)`:
   a. JSON-Definition laden (wie bisher).
   b. Pro `mediaSources`-Eintrag mit `sourceType = "LocalDirectory"` und
      `contentDirectory`: Template-Baum aus dem Bestand in das Zielverzeichnis
      (`path` relativ unter `ContentRootPath`, z. B. `Data/DemoMedia/Demobibliothek`)
      kopieren — nur wenn das Ziel noch nicht existiert; pro Movie-/
      Episoden-NFO `<name>.mp4` aus `videoFile` erzeugen; Anzeigebilder per
      `DemoMediaImageGenerator` schreiben.
   c. `MediaSource` mit `SourceType = LocalDirectory`, `Path` = absoluter
      Zielpfad anlegen; `MediaSourceUser` verknüpfen (Transaktion wie bisher).
   d. Nach dem Commit: `MediaSourceScanCoordinator.RunFullScanAsync(
      "Demodaten-Scan", null)` — Scan + Klassifizierung erzeugen
      `MediaCollection`s, `MediaItem`s, `Movie`s, `TVShow`s, `TVShowSeason`s,
      `TVShowEpisode`s und `Picture`s.
4. Redirect auf `/`; Bibliothek und Menü zeigen die Inhalte.
5. Fehlerfall: Exception in 3b–3d → `Register.razor` zeigt „Die Demodaten
   konnten nicht angelegt werden."; Benutzer bleibt angelegt (Festlegung 4).

Beteiligte Klassen: `Register`, `FileSystemDemoDataSetService`,
`DemoMediaContentMaterializer` (neu), `DemoMediaImageGenerator` (neu),
`MediaSourceScanCoordinator`, `MediaSourceScanner`, `MediaSourceClassifier`,
`LocalMediaSourceReader`.

### Installation ohne Auswahl

`Input.DemoDataSetId` leer → `ApplyAsync` wird nicht aufgerufen (bzw. liefert
bei leerer Id sofort zurück) → keine Quelle, leere Bibliothek. Unverändert.

## Neue Klassen

|| Klasse | Typ | Zweck |
||--------|-----|-------|
|| `DemoMediaContentMaterializer` | Klasse (`VideoWebPlayer/Services/DemoData/`) | Kopiert den Template-Baum, schreibt `.mp4`-Dateien aus der Dummy-Datei, ruft die Bildgenerierung; idempotent (existierendes Ziel wird nicht erneut befüllt) |
|| `DemoMediaImageGenerator` | Klasse (`VideoWebPlayer/Services/DemoData/`) | Erzeugt JPEG-Anzeigebilder (Farbverlauf + Titel) per SkiaSharp: Poster 500×750, Fanart 1280×720, Banner 1000×185, Episodenbild 640×360 |
|| `VideoWebPlayer/DemoDataSets/Demobibliothek.json` | Repository-Asset | Definition: Name, Beschreibung, eine `mediaSources`-Quelle (`sourceType: LocalDirectory`, `contentDirectory`, `videoFile`, `path`) |
|| `VideoWebPlayer/DemoDataSets/Demobibliothek/` | Repository-Asset | `demo-video.mp4` + `Medien/`-Template (nur `.nfo`-Dateien): 3 Filme, 2 Serien à 1 Staffel à 3 Episoden |

## Änderungen an bestehenden Klassen

### `FileSystemDemoDataSetService` (Service)

- **Neue Abhängigkeiten:** `DemoMediaContentMaterializer`,
  `MediaSourceScanCoordinator` (beide scoped, per Konstruktor).
- **Geänderte Methoden:** `ApplyAsync` —
  pro Quelleneintrag: `SourceType` aus der Definition parsen und auf der
  `MediaSource` setzen; bei `LocalDirectory` mit `contentDirectory` vor dem
  `AddMediaSourceAsync` die Materialisierung ausführen und `Path` auf den
  absoluten Zielpfad setzen. Nach `tx.CommitAsync`:
  `RunFullScanAsync("Demodaten-Scan", null, cancellationToken)` — nur wenn
  mindestens eine Quelle angelegt wurde.
- **`DemoMediaSourceDefinition` erweitert:** `SourceType` (`string?`),
  `ContentDirectory` (`string?`), `VideoFile` (`string?`); `Path` darf relativ
  sein → Auflösung unter `ContentRootPath`.
- **Validierung:** unbekannter `sourceType`-Wert → `InvalidOperationException`;
  `contentDirectory` ohne `sourceType: LocalDirectory` → ignoriert;
  fehlendes `videoFile` bei Einträgen mit `contentDirectory` →
  `InvalidOperationException` (Bestand defekt — ehrlicher Fehler).
- `GetAvailableAsync`, `GetDemoDataFolderPath`, Fehlerverhalten: unverändert.

### `VideoWebPlayer.csproj`

- Copy-Regel erweitern: `DemoDataSets\**\*` (inkl. Unterverzeichnisse, `.nfo`,
  `.mp4`) mit `CopyToOutputDirectory`/`CopyToPublishDirectory` = PreserveNewest —
  damit der Bestand in Build- und Publish-Output liegt. Bestehende
  `Update`-Regel für `DemoDataSets\*.json` bleibt; ergänzende `None`-/`Content`-
  Items für Nicht-JSON-Dateien und verschachtelte Pfade.

### `.gitignore`

- `/VideoWebPlayer/Data/DemoMedia/` ignorieren (materialisierte Laufzeitdaten).

## Datenbankmigrationen

Keine.

## Validierungsregeln

|| Feld / Objekt | Regel | Fehlerfall |
||---------------|-------|------------|
|| `sourceType` (JSON) | Muss auf `MediaSourceType`-Enum-Wert abbildbar sein (OrdinalIgnoreCase); fehlend → bisheriger Default `Sftp` | `InvalidOperationException` mit Bestand- und Quellenname |
|| `path` bei `LocalDirectory` | Relativ → unter `ContentRootPath` auflösen; absolut → direkt verwenden | materialisiertes Ziel nicht beschreibbar → Exception aus Dateisystem (unverändertes Fehlerverhalten) |
|| `contentDirectory` | Pfad relativ zum `DemoDataSets`-Ordner; muss existieren, wenn angegeben | `InvalidOperationException` |
|| `videoFile` | Pfad relativ zum `DemoDataSets`-Ordner; Pflicht bei `contentDirectory` | `InvalidOperationException` |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Registrierung dauert länger:** synchroner Scan (~Sekunden bei 9 Dateien)
  verlängert den POST; akzeptabel und von der Anforderung gefordert.
- **Hintergrund-Scanner:** `MediaSourceScanService` startet ~10 s nach
  Hoststart und scannt ebenfalls — die Pipeline ist idempotent
  (Scan-Diffing, Upsert-Logik), Doppelanläufe sind ungefährlich; das
  prozessweite `_classificationRunning`-Flag serialisiert die Klassifizierung.
- **Materialisierte Daten im Repo-Arbeitsverzeichnis:** bei Dev-/Testläufen
  landet `Data/DemoMedia/` im Projektverzeichnis → `.gitignore`-Eintrag;
  Tests räumen in `Dispose` auf.
- **StandardSources.json-Target** bleibt unverändert (anderer Zweck,
  weiterhin kein Fund).
- **Mehrere Quellen je Bestand:** jede `LocalDirectory`-Quelle mit
  `contentDirectory` braucht ein eigenes Ziel — `path` ist je Eintrag
  anzugeben; ohne `path` greift die bisherige Pfadlogik (leer/relativ wie
  bisher — unverändertes Verhalten für Bestände ohne Inhalte).

## Umsetzungsreihenfolge

1. **Dummy-Video erzeugen und beilegen**
   - Voraussetzungen: ffmpeg auf der Entwicklungsmaschine (vorhanden, Version 9.0.2).
   - `demo-video.mp4` (h264, yuv420p, faststart, ~3 s, möglichst klein) per
     ffmpeg erzeugen, unter `VideoWebPlayer/DemoDataSets/Demobibliothek/` ablegen.

2. **Template-Baum und JSON-Definition anlegen**
   - Voraussetzungen: Schritt 1.
   - `Demobibliothek.json` + `Medien/`-NFO-Baum (3 Filme; 2 Serien à
     `tvshow.nfo` + `Staffel 1/` mit 3 Episoden-NFOs; Felder: `title`,
     `genre`, `plot`, `premiered`/`releasedate`, `season`, `episode`,
     `aired`, `studio`, `mpaa`).

3. **`DemoMediaImageGenerator` implementieren**
   - Voraussetzungen: SkiaSharp-Dependency (vorhanden).
   - JPEG-Erzeugung (Gradient + Titeltext) für Poster/Fanart/Banner/Episodenbild.

4. **`DemoMediaContentMaterializer` implementieren**
   - Voraussetzungen: Schritte 2–3.
   - Baum kopieren, `.mp4` je NFO anlegen (Root-Element `movie`/
     `episodedetails`), Bilder schreiben; idempotent.

5. **`FileSystemDemoDataSetService` erweitern**
   - Voraussetzungen: Schritt 4.
   - `SourceType`/`ContentDirectory`/`VideoFile` in der Definition;
     Materialisierung + `SourceType`-Setzen + Scan-Anstoß; DI-Konstruktor
     erweitern (DI-Registrierung `AddScoped` bleibt).

6. **csproj-Copy-Regel + `.gitignore`**
   - Voraussetzungen: Schritt 2 (Dateien existieren).
   - `DemoDataSets\**` in Output/Publish; `Data/DemoMedia/` ignorieren.
   - Verifizieren: `dotnet publish` → Bestand liegt im Publish-Output.

7. **Tests schreiben**
   - Voraussetzungen: Schritte 1–6.
   - Neue Testklassen gemäß Abschnitt „Tests".

## Tests

### Neue Tests

|| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
||--------------------|------------|-------------------------------------|
|| `GetAvailableAsync_ReturnsDemobibliothek` | `FileSystemDemoDataSetServiceTests` (`OsInterface`, `MediaSourceClassifierCollection`) | Shipped Bestand wird mit Name/Beschreibung gelistet |
|| `ApplyAsync_CreatesLocalDirectorySourceWithAccess` | `FileSystemDemoDataSetServiceE2ETests` | `MediaSource.SourceType == LocalDirectory`, `Path` rooted, `MediaSourceUser` für den Benutzer (WebApplicationFactory + echte SQLite, Services aus DI-Scope) |
|| `ApplyAsync_MaterializesMediaDirectory` | `FileSystemDemoDataSetServiceE2ETests` | Zielverzeichnis existiert, enthält `.mp4`/`.nfo`/`.jpg` (je Film/Episode ein Video, Poster/Fanart/Season-Bilder) |
|| `ApplyAsync_LibraryIsFilledAfterScan` | `FileSystemDemoDataSetServiceE2ETests` | Nach `ApplyAsync`: ≥3 `Movies`, ≥2 `TVShows`, `TVShowSeason` + ≥6 `TVShowEpisodes`, `PosterPictureId` gesetzt — ohne manuellen Scan |
|| `Create_ReturnsDecodableJpeg` | `DemoMediaImageGeneratorTests` | Generierte Bytes sind dekodierbares JPEG mit erwarteten Maßen (SkiaSharp-Decode) |
|| `DemoVideoFile_IsValidMp4Container` | `FileSystemDemoDataSetServiceTests` (`OsInterface`) | Beigelegte `demo-video.mp4` enthält `ftyp`- und `moov`-Boxen und eine Spur (Container-Sanity-Check ohne ffprobe) |
|| `RegisterPage_FirstUser_ShowsDemoDataSetOption` | `DemoDataRegistrationE2ETests` (`E2E`+`OsInterface`, `MediaSourceClassifierCollection`) | GET `/Account/Register` enthält `id="demo-data-set"` mit Option `Demobibliothek` |
|| `RegisterFirstUser_WithDemoDataSet_FillsLibrary` | `DemoDataRegistrationE2ETests` | Form-POST mit `Input.DemoDataSetId` → User (Admin), `MediaSource` `LocalDirectory` + `MediaSourceUser`, gefüllte Bibliothek — kompletter Happy Path über das Formular |
|| `RegisterFirstUser_WithoutDemoDataSet_StaysEmpty` | `DemoDataRegistrationE2ETests` | POST ohne Auswahl → User angelegt, keine `MediaSource`, leere Bibliothek (AK-4) |

Testklasse-Konvention: E2E-Hosts wie `LocalMediaSourcePipelineE2ETests`/
`FirstUserRedirectE2ETests` (`WebApplicationFactory`, Temp-SQLite);
Materialisierungs-Ziel in `Dispose` per `TempDirectoryCleanup` räumen;
`MediaSourceClassifierCollection`-Serialisierung wegen
`_classificationRunning`-Flag.

### Betroffene bestehende Tests

Keine — `FileSystemDemoDataSetService` hat keine Tests; Konstruktoränderung
betrifft nur DI-Auflösung (scoped → scoped, keine Signatur-Brüche für Aufrufer).

### E2E-Tests (primärer Funktionsnachweis)

|| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
||-----------|----------|------------------------|-------------------------------|-------------------|
|| Pflicht | Registrierung des ersten Benutzers mit Demodaten-Auswahl über das Formular bis zur gefüllten Bibliothek | `DemoDataRegistrationE2ETests.RegisterFirstUser_WithDemoDataSet_FillsLibrary` | AK-1, AK-2, AK-5 | Einziger Weg, den ein Anwender tatsächlich geht (Formular → Apply → Scan → sichtbare Wirkung) |
|| Pflicht | Registrierung ohne Auswahl bleibt leer | `DemoDataRegistrationE2ETests.RegisterFirstUser_WithoutDemoDataSet_StaysEmpty` | AK-4 | Negativfall über denselben Benutzerfluss auslösbar |
|| Pflicht | `ApplyAsync` erzeugt Quelle, Verzeichnis, Bibliothek | `FileSystemDemoDataSetServiceE2ETests.*` | AK-1, AK-2, AK-5 | Persistenz + Dateisystem + Scan-Pipeline in echter Umgebung |
|| Pflicht | Auswahlfeld erscheint mit dem Bestand | `DemoDataRegistrationE2ETests.RegisterPage_FirstUser_ShowsDemoDataSetOption` | AK-1, AK-5 | Sichtbarkeit der Auswahl im echten Formular-HTML |

## Akzeptanzszenarien

|| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
||---|----------|----------|------------------|----------|-------------------|----------|
|| 1 | Happy Path: Erstregistrierung mit Demodaten | POST `/Account/Register` mit gewähltem Bestand | Keine Benutzer (außer SYSTEM), keine Quellen; `DemoDataSets/` liegt bei; `Data/DemoMedia/` existiert nicht | Formular laden → Bestand wählen → absenden → `ApplyAsync` materialisiert, legt Quelle an, stößt Scan an → Redirect | Bibliothek zeigt 3 Filme + 2 Serien mit Staffel/Episoden und Anzeigebildern; Video abspielbar | `DemoDataRegistrationE2ETests.RegisterFirstUser_WithDemoDataSet_FillsLibrary` + reale Abnahme mit Screenshots |
|| 2 | Erstregistrierung ohne Auswahl | POST `/Account/Register` ohne Bestand | wie 1 | absenden ohne Auswahl | Leere Bibliothek, keine Quelle | `DemoDataRegistrationE2ETests.RegisterFirstUser_WithoutDemoDataSet_StaysEmpty` |
|| 3 | Materialisierungsziel existiert bereits (Wiederholung/Re-apply) | `ApplyAsync` bei vorhandenem `Data/DemoMedia/Demobibliothek` | Verzeichnis von früherem Lauf vorhanden | Materialisierung wird übersprungen, Quelle wird angelegt (sofern Name frei), Scan läuft | Bibliothek gefüllt, keine doppelten Dateien | Service-E2E (`ApplyAsync` zweimal/idempotent) — in `ApplyAsync_MaterializesMediaDirectory` mitabgedeckt |
|| 4 | Fehler beim Anlegen | `ApplyAsync` wirft (z. B. defekter Bestand) | wie 1 | absenden → Exception | Formular zeigt „Die Demodaten konnten nicht angelegt werden.", Benutzer existiert | Bestehendes Verhalten in `Register.razor` (unverändert); implizit durch Code-Review |
|| 5 | Wartesituation: paralleler Hintergrundscan | `MediaSourceScanService` läuft zeitgleich | Scan-Service-Loop aktiv | Apply-Scan und Hintergrundscan überlappen | `_classificationRunning` serialisiert Klassifizierung; Inhalte erscheinen genau einmal (Upsert) | Serialisierung über `MediaSourceClassifierCollection` in Tests; Pipeline-Upsert bestehend |

## Offene Punkte

Keine.
