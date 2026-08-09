# Architektur und Frontend-Oberflaechen

## Primaeres Frontend: VideoWebPlayer

`VideoWebPlayer` ist eine .NET-10-Blazor-Webanwendung mit serverseitigem Host und Client-Projekt. Die Navigation liegt unter `VideoWebPlayer/Components/Layout/`; die fachlichen Seiten liegen unter `Components/Pages/`.

| Stitch-Ziel | Vorhandene Oberflaeche | Relevante Dateien/Bereiche |
|---|---|---|
| Dashboard | Startseite mit Continue Watching, Favoriten, zuletzt hinzugefuegten und Genre-Abschnitten | `Components/Pages/Home/Home.razor`, `Components/Shared/Home/` |
| Serien | Serien-/Quellenansicht und Serienlisten | `Components/Pages/MediaSources/`, `Components/Pages/TV/TVShowDetails.razor` |
| Filme | Film-/Sammlungsansicht | `Components/Pages/Movies/MovieCollectionDetails.razor`, `Components/Shared/Media/` |
| Detailansicht | Film-, Serien- und Sammlungsdetails mit Medienaktionen | `Components/Pages/Movies/`, `Components/Pages/TV/`, `Components/Shared/Media/MediaBase.razor` |
| Video Player | Wiedergabe und Continue-Watching-Interaktion | `Components/Shared/Media/VideoPlayer.razor`, `wwwroot/js/continueWatching.js` |
| Navigation | Top-/Side-Navigation, rollen- und quellenabhaengige Links | `Components/Layout/MainLayout.razor`, `NavMenu.razor`, `NavMenu.razor.css` |
| Administration | Quellen, Genres, Backups, Updates, Sicherheit und Programmeinstellungen | `Components/Pages/Admin/` |
| Konto | Login, Registrierung, Verwaltung, Passwort- und 2FA-Seiten | `Components/Account/` |

## Paralleles Frontend: WebPlayer

Die .NET-8-Anwendung besitzt eine geteilte Layoutstruktur. Der Host verwendet `WebPlayer/WebPlayer/Components/Layout/MainLayout.razor` und `TopNav.razor`; der Client verwendet `WebPlayer/WebPlayer.Client/Pages/` mit den Routen `/`, `/Details`, `/MediaSources`, `/Play` und `/Admin`.

Die beiden Implementierungen haben unterschiedliche Layout- und CSS-Konventionen. Ohne Vorentscheidung fuer das Zielprojekt besteht das Risiko, dass eine visuelle Aenderung nur eine der laufenden Oberflaechen erreicht.

## Fachliche Integrationsgrenzen

- Authentifizierung und Autorisierung werden ueber Blazor-`AuthorizeView`, Identity-Seiten und Rollen/Claims gesteuert.
- Medienquellen werden dynamisch aus API-/Service-Aufrufen geladen und in der Navigation angezeigt.
- Medienkarten und Detailseiten verwenden Bild-/Stream-URLs aus den bestehenden Services.
- Admin-Navigation und Sicherheits-Badge besitzen zustandsbehaftete Logik.
- Der Player nutzt Overlay-Zustand und JavaScript fuer Fortschritts-/Continue-Watching-Funktionen.

Diese Grenzen sollten bei der Umsetzung unveraendert bleiben; Styling und Komponentenzuschnitt duerfen die Navigation und Ereignisbindungen nicht entkoppeln.
