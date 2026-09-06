# Blockade – Entwicklungsschritt 3

Erstellt am: 2026-09-06
Branch: `task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-3-automatische-sortierung-anzeige`
Grund: Maximale Anzahl an Nachbesserungsrunden erreicht

Der Schritt konnte nicht automatisiert abgeschlossen werden. In drei aufeinanderfolgenden
Abnahmeprüfungsrunden wurden jeweils neue Abweichungen gefunden und behoben (Runde 1: fehlendes
Titelbild, Freischaltungsprüfung nur Platzhalter, `disabled` am falschen Button; Runde 2:
Freischaltungsprüfung lieferte für Film/Staffel/Episode immer `false`). Die dritte und letzte
zulässige Prüfrunde hat eine weitere, verbleibende Abweichung gefunden:

## Verbleibende Abweichung (Runde 3)

**Id-Kollision in der Freischaltungsprüfung führt dazu, dass nicht freigeschaltete Titel
fälschlich als freigeschaltet angezeigt werden.**

In `VideoWebPlayer/Services/PlaylistService.cs`, `CheckEntryAccessible` (Zeilen 805–817), wird die
über `ResolveUnlockMediaIdAsync`/`LoadUnlockHierarchyMappingsAsync` ermittelte Freischaltungs-Id
undifferenziert gegen die **Vereinigung** beider Id-Räume geprüft:

```csharp
var isUnlocked = unlockedUnlockId.HasValue &&
    (unlockedMovieCollectionIds.Contains(unlockedUnlockId.Value) || unlockedTVShowIds.Contains(unlockedUnlockId.Value));
```

Da `MovieCollections` und `TVShows` getrennte Tabellen mit je eigener, bei 1 beginnender
Id-Vergabe sind, überlappen sich die Id-Bereiche vollständig. Konkrete Folge: Hat ein Anwender
z. B. die Serie mit Id 12 freigeschaltet, werden in der Playlist auch alle Filme der
Filmsammlung mit Id 12 als freigeschaltet (nicht ausgegraut) dargestellt, obwohl dafür keine
Freischaltung besteht und kein Quellenzugriff vorliegt — und umgekehrt für Staffeln/Episoden.
Dies tritt in jeder Installation mit Filmen und Serien auf, sobald Einzelfreischaltungen
vergeben sind.

Die referenzierte, korrekte Implementierung in `ItemsController.IsUnlockedAsync`
(`VideoWebPlayer/Controllers/ItemsController.cs`, Zeilen 372–387) trennt die Id-Räume korrekt
(Film nur gegen `MovieCollectionId`, Episode/Staffel nur gegen `TVShowId`). Die
Hierarchie-Auflösung selbst (`LoadUnlockHierarchyMappingsAsync`) ist bereits korrekt — es fehlt
lediglich die Zuordnung, aus welchem Id-Raum die aufgelöste Id stammt, bei der abschließenden
Prüfung.

Vollständiger Bericht: `docs/projects/task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln/acceptance-schritt-3.md`
(sowie die archivierten Vorrunden `acceptance-schritt-3.1.md`, `acceptance-schritt-3.2.md`).

## Einschätzung

Der Fehler betrifft ausschließlich die Anzeige (Ausgrauen) in Schritt 3; die zugrunde liegende
Zugriffskontrolle beim tatsächlichen Abspielen (`ItemsController`) ist davon nicht betroffen und
bleibt korrekt. Die Anforderung „Titel, für die der Anwender keine Freischaltung besitzt, werden
ausgegraut dargestellt" ist dadurch aber nicht vollständig erfüllt. Der Fix selbst ist klein und
lokal begrenzt (Zuordnung der aufgelösten Id zum korrekten Id-Raum statt Vereinigung beider
Mengen), erfordert aber laut Ablaufregel eine menschliche Entscheidung, da die maximale Anzahl
automatisierter Nachbesserungsrunden erreicht ist.

Der Schritt konnte nicht automatisiert abgeschlossen werden. Bitte klären und anschließend
bestätigen, damit die Abarbeitung fortgesetzt werden kann.
