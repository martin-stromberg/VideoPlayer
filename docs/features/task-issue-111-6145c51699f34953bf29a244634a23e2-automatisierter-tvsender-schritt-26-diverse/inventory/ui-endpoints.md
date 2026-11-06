# UI & Endpunkte

## UI

| Stelle | Zustand heute |
|--------|---------------|
| `NavMenu.razor` Z. 41-44 | `/live-tv`-Link für alle Angemeldeten; injiziert `ApplicationDbContext`, `VideoWebPlayerClient` |
| `LiveTv.razor` | `@page "/live-tv"`; Login-Form bei anonym, sonst Guide — keine Flag-Prüfung |
| `TVSenderAdmin.razor` | `@page "/admin/tv-sender"`; `isAdmin`-Check; injiziert bereits `ProgramSettings` — kein Flag-Check |
| `AdminIndex.razor` Z. 51-55 | Admin-Kachel „TV-Sender" → `/admin/tv-sender` (bleibt sichtbar — Toggle lebt in „Allgemein") |
| `ProgramSettings.razor` | `@page "/admin/program-settings"`; EditForm → `UpdateGeneralSettingsAsync`; Platz für Checkbox |

## Endpunkte

| Endpunkt | Schutz heute |
|----------|--------------|
| `GET /api/program-guide/now`, `GET /api/program-guide/upcoming` (`ProgramGuideController`) | `[BearerTokenCheck]` + `CheckLoggedIn()` — kein Feature-Check |
| `GET /api/live-stream/{fileName}` (`LiveStreamController`) | `[BearerTokenCheck]` + `CheckLoggedIn()` — kein Feature-Check |
| `GET /api/internal/media-stream/{token}` (`InternalMediaStreamController`) | kurzlebiger Token für ffprobe — sender-agnostisch, bleibt |
| `/admin/tv-sender`, `/live-tv` (Blazor) | Auth/Admin — Inhalt bei deaktiviertem Sender sperren |

Zielverhalten ohne Freigabe: 404 an den API-Endpunkten, Hinweis statt Inhalt auf den Blazor-Seiten,
kein NavMenu-Eintrag.
