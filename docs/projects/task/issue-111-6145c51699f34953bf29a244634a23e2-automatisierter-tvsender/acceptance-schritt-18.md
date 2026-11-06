# Abnahme Schritt 18 — Maskottchen-Interstitials als Füller (N12)

Prüfer: Devin (unabhängige Abnahme, nicht der Implementierer)
Geprüfter Stand: `a57b115` (`Abschluss: Maskottchen-Interstitials als Fuehler (N12)`),
Arbeitsbaum sauber, Gegenprobe-Änderung revertiert.

## Ergebnis

**Status:** Erfüllt

Die N12-Anforderung („Kurze Clips mit eigenem Sender-Maskottchen als Lückenfüller/
Kennzeichen … ggf. eigene Quelle ohne Serien/Film-Einordnung") ist vollständig umgesetzt:
neue Füller-Materialart `ProgramRuleMaterialType.MediaSourceClips = 6`
(`MaterialId` → `MediaSources.Id`), `ExpandMediaSourceClipsAsync` rotiert alle
uneingeordneten Video-`MediaItem`s der Quelle als Füller-Ring, plus 6 tatsächlich
produzierte MP4-Clips samt Drehbüchern und Generatorskript im Projektordner. Die
Werkzeug-Entscheidung (kein KI-Zugang → prozedurale Generierung mit Pillow + ffmpeg) ist
im Handover nachvollziehbar dokumentiert und fachlich vertretbar. 18 neue Tests, Suite
grün, Gegenprobe positiv.

### Verifiziert

- **Materialart + Expansion** (`ProgramRuleMaterialType.cs:35-41`,
  `ProgramScheduleService.cs:978, 1341-1391`): `ExpandMediaSourceClipsAsync` expandiert
  alle `MediaItem`s der Collections der Quelle (`mi.MediaCollection.MediaSourceId == sourceId`),
  filtert Nicht-Videos (`IsVideoFileName`), schließt über `MovieMediaItems` **und**
  `TVShowEpisodeMediaItems` verknüpfte Items aus — Trailer-Links sind `MovieMediaItems`,
  also ebenfalls ausgeschlossen. Eingeordnete Inhalte derselben Quelle bleiben außen vor
  (Test `…_ExcludesClassifiedItems`). Duration-Pflicht + Probe-Diagnosezähler via
  `CountNoDuration`, Zugriffsprüfung `access.Allows(sourceId, null, null)` — konservativ
  korrekt, da uneingeordnete Items keine Unlock-Roots (Show/Collection) haben können.
  Fehlende Quelle → `MaterialMissing`/verwaist. Stabile Ring-Reihenfolge `OrderBy(i => i.Id)`.
- **Nur Füller** (`ProgramRuleService.cs:15-20, 42-50`): `MediaSourceClips` steht nur in
  `GetAllowedMaterialTypes(Filler)`; Repeat/HolidaySpecial schließen es über dasselbe
  Ausschluss-Array wie Trailer aus — deckungsgleich mit der N17-Entscheidung
  „Trailer nur Füller". Serverseitig erzwungen (Test `…_ForSeriesFixedTime_IsRejected`),
  nicht nur UI-seitig versteckt.
- **Existenzvalidierung** (`ProgramRuleService.cs:283`): `MediaSources.AnyAsync` —
  deckungsgleich zur Expansion (`AnyAsync(s => s.Id == sourceId)` → `MaterialMissing`).
- **Ring-Fortsetzung** (`ProgramCursorContentType.cs:25`, `NextIndexFor:721`,
  `PersistCursors:945-956`): `MediaItem = 4` als Cursor-Typ; generischer
  ContentType+ContentId-Abgleich braucht keine Sonderbehandlung; Test
  `…_CursorContinuesRingAcrossRuns` belegt die Alternation am Laufübergang. Reconcile
  findet Clip-Slots über `FindItemIndex` per `MediaItemId` (Test
  `Reconcile_ClipSourceFillerSlots_StayScheduled`).
- **Titel-Fallback** (`ProgramGuideService.cs:98-160`,
  `TVSenderAdmin.razor:845-865`): Slots ohne `MovieId`/`TVShowEpisodeId` bekommen
  `MediaItem.Name` als Titel (Programmauskunft, Sendeplan-Karte, Live-TV — letztere rendert
  nur `entry.Title`, `MediaType`/`MediaId` werden nirgends dereferenziert). Erste Slots
  ohne Inhaltsreferenz überhaupt — Fallback war nötig und ist getestet
  (`GetNowAsync_BareMediaItemSlot_FallsBackToItemName`).
- **Admin-UI** (`TVSenderAdmin.razor:241-250, 1054-1066, 1193-1206`): Dropdown
  `#rule-material-clipsource` nur, wenn `AllowedMaterialTypes` den Typ enthält (nur
  Füller — bUnit-Test deckt Filler-an/nicht-Filler-aus). `OnClipSourceSelected` ist
  deckungsgleich zum `OnPlaylistSelected`-Muster; `MaterialLabel`/`DescribeMaterial`
  liefern „Clip-Quelle: <Name>" für Liste und Bearbeitung (Options-`selected` beim
  Editieren geprüft).
