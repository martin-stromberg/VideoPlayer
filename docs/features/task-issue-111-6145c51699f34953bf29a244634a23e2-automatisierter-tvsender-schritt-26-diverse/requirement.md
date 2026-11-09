# Anforderung: Schritt 26 — Diverse Nachbesserungen (N23–N25)

Quelle: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/nachbesserungen.md` §N23–§N25.
Drei unabhängige Rückmeldungen — jeweils eigener Commit.

## N23 — Serien-Auswahl wirkt nicht zufällig („A"-Häufung)

### Fachliche Zusammenfassung
Im erzeugten Sendeplan häufen sich Serien mit frühem Anfangsbuchstaben („A"). Die Material-Expansion
(`ExpandGenreMaterial` / Strand-Bildung in `ProgramScheduleService`/`MediaQueryService`) sortiert das
Kandidatenmaterial vermutlich deterministisch (Titel/Added), sodass die vorderen Positionen immer an
alphabetisch frühe Serien gehen. Gefordert: eine **stabile, aber nicht alphabetisch voreingenommene
Mischung** — z. B. deterministischer Hash-Mix je Regel/Strang oder Rotation über den gesamten Bestand
statt ab Listenanfang.

### Betroffene Klassen und Komponenten
- `ProgramScheduleService` (Strand-Bildung, `SelectAiringItemsInterleaved`, Material-Expansion)
- `MediaQueryService` (Sortierung des Kandidatenmaterials)
- Tests: bestehende Schedule-/Strand-Tests + neue Tests mit mehreren Serien (vorderste Position
  variiert über Regeln/Occurrences, bleibt aber reproduzierbar)

### Implementierungsansatz
Deterministische, aber gleichverteilte Auswahl: z. B. stabiler Hash aus (Regel-/Strang-Schlüssel,
Item-Schlüssel) als Sortierschlüssel, oder Rotations-Offset über den Bestand. Wichtig: Ergebnis muss
reproduzierbar bleiben (gleiche Eingabe → gleicher Plan), damit bestehende Stabilitätsgarantien
(Schritt 21/22, Eviction) nicht brechen.

## N24 — „Die nächsten Stunden" aktualisiert nicht im Verlauf

### Fachliche Zusammenfassung
Die Upcoming-Liste auf `LiveTv.razor` (~Z. 100–113, `upcoming`, `PollInterval=15s`) veraltet während
der Wiedergabe: abgelaufene Einträge bleiben stehen, neue kommen nicht nach. Zu diagnostizieren: läuft
der Poll wirklich, wird `upcoming` neu befüllt (auch bei Slot-Wechsel/Stream-Recovery)? Fix + bUnit-Test.

### Betroffene Klassen und Komponenten
- `LiveTv.razor` (Poll-Timer, `upcoming`-Liste)
- ggf. `ProgramGuideController`/`IProgramGuideService` (Datenquelle)
- bUnit-Test(s) für die Refresh-Logik

## N25 — TV-Sender admin-freischaltbar

### Fachliche Zusammenfassung
Der TV-Sender soll per Admin-Freigabe aktivierbar sein. Ohne Freigabe: kein NavMenu-Eintrag, alle
Sender-Endpunkte (`/api/live-stream*`, `/api/program-guide*`, `/admin/tv-sender`, interne
Stream-Endpunkte) serverseitig gesperrt (404/403). Worker (`ProgramScheduleWorker`, `LiveStreamWorker`,
`MediaDurationProbeService`-Anteile am Sender) pausieren bei deaktiviertem Sender. Einstellung muss
persistiert werden (vorhandener Settings-Mechanismus, z. B. `ProgramSettingsService`/AppSettings).

### Betroffene Klassen und Komponenten
- Settings: `ProgramSettingsService` / AppSettings-Mechanismus (zu klären, wie Flags heute gehalten werden)
- Admin-UI: Toggle (`TVSenderAdmin.razor` oder allgemeine Einstellungen)
- NavMenu/`MainLayout`: Menüeintrag nur bei Freigabe
- Controller: `LiveStreamController`, `ProgramGuideController`, interne Stream-Endpunkte — serverseitige
  Absicherung (nicht nur ausgeblendeter Link!)
- Worker: `ProgramScheduleWorker`, `LiveStreamWorker` — Idle-Verhalten bei deaktiviertem Sender
- Tests: Endpoint-Schutz ohne Flag, Toggle ändert Sichtbarkeit, Worker idles

### Offene Fragen
- Standardwert des Flags: deaktiviert oder aktiviert? (Annahme: deaktiviert = Feature muss explizit
  freigeschaltet werden; bei bestehender Installation ggf. Rückfrage — Default: aus.)
- Soll `/admin/tv-sender` selbst erreichbar bleiben, damit der Admin den Toggle dort bedienen kann?
  (Annahme: Toggle kommt in allgemeine Einstellungen, damit `/admin/tv-sender` komplett gesperrt
  werden kann.)
