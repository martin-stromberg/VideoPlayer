# Umsetzungsplan

## 1. README fuer GitHub-Startseite straffen

- Einstieg beibehalten: Titel, Kurzbeschreibung und Badges.
- Funktionsumfang beibehalten, aber redundant formulierte Repository-Abgrenzungen entfernen.
- Voraussetzungen auf das Noetigste kuerzen.
- Installation als kompakten Schnellstart belassen.
- Detailhinweise zu Launch-Profilen, Discovery-Adresse und Health-Endpoint in die Installationsdoku verweisen.
- Secrets nur kurz erwaehnen und auf `docs/SECRETS_MANAGEMENT.md` verlinken.
- Abschnitt `Markdown-Linkcheck` aus dem Hauptfluss entfernen oder auf einen kurzen Entwicklungshinweis reduzieren.
- Abschnitt `Veroeffentlichung` entfernen, da er fuer eine oeffentliche Startseite intern wirkt.
- Dokumentationslinks auf zentrale Einstiegspunkte reduzieren:
  - Installationsanleitung
  - API-Vertrag
  - Secrets Management
  - Dokumentationsindex
- Entwicklung knapp halten: Restore, Build, Tests, optional Hook-Verweis in einem Satz.
- Lizenz und Autor beibehalten.
- GitHub-Autor/Repo-Nennung konsistent pruefen.

## 2. About-Seite erstellen

- Neue Razor-Komponente `VideoWebPlayer/Components/Pages/About.razor` anlegen.
- Route `@page "/about"` setzen.
- Layout des bestehenden Projekts verwenden; keine neue Navigation einfuehren, weil `MainLayout` bereits auf `/about` verlinkt.
- Inhalt:
  - Kurze Beschreibung: private Videobibliothek, Verwaltung und Wiedergabe im Browser.
  - Erste Schritte:
    1. Anmelden oder initiales Konto erstellen.
    2. `Einrichtung` oeffnen.
    3. Medienquelle fuer lokale Ablage, FTP oder SFTP anlegen.
    4. Quelle speichern und Scan/Klassifizierung starten bzw. abwarten.
    5. Quelle links in der Navigation oeffnen.
    6. Video auswaehlen und abspielen.
  - Link zur GitHub-Seite: `https://github.com/martin-stromberg/VideoPlayer`.
- Tonalitaet: anwendernah, kurz, ohne technische Detailtiefe.
- Keine internen Release-/Hook-Hinweise auf der About-Seite.

## 3. Tests und Validierung

- `dotnet build VideoPlayer.sln --configuration Release /p:NoWarn=NU1903`
- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --configuration Release --no-build`
- `dotnet run --no-restore --project tools/SecretScan/SecretScan.csproj -- --root .`
- `dotnet run --no-restore --project tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj -- --root .`
- Falls praktikabel: Test fuer `/about` ergaenzen, der erfolgreichen Render/Status bestaetigt und den GitHub-Link nachweist.

## 4. Dokumentation und Release Notes

- `docs/INDEX.md` nur anpassen, falls neue About-Seite oder README-Struktur dort erwaehnt werden soll.
- `docs/RELEASE_NOTES.md` mit einem kurzen Eintrag aktualisieren:
  - README fuer GitHub-Startseite gestrafft.
  - About-Seite mit ersten Schritten und GitHub-Link ergaenzt.

## Offene Punkte

Keine.
