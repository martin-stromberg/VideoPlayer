# Bestandsaufnahme – Aufräumen der Backupverwaltung

## Branch

`task/issue-142-ce955b274b1c4da2b7a1d0bdcc9a8ca7-aufraeumen-der-backupverwaltun`

## Eingangsartefakte

- `issue.md` mit der Kundenanforderung
- `stitch_private_media_library-backup.zip` mit Designentwurf (`code.html`, `screen.png`)

## Betroffene Dateien

### UI-Seite

- `VideoWebPlayer/Components/Pages/Admin/Backups.razor`
  - Enthält die komplette Backup-Verwaltungsseite.
  - Aktuell: Karteikarten-Grid für Backups, separate Upload-Sektion, separate Historie-Sektion, Settings-Grid.
  - Implementiert mit Blazor, InteractiveServer, Antiforgery, AuthState.

### Services

- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupFacade.cs`
  - Koordiniert Backup-Operationen, Settings und Historie.
- `VideoWebPlayer/Services/Backups/BackupSettingsService.cs`
  - Liefert und aktualisiert `BackupSettings`.
- `VideoWebPlayer/Data/BackupSettings.cs`
  - Einstellungsmodell: StoragePath, AutomaticBackupsEnabled, Son/Father/Grandfather Retention, MaxUploadSizeBytes.

### Styles

- `VideoWebPlayer/wwwroot/app.css`
  - Enthält Admin-Console-Theme und Backup-spezifische CSS-Klassen.

### Dokumentation

- `docs/help/backups.md`
  - Benutzerdokumentation für die Backup-Funktion.

## Aktueller Seitenaufbau

1. Nicht autorisiert / Ladezustand
2. Erfolgs-/Fehlermeldungen
3. Fortschrittsmeldungen für Restore und manuelles Backup
4. Aktionen: Backup erstellen, Aktualisieren
5. Statistiken: Bestand, Speicher, Automatik
6. Vorhandene Backups als Karten-Grid
7. Restore-Bestätigung
8. Löschen-Bestätigung
9. Backup hochladen
10. Einstellungen
11. Historie

## Benötigte Änderungen

1. Seitenkopf mit Kicker, Titel und primärem CTA neu gestalten.
2. Statistikkarten in einer Reihe oben platzieren.
3. Hauptbereich in ein 2/3 + 1/3 Grid aufteilen.
   - Links: Letzte Backups als Liste.
   - Rechts: Konfiguration.
4. Upload und Historie behalten, aber visuell integrieren (z. B. in die linke Spalte unterhalb der Backups oder als ausklappbare Bereiche).
5. Farben und Abstände an den Designentwurf anpassen, ohne das bestehende Designsystem zu brechen.

## Offene Designentscheidungen

- Upload- und Historie-Bereich sollen im neuen Layout platziert werden. Mögliche Varianten:
  1. Unterhalb der Backup-Liste in der linken Spalte.
  2. In einer separaten, einklappbaren Sektion.
  3. Als separate Registerkarten.
- Retention-Settings: Der Entwurf zeigt ein einfaches Dropdown. Die aktuelle GVS-Logik (Sohn/Vater/Großvater) bleibt aber im Datenmodell. Es wird empfohlen, die drei Werte in einem kompakten „Aufbewahrung"-Block in der rechten Spalte anzuzeigen.

## Test-Infrastruktur

- `VideoWebPlayer.Tests` enthält Unit-Tests für Backup-Services.
- Es existieren keine dedizierten UI-Tests für die Backups-Seite.
- E2E-Tests sind im Projekt `VideoWebPlayer.Maui.Tests` nicht ersichtlich.
- Da die Anforderung ein UI-Feature ist, sollten E2E-Tests für den Benutzerfluss geplant werden (Navigationspfad, Backup-Button, Konfigurationsspeicherung).
