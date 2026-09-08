# Bestandsaufnahme: Korrektur der Medienauswahl-Suche für Playlists

Diese Bestandsaufnahme analysiert den bestehenden Projektcode bezüglich der Anforderung zur Korrektur zweier kritischer Fehler bei der Playlist-Medienauswahl:
1. Case-sensitive Namenssuche blockiert Benutzer
2. Erweiterung um neue Medientypen hat das Quellen-Browsing unbeabsichtigt verändert

## Zusammenfassung

### Wesentliche Befunde

**Betroffene Komponenten:**
- Server-seitig: `ItemsController.cs` (zentral) und seine fünf `Get*EntriesAsync()`-Hilfsmethoden
- Client-seitig: `VideoWebPlayerClient.cs` (RequestItemsCoreAsync) und `MediaSearchSelector.razor`
- ViewModel: `MediaSourceDetailsViewModel.cs`

**Problem 1: Case-sensitive Suche**
- Alle fünf `Get*EntriesAsync()`-Methoden verwenden `.Contains(filter.Search)`, was vom SQLite EF Core Provider zu case-sensitiver `instr()`-Funktion übersetzt wird
- Eine Suche nach "breaking bad" findet kein Element "Breaking Bad"
- **Keine Tests vorhanden**, die abweichende Groß-/Kleinschreibung abdecken

**Problem 2: Unbeabsichtigte Vergrößerung des Quellen-Browsing**
- `ItemsController.Get()` ruft alle 5 Hilfsmethoden immer auf
- Beim Quellen-Browsing (mit `mediaSourceId` gesetzt) werden jetzt 5 statt ursprünglich 2 Medientypen zurückgegeben
- **Keine Tests vorhanden**, die das Quellen-Browsing-Verhalten explizit absichern

**Anforderung Implementierungslösung (Empfehlung: Option A)**
- Opt-in-Parameter `includeIndividualMediaTypes` (boolean, Standard: false)
- Nur wenn dieser Parameter true ist, werden die 3 neuen Typen (Movie, TVShowSeason, TVShowEpisode) abgerufen
- `MediaSearchSelector` setzt den Parameter auf true, Quellen-Browsing nicht

### Test-Ausgangszustand

**Aktuelle Test-Ergebnisse (vor Umsetzung der Anforderung):**
- **Zeitpunkt:** 2026-09-08 08:20 UTC+2
- **Commit:** e185cdf (feat: Namenssuche statt Medien-Id beim Hinzufuegen zu Playlists)
- **Testergebnis:** ✅ **Alle 480 Tests bestanden** (0 Fehler, 3m 20s Laufzeit)
- **Testabdeckung existierender Funktionalität:** Vollständig

**Bekannte Testlücken zur Anforderung:**
- Keine Tests für case-insensitive Suche (verschiedene Groß-/Kleinschreibung)
- Keine Tests für Quellen-Browsing-Regression (sollte nur 2 Typen, nicht 5 zurückgeben)
- Kein Test für Opt-in-Parameter (Parameter existiert noch nicht)

Siehe [tests.md](inventory/tests.md) für vollständige Test-Analyse und Nachweis.

## Details

Ausführliche Code-Analysen:

- [Datenmodelle](inventory/models.md) — DTOs und Datenbank-Modelle
- [Logik](inventory/logic.md) — Controller, Clients, ViewModels und deren Methoden
- [Tests](inventory/tests.md) — Testklassen, Hilfsmethoden und Testausgangszustand

## Schlüsselerkenntnnisse für die Implementierung

1. **Case-insensitive Suche:** Muss in allen 5 `Get*EntriesAsync()`-Methoden (Zeilen 188, 226, 259, 292, 325 in ItemsController.cs) einheitlich angepasst werden. Technik muss mit SQLite getestet werden (Option: `.ToLower().Contains()` oder `EF.Functions.Like`).

2. **Opt-in-Parameter:** 
   - Hinzufügen zum `MediaEntryFilter` Record (Zeile 179)
   - Propagieren durch `RequestItemsCoreAsync` (Zeile 455)
   - Setzen in `MediaSearchSelector.razor` (Zeile 105)
   - Nicht setzen in `MediaSourceDetailsViewModel.LoadNextPageAsync` (Zeile 100)

3. **Regressionstests erforderlich:**
   - Case-insensitive Suche mit verschiedener Groß-/Kleinschreibung
   - Quellen-Browsing mit `mediaSourceId` + ohne Opt-in → nur 2 Typen
   - Quellen-Browsing mit `mediaSourceId` + mit Opt-in → alle 5 Typen
   - Playlist-Suche ohne `mediaSourceId` → alle 5 Typen (unabhängig von Parameter)

4. **Aktuelle Tests:** Alle 480 Tests bestanden; sind aber nicht ausreichend für die geplanten Änderungen (kein Regression-Schutz für die neuen Use Cases).
