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
