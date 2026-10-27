# Übersetzte Anforderung: Gleichverteilte Materialauswahl im Sendeplan (Issue #297)

Quelle: `issue.md` (Issue #297: „TV-Sender: Sendeplan enthält verdächtig viele Titel mit Anfangsbuchstabe A — Zufälligkeit der Auswahl fraglich").

## Fachliche Zusammenfassung

Die Material-Expansion des `ProgramScheduleService` baut die `SchedulableItem`-Ringe der ungeordneten Materialarten in einer festen Sortierreihenfolge (Alphabet, Id oder Releasedatum) statt in einer verteilten Ordnung. Da der Rotations-Cursor (`ProgramRuleCursor` via `NextIndexFor`) bei jedem Lauf ohne passenden Cursor-Anker — erstes Generieren, veränderte Materialliste, weggefallenes Cursor-Element (`FindIndex` < 0 → Index 0) — wieder am Ringanfang startet und die Lücken-/Clip-Füllung stets den Ringkopf zieht, werden die frühen Listenelemente systematisch häufiger eingeplant. Das erzeugt den beobachteten Bias zugunsten alphabetisch/früh sortierter Titel. Zu ändern ist die Reihenfolge, in der Kandidaten ungeordneter Materialarten in Ring/Strang aufgenommen werden, sodass die Auswahl über Generierungsläufe annähernd gleichverteilt ist — ohne die fachlich geordneten Abspielreihenfolgen (Serienepisoden, manuell sortierte Playlists) anzutasten.

## Betroffene Klassen und Komponenten

- **Logik/Service:** `VideoWebPlayer/Services/ProgramScheduleService.cs`
  - `ExpandMaterialAsync` bzw. die Einzel-Expansionen: `ExpandMovieCollectionAsync` (sortiert `ReleaseDate ?? PremieredAt`, `Id`), `ExpandGenreAsync` (Filme `OrderBy(ReleaseDate ?? PremieredAt).ThenBy(Name).ThenBy(Id)` → gemeinsamer `OtherStrandKey`-„other"-Strang; Episoden je `show:{TVShowId}`-Strang), `ExpandPlaylistAsync` (sortiert nach `PlaylistSortMode`), `ExpandMediaSourceClipsAsync` und `ExpandClipGroupItemsAsync` über `FilterClipCandidatesAsync` (`OrderBy(MediaItemId)`), ggf. `ExpandTrailersAsync`.
  - `RuleState.SetItems` / `StrandState` (Aufbau der Ringe, `PreservesOrder`-Unterscheidung zwischen geordneten Strängen — `TVShowEpisode`, `PlaylistEntry` — und ungeordneten Ringen), `NextIndexFor` (Cursor-Ankerung), `NextClipItem`/`FindLeadMatchOffset` (Ziehen des Ringkopfs).
  - `StrandOrderKey` (FNV-1a) als vorhandenes Muster für eine *deterministische* Mischung — relevant, weil die Reproduzierbarkeitsprüfung (`ReproContext`, Reconcile-Pfad) prozess- und laufinvariante Ordnungen verlangt.
- **Datenmodell:** voraussichtlich unverändert (`ProgramRuleCursor` referenziert Elemente über `LastContentType`/`LastContentId`, nicht über Positionen).
- **Tests:** `VideoWebPlayer.Tests` — neue oder erweiterte Testklasse neben `ProgramScheduleServiceTests_Generation`/`_Genres`/`_Interleaving`; Testfixture `ProgramScheduleTestDb` (echtes SQLite, in-memory shared) bereits vorhanden.

## Implementierungsansatz

- Die ungeordneten Ringe (Filme einer `MovieCollection`, der „other"-Strang einer `Genre`-Expansion, Clips aus `MediaSourceClips`/`ProgramClipGroup`, ggf. Trailer) erhalten eine deterministisch gemischte Reihenfolge — naheliegend über denselben Mechanismus wie `StrandOrderKey` (FNV-1a über stabile Schlüssel wie `Rule.Id` + Inhaltsschlüssel), damit Pläne und Cursor laufinvariant und reproduzierbar bleiben; `string.GetHashCode`/`Random` scheiden aus (prozesszufällig).
- Geordnete Stränge bleiben unverändert: `ExpandShowAsync` (Sendedreihenfolge Staffel/`Number`) und die `show:{id}`-Stränge von `ExpandGenreAsync`; die `PreservesOrder`-Logik ist die vorhandene Unterscheidungsstelle.
- Zu prüfen, wo die Mischung ansetzt: in den Expand-Methoden oder zentral beim Ringaufbau in `RuleState.SetItems`/`ClipGroupState.SetItems` für nicht-`PreservesOrder`-Ringe.
- Abhängigkeiten: `NextIndexFor` muss den Cursor-Anker in der gemischten Liste weiter finden; die Reproduzierbarkeits-/Reconcile-Prüfung darf durch die neue Ordnung nicht brechen (gleiche Ordnung bei jeder Expansion desselben Materials).

## Akzeptanzkriterien (aus der Anforderung abgeleitet)

1. **Annähernde Gleichverteilung:** Ein Test gegen echtes SQLite (`ProgramScheduleTestDb`) weist über mehrere Generierungsläufe eine annähernd gleichverteilte Auswahl aus dem Materialpool nach — kein systematischer Alphabet-/Id-Bias in den erzeugten `ProgramSlot`s.
2. **Geordnete Reihenfolgen bleiben:** Serienepisoden laufen weiter in Sendedreihenfolge; bestehende Interleaving- und Fortsetzungstests bleiben grün.
3. **Reproduzierbarkeit:** Die gemischte Ordnung ist prozess- und laufinvariant; Cursor-Fortsetzung (`NextIndexFor`) und Reconcile-/Reproduzierbarkeitsprüfung funktionieren unverändert.

## Konfiguration

Keine — das korrigierte Auswahlverhalten soll fachlich immer gelten; eine Schaltbarkeit ist aus der Anforderung nicht ableitbar.

## Offene Fragen

- Gilt die Gleichverteilungserwartung auch für `Playlist`-Material mit `PlaylistSortMode.Manual` (manuell festgelegte Abspielreihenfolge) bzw. `ReleaseDate`-Sortierung, oder ist die Playlist-Reihenfolge bewusst gewollt? Annahme: Playlist-/Serienreihenfolge bleibt erhalten, nur ungeordnete Ringe werden gemischt.
- Bezugsgröße der Verteilung: Titel-Ebene (Film/Serie) oder `MediaItem`-Ebene? Annahme: Titel-Ebene, wie vom Kunden beobachtet.
- Toleranz/Stichprobengröße der Verteilungsprüfung (Anzahl Generierungsläufe, zulässige Abweichung) ist festzulegen.
