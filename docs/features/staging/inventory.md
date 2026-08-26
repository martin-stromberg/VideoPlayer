# Bestandsaufnahme

## README

Die aktuelle README enthaelt fachliche Projektbeschreibung, Features, Installation, Secrets, Hook-/Markdown-Linkcheck-Details, Dokumentationslinks, Entwicklung, Veroeffentlichung, Lizenz und Autor.

Fuer die GitHub-Startseite sind insbesondere die folgenden Inhalte zu detailliert:

- Ausfuehrliche Hook- und SecretScan-Befehle.
- Veroeffentlichungsstatus und interne Freigabehinweise.
- Lange Dokumentationsliste.
- Mehrfach wiederholte Beschreibung der Repository-Verantwortung.
- Detailhinweise zu Launch-Profilen, Discovery-Adresse und Health-Endpoint.

Diese Informationen koennen in bestehende Detaildokumente verlagert bzw. dort belassen werden:

- `docs/GUIDE_Installation.md`
- `docs/SECRETS_MANAGEMENT.md`
- `docs/PUBLICATION_CHECKLIST.md`
- `docs/INDEX.md`

## About-Seite

`VideoWebPlayer/Components/Layout/MainLayout.razor` enthaelt bereits einen sichtbaren Link auf `/about`:

```razor
<NavLink class="nav-link" href="/about">
    <span class="bi bi-info-circle"></span> About
</NavLink>
```

Unter `VideoWebPlayer/Components/Pages/` existiert aktuell keine About-Seite. Die Route muss daher als neue Razor-Komponente angelegt werden, voraussichtlich `VideoWebPlayer/Components/Pages/About.razor` mit `@page "/about"`.

## Navigation und erste Schritte

Die linke Navigation zeigt Medienquellen dynamisch, wenn `VideoWebPlayerClient.RequestSourcesAsync()` Quellen liefert. Administratoren erreichen die Einrichtung ueber `/admin`; Medienquellen werden unter Admin/MediaSources verwaltet.

Die About-Seite sollte deshalb den Anwender auf diesen Ablauf fuehren:

1. Als erster Benutzer registrieren bzw. anmelden.
2. In `Einrichtung` eine Medienquelle anlegen.
3. Pfad bzw. FTP/SFTP-Zugangsdaten konfigurieren.
4. Scan/Klassifizierung ausloesen oder den automatischen Scan abwarten.
5. Die Quelle in der Navigation oeffnen.
6. Film, Serie, Staffel oder Episode auswaehlen und abspielen.

## Testoberflaechen

Es gibt bestehende Unit-/Integrationstests in `VideoWebPlayer.Tests`. Fuer die neue About-Seite sollte mindestens ein Routing-/Render-Test ergaenzt werden, sofern die bestehende Testinfrastruktur das ohne hohen Aufwand erlaubt. Andernfalls muss ein Build plus manueller/automatisierter Smoke-Test der Route dokumentiert werden.
