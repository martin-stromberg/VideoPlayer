# Nachbesserungen / Rückmeldungen — Automatisierter TV-Sender

Gesammelte Verbesserungswünsche aus der Nutzung der Testversion.
Status: **offen**, noch nicht geplant/umgesetzt.

## N1 — Mehrfachauswahl bei Genres (Priorität: hoch)

**Beobachtet:** Im Regel-Formular (`TVSenderAdmin.razor`, Material-Dropdown `rule-material-genre`)
ist pro Regel genau **ein** Genre als Programmmaterial wählbar (`ProgramRule.MaterialId` =
eine Genre-ID).

**Gewünscht:** Bei allen Regelarten, die Genre-Material erlauben (Genre-/Formatrotation,
Wiederholung, Feiertags-Special, Platzhalter/Füller), muss eine **Mehrfachauswahl von Genres**
möglich sein — z. B. „Familie + Animation" gemeinsam in einer Rotation.

**Betroffene Stellen:**
- Datenmodell `ProgramRule`: `MaterialType`/`MaterialId` (1:n → Join-Tabelle `ProgramRuleGenre`
  oder vergleichbar), EF-Konfiguration, Migration, `OptionalRestoreTables`/`OptionalRestoreColumns`
- `ProgramRuleService` (Validierung, Input-Modell, `GetAllowedMaterialTypes`)
- `TVSenderAdmin.razor` (Mehrfach-Auswahldialog statt Einfach-Dropdown)
- `ProgramScheduleService.ExpandGenreAsync` (Vereinigung mehrerer Genres, Duplikat-Behandlung,
  Cursor-/Rotationssemantik über die Vereinigungsmenge)
- Tests + Abnahme analog den bisherigen Schritten

## N2 — Quell-Filter bei Genre-Material (Priorität: hoch)

**Beobachtet:** Bei Genre-Auswahl werden implizit **alle Medienquellen** berücksichtigt —
die Episoden/Filme des Genres aus sämtlichen eingerichteten Quellen.

**Gewünscht:** Bei Genre-Material muss (ebenfalls per **Mehrfachauswahl**) festgelegt werden
können, **welche Quellen (`MediaSource`)** für diese Regel betrachtet werden. Beispiel:
„Genre Familie, aber nur aus Quelle A und B, nicht aus dem Archiv-Server."

**Betroffene Stellen:**
- Datenmodell: Quell-Filter je Regel (Join-Tabelle `ProgramRuleSources` oder an die
  Genre-Verknüpfung gekoppelt — Designentscheidung bei Umsetzung klären: Filter pro Regel
  oder pro Genre-Eintrag)
- `TVSenderAdmin.razor`: zweite Mehrfach-Auswahl (Quellen-Liste) bei Genre-Material
- `ProgramScheduleService.ExpandGenreAsync` + ggf. weitere Expand-Pfade: Items auf die
  gewählten `MediaSourceId`s einschränken (siehe `MediaItem`/`TVShowEpisode` → Quellzuordnung)
- Interaktion mit Freigabe-Filter (`IUnlockedMediaService`): Quell-Filter zusätzlich,
  nicht ersetzend
- Sinnvoller Default: **keine Auswahl = alle Quellen** (Abwärtskompatibilität zu N1/N2
  für bestehende Regeln — Migration mit leerem Filter = uneingeschränkt)

## Hinweise zur Einordnung

- Beide Punkte betreffen das **gleiche Formularfeld** (Programmmaterial bei Genre) und das
  **gleiche Datenmodell** (`ProgramRule.MaterialId` als Einzelwert) — sinnvoll gemeinsam
  in einem Schritt umzusetzen, da sonst doppelte Migrationen/Formular-Umbauten anfallen.
- Offene Designfrage bei Umsetzung: gilt die Quell-Differenzierung nur für Genre-Material
  oder generell pro Regel (auch Serie/Film/Sammlung/Playlist)? Anforderung spricht zunächst
  nur von Genres.

## N3 — Genre-Auswahl berücksichtigt Status und Alternativnamen (Priorität: hoch)

**Beobachtet:** Das Genre-Dropdown im Regel-Formular wird über `api/items/genres`
(`ItemsController` → `MediaMetadataEditorService.GetGenreOptionsAsync`) befüllt. Diese
Methode gruppiert **alle** Genres ausschließlich nach Roh-`Name` — weder `Genre.IsHidden`
(Status „Ausgeblendet") noch `Genre.AlternateNames` werden berücksichtigt.

**Gewünscht:** Die Genre-Auswahl muss denselben Status-/Alternativnamen-Regeln folgen wie
der Rest der Anwendung (vgl. `GenreAdmin.razor`, `GetOrCreateGenresAsync` in
`MediaSourceClassifier`, `SourceGenresController` mit `Where(g => !g.IsHidden)`):

- Ein Genre mit Status **„Ausgeblendet"** (`IsHidden = true`) darf nicht unter seinem
  eigenen Namen wählbar sein.
- Hat das ausgeblendete Genre **Alternativnamen**, gilt die Auswahl dem Ziel-Genre dieser
  Umleitung. Beispiel aus der Praxis: Genre „Family" ist ausgeblendet und hat den
  Alternativnamen „Familie" → in der Auswahl darf **nur „Familie"** stehen (Auswahl des
  kanonischen Genres), nicht „Family".
- Angezeigt wird also die Menge der **sichtbaren, kanonischen Genres** nach Auflösung der
  Alternativnamen — analog der Scan-Logik, die „Family" beim Einlesen auf „Familie" umlenkt.

**Betroffene Stellen:**
- `MediaMetadataEditorService.GetGenreOptionsAsync` (bzw. ggf. eine eigene,
  TV-Sender-spezifische Optionsquelle — Achtung: derselbe Endpoint wird auch vom
  Metadaten-Editor genutzt; Änderung dort absichtlich oder separat entwerfen)
- Alternativnamen-Auflösung beim Mapping `MaterialId` → Inhalte: Der Scanner verknüpft
  Inhalte über die Umleitung mit dem kanonischen Genre, **Altbestand** kann aber noch
  `MovieGenre`/`TVShowGenre`-Zeilen auf das ausgeblendete Genre „Family" besitzen —
  `ExpandGenreAsync` sollte Inhalte auflösungskonform einschließen (inkl. verknüpfter
  Alt-Zuordnungen auf die Alternativnamen des gewählten Genres bzw. umgekehrt)
- Zusammenspiel mit **N1** (Mehrfachauswahl: die Liste der kanonischen Genres) und **N2**
  (Genres sind quellbezogen: `Genre.MediaSourceId` — die Dedupe nach Name über Quellen
  hinweg muss mit dem Quell-Filter konsistent bleiben)
- `Genre.StartDate`/`EndDate`-Sichtbarkeitsfenster analog mitbehandeln (zeitweise
  ausgeblendete Genres wie in der Genre-Verwaltung)
- Tests: verstecktes Genre mit Alternativname → nur Ziel-Genre wählbar; verstecktes Genre
  ohne Alternativname → nicht wählbar; zeitlich eingeschränktes Genre → Status gemäß
  Sichtbarkeitsfenster
