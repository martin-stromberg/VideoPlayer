# Abnahme Schritt 17 — Trailer-Verknüpfung + Vorlauf-Füllmaterial (N11)

Prüfer: Devin (unabhängige Abnahme, nicht der Implementierer)
Geprüfter Stand: `14cc4f0` (`feat: Trailer als Vorlauf-Fuellmaterial zu eingeplanten Filmen
(N11, Schritt 17)`), Arbeitsbaum sauber, Probe-Änderungen revertiert.

## Ergebnis

**Status:** Abweichungen gefunden

Kernanforderung ist umgesetzt und durch 12 neue Tests abgesichert: `-trailer`-Suffix und
`trailers/`-Unterordner werden beim Scan erkannt, als `MovieMediaItem.IsTrailer`-Link
persistiert, nie als regulärer Film geplant; die Füller-Materialart „Trailer anstehender
Filme" bevorzugt Trailer, deren Film innerhalb `ProgramSchedule:TrailerLeadDays` (Default 7)
ausgestrahlt wird, mit Fallback auf die übrigen Füller-Regeln. Vier Befunde (einer latent
funktional relevant, drei klein) sind unten als Abweichungen gelistet — keines davon bricht
den Hauptpfad.

### Verifiziert

- **Konventionserkennung** (`MediaSourceClassifier.cs:775-781`): Suffix-Match auf
  `EndsWith("-trailer", OrdinalIgnoreCase)` des Dateinamen-Stamms, Ordner-Match auf
  `MediaCollection.Name == "trailers"` (OrdinalIgnoreCase). Beide Wege führen dazu, dass die
  Datei vor dem NFO-/Movie-Zweig (`:799-803`) und im Episoden-Pfad (`:580`) übersprungen
  wird — ein `Film-trailer.nfo` mit `<movie>`-Inhalt erzeugt ebenfalls keinen Movie-Satz,
  da die Datei vor der NFO-Prüfung ausgefiltert wird. `trailers/` als Root-Collection bleibt
  unverknüpft (kein `Movie`), landet aber auch nicht als Inhalt im Plan.
- **Verknüpfung** (`LinkTrailerMediaItemsAsync`/`EnsureTrailerLinkAsync`, `:988-1044`):
  `-trailer` → Film des stammgleichen Hauptitems über dessen `!IsTrailer`-Link;
  `trailers/`-Kindcollection → alle Filme der Eltern-Collection. Idempotent; Alt-Links ohne
  Flag werden zu `IsTrailer = true` hochgestuft. Verarbeitungsreihenfolge Eltern↔Kind ist
  doppelt abgedeckt (Schritt 4 + Schritt 5 in `ProcessCollectionAsMovieAsync`).
- **`!IsTrailer` konsistent an allen Hauptdatei-Konsumenten:** `MovieMediaItemsAsync`
  (`ProgramScheduleService.cs:1000` — damit Film-/Filmsammlungs-/Genre-Material),
  `ItemsController.FindMediaItemAsync` Stream/Download (`:565`),
  `AssignPicturesToMovieAsync` (`Classifier:1050`), `BackfillMissingActorsAsync`
  (`Classifier:1770`). Film ohne Nicht-Trailer-Link wird in `TryMovieItem` als
  „ohne Mediendatei" übersprungen — Trailer-Hauptdatei ausgeschlossen.
  `GetRelationsForMediaItemAsync` löst Trailer-Item → Film auf (nur Favorite-Lookup,
  Richtung unproblematisch).
- **Vorlauf-Logik:** Verzögerte Expansion in `EnsureCoverageAsync` (Trailer-Regeln starten
  mit leerer Itemliste, `ExpandTrailerStatesAsync` nach der Occurrence-Materialisierung —
  frisch eingeplante Filmstarts zählen). `FindLeadMatchOffset` akzeptiert nur Airings in
  `(fillAt, fillAt + TrailerLeadDays]`; bei Nicht-Treffer `continue` → andere Füller füllen
  (Fallback). Trailer-Slots tragen `MovieId`, sind aber per `trailerItemIds` aus
  `BuildMovieAiringsMap` ausgeschlossen — kein Selbstbezug. `TrailerLeadDays <= 0` →
  Default 7 (`EffectiveTrailerLead`), Options-Bindung `ProgramSchedule` registriert,
  `appsettings.json` enthält den Schlüssel.