- **Clips:** 6 MP4s per ffprobe verifiziert — alle h264 1280×720 @25 fps + AAC 44,1 kHz,
  10–20 s, 156–376 KB (keine Riesen-Binaries); `signalstats` auf `funki-begruessung.mp4`
  (YAVG≈52 dunkler Hintergrund, YMAX=243 — echte bewegte Inhalte, kein Schwarzbild).
  6 Drehbücher (`drehbuecher/01-06`), reproduzierbarer Generator
  (`generator/generate_clips.py`, 476 Zeilen, Pillow→rawvideo→ffmpeg + Sinus-Jingle),
  `README.md` erklärt Figur und Einbindung. Motion-Graphics-Niveau für Kennzeichen-
  Füller ausreichend; Anwender wollte tatsächliche Produktion — geliefert.
- **Doku:** `docs/help/tv-sender.md` (Abschnitt „Maskottchen-/Kennzeichen-Clips" —
  dedizierte Quelle, keine `.nfo`, für alle teilen, Füller-Regel „Clip-Quelle"),
  `docs/RELEASE_NOTES.md` (EN+DE), `README.md`, `handover.md` (Designentscheidung +
  Werkzeug-Evaluierung), `steps.md` → Fertig.
- **Keine Migration/Backup-Änderung nötig:** reine Enum-Erweiterung (int-Werte);
  `ProgramRules`/`ProgramRuleCursors` stehen bereits in `RequiredRestoreTables`.
- **Gegenprobe durchgeführt:** Linked-Item-Filter in `ExpandMediaSourceClipsAsync`
  entfernt → `EnsureCoverage_ClipSource_ExcludesClassifiedItems` schlägt wie erwartet
  fehl (klassifizierte Movie-/Episoden-Items landen im Füller-Ring). Änderung per
  `git checkout --` revertiert, Baum sauber.
- **Build/Format:** `dotnet build VideoPlayer.sln` Debug **und** Release: 0 Fehler,
  jeweils 144 Warnungen — identischer Alt-Bestand (BL0008 Account-Pages, CS8602/CS9113
  in Alt-Tests), keine neuen Warnungen in geänderten Dateien.
  `dotnet format VideoPlayer.sln --verify-no-changes --severity error`: sauber.
- **Testsuite** (`dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`,
  synchron): 2124 bestanden + 1 Alt-Skip
  (`PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`)
  + 1 Fehlschlag `PlaylistSkipLockedEntriesE2ETest` (Playwright-Timeout auf
  `#playlist-detail-selected-entry`, 30 s) — dokumentierter Baseline-Flake, Einzel-Retry
  grün. Ergebnis entspricht exakt dem Implementierer-Bericht (dessen Lauf: identischer
  Flake durchgelaufen, stattdessen eigener E2E-Seed-Fehler gefixt + Retry grün).
  Neue Tests: 8 Schedule (`ProgramScheduleServiceTests_ClipSource`), 4 Regel
  (`ProgramRuleServiceTests_ClipSource`), 1 Guide, 1 bUnit, E2E konsolidiert in
  `Admin_CreateEditDeactivateDeleteRule` (selbe App-Instanz, konform zur
  E2E-Minimierungsregel).

## Abweichungen

- [ ] — keine.

## Hinweise

- **Bewusste Nicht-Umsetzung eines Interstitial-Markers** (im Handover begründet):
  N12 verlangte nur „ggf." eine Kennzeichnung — der gewählte Weg „eigene Quelle ohne
  Einordnung" ist exakt die in N12 selbst skizzierte Variante und minimal-invasiv
  (kein `IsInterstitial`-Flag, das alle Bibliotheksabfragen träfe). Nachvollziehbar.
- **Vorhandene UX-Muster übernommen, nicht neu eingeführt:** `ClearMaterial` setzt
  `MaterialType` nicht zurück (identisch zum Trailer-Pfad; ungültige Kombis fängt die
  Server-Validierung), und die leere Option „Clip-Quelle wählen…" kann eine getroffene
  Auswahl nicht aufheben (identisch zum Playlist-Dropdown). Kein neuer Defekt.
- **Flake ehrlich vermerkt:** `PlaylistSkipLockedEntriesE2ETest` fiel einmal im
  Volllauf (Timeout), Einzel-Retry grün — wie vom Prozess gefordert dokumentiert, kein
  Bezug zur Änderung (Playlist-Detailseite, unverändert).
- **Repos-Hygiene:** Lifecycle-Ordner `docs/features/…-schritt-18-maskottchen/` enthält
  nur ignorierte Test-Logs (inventory/test-results), Tasks-Datei ist committed —
  entspricht der Handover-Aussage.
- `handover.md` kennzeichnet Schritt 18 als „FERTIG (Implementierung; Abnahme durch
  anderen Agenten ausstehend)" — diese Abnahme erfüllt die ausstehende Prüfung.
