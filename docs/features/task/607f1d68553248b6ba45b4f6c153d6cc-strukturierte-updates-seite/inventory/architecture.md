# Bestehende Architektur und Datenfluss

## UI

`Updates.razor` ist eine interaktive Server-Blazor-Komponente auf `/admin/updates`. Sie prueft beim Initialisieren Authentifizierung und den Claim `IsAdmin=True`. Danach laedt sie mit `UpdateAdminService.GetSnapshotAsync()` Status und Einstellungen und aktualisiert den Status zusaetzlich alle drei Sekunden per `PeriodicTimer`.

Die Komponente stellt aktuell dar:

- Statusalert mit Zustand, verfuegbarer Version, Fehlertext und Lock-Hinweis.
- POST-Formular fuer `/admin/updates/api/check`.
- POST-Formular fuer `/admin/updates/api/install`.
- interaktive lokale Snapshot-Aktualisierung ueber `ReloadAsync`.
- Statusdetails als Tabelle.
- `EditForm` mit DataAnnotations fuer Konfiguration und Speichern.

Beim Speichern wird ein `UpdateSettingsUpdate` erzeugt. Eine neue Aktivierung des Prerelease-Kanals erfordert aktuell zusaetzlich eine Bestaetigung in der UI.

## HTTP-Aktionen

`UpdatesController` ist unter `/admin/updates/api` geroutet und durch `AdminOnly` geschuetzt. `POST /check` und `POST /install` validieren das Antiforgery-Token und delegieren an die Fassade. Das Ergebnis wird als Query-Parameter zur Seite zurueckgeleitet.

`InstallAsync` laedt bei `UpdateAvailable` zuerst das Paket und ruft danach `InstallAsync(true, false, ...)` auf. Bei `ReadyToInstall` wird der Download uebersprungen. `IsInstallable` kapselt die fachliche Aktivierungsregel des Install-Buttons.

## Laufzeit und Persistenz

`UpdateAdminService` kombiniert `IUpdateSettingsService` mit `IAutoUpdateOrchestrator` und `IAutoUpdateCommandHandler`. `UpdateSettingsService` liest bzw. erzeugt die Singleton-Zeile mit `Id = 1`, speichert sie in `ApplicationDbContext.UpdateSettings` und uebertraegt Werte in `AutoUpdateOptions`.

Die Initialisierung erfolgt beim Host-Start durch `UpdateSettingsInitializer`. Backup-relevante Werte werden ausserdem als `UpdateBackupOptions` fuer die Backup-Infrastruktur bereitgestellt.

## Navigations- und Layoutbefund

Das Admin-Dashboard verlinkt direkt auf `/admin/updates`. Die globale Anwendung verwendet die bestehende Layout-/Navigationsstruktur; die Stitch-Datei besitzt dagegen eine eigene Seitenleiste und Topbar. Fuer die Umsetzung ist daher die vorhandene Anwendungshuelle beizubehalten und nur die Inhaltsstruktur der Updates-Seite an den Entwurf anzunaehern.