- **Reconcile-Konsistenz (Füller-Pfad):** Trailer-Füllslots werden einzeln gegen die mit
  persistierten Airings expandierte Itemliste geprüft, bleiben erhalten (`Reconcile_KeptTrailerSlot_StaysScheduled`).
- **Migration + Backup:** `AddMovieMediaItemIsTrailer` (bool, `defaultValue: false`, Down
  sauber), Snapshot aktualisiert; `MovieMediaItems.IsTrailer` in `OptionalRestoreColumns`
  + `OptionalRestoreBoolDefaults(false)`; Legacy-Backup-Regressionstest
  (`RemoveColumnsAsync` ohne Spalte → Restore ok, `IsTrailer = false`) grün.
- **Materialart/Editor:** `ProgramRuleMaterialType.Trailer = 5` nur explizit in der
  Filler-Zulassungsliste; Validation `Trailer => true` (keine Entität, `MaterialId = 0`);
  Checkbox `rule-material-trailer` setzt Typ + `MaterialId = 0` + räumt Genre-/Quellenwahl,
  `MaterialLabel` deckt Trailer ab; `ProgramCursorContentType.Trailer = 3` additiv.
- **Gegenproben (Mutanten getötet, beide revertiert):**
  1. `IsTrailerFileName` auf `-trailerx` gesetzt →
     `ClassifyAllAsync_TrailerSuffix_LinksToMovie_NotAsMovie` schlägt fehl (1 statt 2 Links).
  2. `s > fillAt` aus `FindLeadMatchOffset` entfernt →
     `EnsureCoverage_BeyondLeadWindow_FallsBackToNormalFiller` schlägt fehl (Trailer laufen
     nach der letzten Ausstrahlung weiter), plus `…_AirsTrailerBeforeScheduledMovie`.
     Anschließend `git checkout`, Baum sauber, Trailer-Filterlauf: 12/12 grün.
- **Builds:** `dotnet build VideoPlayer.sln` Debug **und** Release: 0 Fehler. Kein
  Datei-Lock aufgetreten.
- **Suite:** `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Debug),
  synchron bis zum Ende: **2111 gesamt — 2109 grün, 1 Alt-Skip, 1 Fehlschlag**:
  `PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest` (Playwright-Timeout auf
  `#playlist-detail-selected-entry`) — bei isolierter Einzelwiederholung grün (10 s),
  feature-unabhängiger Flake.
- **Format:** `dotnet format VideoPlayer.sln --verify-no-changes --severity error` auf den
  geänderten Dateien: sauber (CI-Schwelle).

## Abweichungen

- [ ] **Host-Lookup des `-trailer`-Suffix ist reihenfolgeabhängig und kann auf Nicht-Video-Items
  treffen** (`MediaSourceClassifier.cs:1009-1018`): `siblings.FirstOrDefault(mi =>
  GetFileNameWithoutExtension(mi.Path) == stem)` durchsucht **alle** MediaItems der
  Collection — der Scanner legt jeden nicht-punktierten Dateieintrag als MediaItem an, also
  auch `Film.nfo` (ohnehin Pflicht für die Film-Erkennung), `Film.jpg`, `Film.srt` usw.
  Steht ein stammgleicher Sidecar vor der Videodatei in der (ungeordneten) DB-Liste, wird er
  als „host" gewählt, die `MovieMediaItems`-Abfrage greift ins Leere und der Trailer wird
  **still nie verknüpft** — kein Log, kein Fehler. Realistisch: `Film.jpg`/`Film.nfo` neben
  `Film.mkv` ist die übliche Plex/Jellyfin-Belegung; Reihenfolge = Scan-Einfügereihenfolge.
  Erwartet wäre eine Einschränkung auf Video-Extensions bzw. auf Items mit `!IsTrailer`-Link.
  Kein Test deckt den Sidecar-Fall ab (die Tests seeden nur Video-Items).
