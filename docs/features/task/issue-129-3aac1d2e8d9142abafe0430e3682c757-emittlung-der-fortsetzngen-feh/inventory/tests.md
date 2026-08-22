# Testabdeckung

## Bestehende Testklassen

### `ContinueWatchingServiceSignalRTests`
Datei: `VideoWebPlayer.Tests/Services/ContinueWatchingServiceSignalRTests.cs`

**Zweck:** Tests für die SignalR-Integration des `ContinueWatchingService`.

**Umfang:** Nur SignalR-Benachrichtigungen — **KEINE Tests für `GetNextEpisodeAsync()`**

| Testmethode | Was wird getestet? |
|-------------|-------------------|
| `ProcessBufferedEntry_NewEntry_SendsSignalRUpdate()` | Neuer Eintrag löst SignalR-Event aus |
| `ProcessBufferedEntry_UpdateExisting_SendsSignalRUpdate()` | Update eines bestehenden Eintrags löst SignalR-Event aus |
| `ProcessBufferedEntry_Episode_SendsSignalRUpdate()` | Episode-Eintrag löst SignalR-Event aus |
| `MultipleUpdates_SendsMultipleEvents()` | Mehrere Updates lösen mehrere Events aus |
| `BufferFlow_EnqueueAndProcess_SendsSignalREvent()` | Kompletter Buffer-Workflow: Enqueue → Read → Process → SignalR |

**Hilfsmethoden:**
- `CreateMockUserManager()` — Mock-Instanz für `UserManager<ApplicationUser>`
- Konstruktor setzt InMemory-Datenbank, Mock-SignalR-Hub und Logger auf

**Lücke:** Es gibt **keine Tests für die Kernlogik `GetNextEpisodeAsync()`**, insbesondere nicht für:
- Happy-Path-Szenarien (einfacher Episoden-Wechsel)
- Verschiedene ReleaseDate-Konfigurationen (NULL, vorhanden, gemischt)
- Fehlende/Lückenhafte Episoden
- Staffel-Navigation
- Edge Cases

## Hilfsmethoden und Fixtures

### `ContinueWatchingBuffer`
Datei: (referenziert in Tests) — Wird zum Puffern von Fortschrittseinträgen verwendet

- `EnqueueOrUpdate(string userId, long? movieId, long? episodeId, TimeSpan position, TimeSpan duration)` — Fügt Eintrag in den Puffer ein oder aktualisiert ihn
- `ReadNextAsync(CancellationToken ct)` — Liest nächsten gepufferten Eintrag aus

### `EventManager`
Datei: (referenziert in Tests) — Wird für DbContext-Initialisierung verwendet

### `MediaUpdateNotificationService`
Datei: (Mock in Tests) — Wird zum Senden von SignalR-Benachrichtigungen verwendet
