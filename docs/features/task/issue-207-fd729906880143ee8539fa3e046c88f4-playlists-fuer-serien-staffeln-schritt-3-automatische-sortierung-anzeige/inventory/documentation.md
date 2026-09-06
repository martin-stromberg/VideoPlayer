# Dokumentation

## docs/help/playlists-api.md

Datei: `docs/help/playlists-api.md`

### Fehlerhafte Dokumentation von IsAccessible

**Ort:** Zeilen 301–307 im Abschnitt "DTO-Modelle" → "DtoPlaylistEntry" → "Hinweis zu `IsAccessible`"

**Fehlerhafte Aussage:**
```markdown
**Hinweis zu `IsAccessible`:** Gibt an, ob der aktuell angemeldete Anwender den referenzierten
Medieninhalt freigeschaltet hat. Die Prüfung erfolgt über denselben Freischaltungsdienst wie bei
Einzelfreischaltungen (siehe `einzelfreischaltungen.md`) und berücksichtigt ausschließlich die
Medientypen `TVShow` und `MovieCollection`, da nur diese einzeln freigeschaltet werden können —
Einträge der Typen `Movie`, `TVShowSeason` und `TVShowEpisode` liefern daher immer `false`.
```

**Problem:**
1. Sagt, dass Movie, TVShowSeason und TVShowEpisode immer `false` liefern — das ist die AKTUELLE FEHLERHAFTE Implementierung, nicht das beabsichtigte Verhalten.
2. Erwähnt nicht den regulären Quellenzugriff (`MediaSourceUsers`), der ebenfalls relevant sein sollte.
3. Die Referenzimplementierung in `ItemsController.cs` zeigt die korrekte Regel: `hasSourceAccess OR isUnlocked`

### Weitere Dokumentation mit Kontext

**Dokumentierte Verben zu Zugriff:** Die Dokumentation bemerkt im Kontext der Endpunkte, dass nur der Besitzer einer Playlist diese ändern darf. Es ist nicht klar, ob der Zugriff auf einzelne Einträge durch die Quelle oder Freischaltung geregelt ist.

---

## Korrekte Dokumentation (Referenz)

### docs/help/einzelfreischaltungen.md (impliziert)

Basierend auf der Nutzung in der API-Dokumentation gibt es ein paralleles System für Einzelfreischaltungen für TVShow und MovieCollection. Das wird erwähnt, aber nicht direkt verlinkt oder erläutert.

---

## Verknüpfung zur Anforderung

Die Anforderung (in `requirement.md`) macht klar, dass:

1. Die Regel `isAccessible = hasSourceAccess OR isUnlocked` für ALLE fünf Medientypen gelten soll (nicht nur TVShow und MovieCollection).
2. Die hierarchische Auflösung (Movie → Collection, Episode → Season → Show) für die Unlock-Prüfung angewendet werden soll.
3. Diese Logik bereits korrekt in `ItemsController.cs` implementiert ist und als Referenz dient.

---

## Zusammenfassung der Dokumentations-Fehler

| Fehler | Ort | Auswirkung |
|--------|-----|-----------|
| IsAccessible: "Einträge der Typen `Movie`, `TVShowSeason` und `TVShowEpisode` liefern daher immer `false`" | Zeile 305 | Dokumentiert das fehlerhafte Verhalten als beabsichtigt |
| Keine Erwähnung von `MediaSourceUsers` | Zeile 301–307 | Nicht klar, dass regulärer Quellenzugriff ein Zugriffskriterium ist |
| Keine Erwähnung der Hierarchie-Auflösung | Zeile 301–307 | Nicht dokumentiert, wie Movie, Episode, Season behandelt werden sollten |
| Keine Verknüpfung zu `EnsureAccessAsync` in ItemsController | Gesamt | Keine Klarheit über die Referenzimplementierung |

Diese Dokumentation muss nach der Implementierungskorrektur aktualisiert werden, um die korrekte Logik zu reflektieren.