- [ ] **`ProgramRuleMaterialType.Trailer` ist über `AllMaterialTypes` auch für
  `Repeat`/`HolidaySpecial` erlaubt und verhält sich pfadinkonsistent:**
  `AllMaterialTypes = Enum.GetValues<ProgramRuleMaterialType>()` zieht den neuen Wert
  automatisch in beide Regelarten (`ProgramRuleService.cs:13-14,39-40`); die Admin-Checkbox
  wird dort ebenfalls angeboten (`AllowedMaterialTypes.Contains(Trailer)`).
  `EnsureCoverageAsync` expandiert Trailer-Regeln aber verzögert mit leerer Liste →
  Occurrences materialisieren **nichts** (stille Tot-Regel); `ReconcileAsync` expandiert
  dieselbe Regel dagegen eager mit persistierten Airings → sie produziert reguläre
  Programmplätze. Gleiche Regel, zwei verschiedene Ergebnisse je nachdem, welcher Lauf
  zuletzt lief — zusätzlich widerspricht es der Hilfe („für Füller-Regeln", tv-sender.md)
  und der N11-Intention (Füller-Option). Naheliegend: `AllMaterialTypes` um Trailer
  bereinigen oder Trailer explizit aus Nicht-Filler-Listen herausnehmen.
- [ ] **Zwei neue Compiler-Warnungen** `CS1572` `MediaSourceClassifier.cs:768-769`: Der
  XML-Doc-Kommentar von `ProcessCollectionAsMovieAsync` wurde durch den eingeschobenen
  Konstanten-/Helper-Block von der Methode getrennt und hängt jetzt vor
  `TrailerFolderName`. Build zählt 145 statt der in der Schritt-16-Abnahme dokumentierten
  142 Warnungen (die dritte Differenz liegt in keiner geänderten Datei —
  Zähl-/Bestandsdelta; die anderen Warnungen in geänderten Dateien sind Altbestand).
- [ ] **`plan-check.md` behauptet E2E-Abdeckung der Trailer-Checkbox über
  `TVSenderAdminE2ETests` — es existiert kein UI-Test:** weder `TVSenderAdminE2ETests.cs`
  noch `Components/TVSenderAdminTests.cs` enthalten eine Trailer-Assertion. Die
  Checkbox-Logik ist nur indirekt über die Service-Tests (`GetAllowedMaterialTypes`,
  Create/Validate) gedeckt. Klein, aber der Plan-/Abnahmenachweis stimmt so nicht.

## Hinweise

- **Flat-Layout-Cross-Linking:** In einer Collection mit mehreren Filmen verlinkt ein
  gemeinsamer `trailers/`-Unterordner jede Trailer-Datei auf **alle** Filme der
  Eltern-Collection (`LinkTrailerMediaItemsAsync`, `IsTrailerFolder`-Zweig). Bei der
  klassischen Ein-Film-Ordner-Konvention korrekt; in flachen Multi-Film-Ordnern kann ein
  fremder Trailer als Vorlauf eines anderen Films laufen. Als Konventions-Ermessensentscheidung
  vertretbar, aber unerwähnt in Doku/Hilfe.
- **Kein Downgrade-Pfad:** Wird eine Datei umbenannt (Suffix entfernt), bleibt ein
  existierender `IsTrailer`-Link bestehen — Upgrade ist implementiert, Downgrade nicht.
  Randfall bei Umbenennung ohne Neu-Scan-Zuordnung.
- `appsettings.json` wurde im Diff zusätzlich umformatiert (Serilog-Arrays) — harmlos,
  erwähnenswert wegen Diff-Rauschens.
- `Movie.MediaItems` (EF-ignorierte Convenience-Property) liefert weiterhin inkl. Trailer —
  aktuell kein Konsument.
- Der ausstehende `nfo`-Sidecar-Befund (erste Abweichung) wurde statisch begründet, nicht
  per Lauf belegt — die betroffene Codezeile ist eindeutig, der Effekt hängt an der
  MediaItem-Einfügereihenfolge.
- Suite-Gesamtzahl 2111 = 2097 (Schritt-16-Abnahme) + 12 neue + 2 umbenannte/angepasste —
  konsistent mit dem Implementierer-Bericht (~2110).
