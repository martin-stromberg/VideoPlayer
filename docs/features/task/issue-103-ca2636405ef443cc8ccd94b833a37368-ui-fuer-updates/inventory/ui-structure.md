# Blazor/MAUI/UI-Struktur fuer Einstellungsseiten

## Aktueller Hauptkontext

Die relevante Webanwendung ist `VideoWebPlayer`. Sie nutzt Razor Components mit Interactive Server:

- `VideoWebPlayer/Program.cs`
- `VideoWebPlayer/Components/App.razor`
- `VideoWebPlayer/Components/Routes.razor`
- `VideoWebPlayer/Components/Layout/MainLayout.razor`
- `VideoWebPlayer/Components/Layout/NavMenu.razor`
- `VideoWebPlayer/Components/Pages/Admin/*.razor`

`ServiceCollectionExtensions.cs` registriert `services.AddRazorComponents().AddInteractiveServerComponents()` und `services.AddServerSideBlazor()`.

## Bestehende Einstellungsseiten

### Programmeinstellungen

`VideoWebPlayer/Components/Pages/Admin/ProgramSettings.razor`

- Route: `/admin/program-settings`
- prueft `IsAdmin`
- nutzt `EditForm`, `DataAnnotationsValidator`, `ValidationSummary`
- speichert ueber `ProgramSettingsService`
- verwendet einfache Bootstrap-Klassen (`form-control`, `btn`, `alert`)

### Backups

`VideoWebPlayer/Components/Pages/Admin/Backups.razor`

- Route: `/admin/backups`
- prueft `IsAdmin`
- zeigt Statusmeldungen und Tabellen
- nutzt `PeriodicTimer` fuer Statuspolling alle 3 Sekunden
- nutzt serverseitige Formular-Posts fuer Create/Upload und Razor-Handler fuer Restore/Delete/Settings
- deaktiviert Buttons ueber `isBusy`, aktive Jobs und Restore-Status

Dieses Muster ist besonders relevant fuer die Update-UI, weil die Anforderung laufende Aktionen, Status und parallele Mehrfachausfuehrung betrifft.

## Navigation

Admin-Menueeintraege werden in `NavMenu.razor` innerhalb eines `AuthorizeView` gerendert, wenn `IsAdmin=True` vorhanden ist. Die neue Update-Seite sollte dort im Abschnitt `Verwaltung` oder nahe `Programmeinstellungen` aufgenommen werden.

## Styling

Die bestehenden Adminseiten sind Bootstrap-basiert und funktional:

- `alert alert-*`
- `table table-dark table-striped`
- `btn btn-primary`, `btn-outline-secondary`, `btn-warning`, `btn-danger`
- `form-check`, `InputCheckbox`, `InputNumber`, `InputText`

Eine neue Seite sollte diesen Stil fortsetzen, statt ein neues Designsystem einzufuehren.

## MAUI-Projekte

Es gibt MAUI-Projekte (`Videos`, `VideoWebPlayer.Maui`) und Tests (`VideoWebPlayer.Maui.Tests`). Fuer die konkrete Anforderung "Administratoren sollen Updates ueber einen neuen Einstellungsbereich konfigurieren" gibt es in der Bestandsaufnahme keine Hinweise, dass die MAUI-Clients der primaere Ort fuer die Update-Administration sind. Die vorhandene Admin-/Backup-/Programmeinstellungsstruktur liegt im Blazor-Webprojekt.

## Erwartete neue UI-Bausteine

Naheliegende neue Komponenten/Seiten:

- `VideoWebPlayer/Components/Pages/Admin/Updates.razor`
- Navigationseintrag in `NavMenu.razor`
- ggf. ViewModel/Service im Namespace `VideoWebPlayer.Services.Updates`
- ggf. Controller `VideoWebPlayer/Controllers/UpdatesController.cs` fuer POST-Aktionen

Die Seite sollte Statuspolling analog zur Backup-Seite nutzen oder nach manuellen Aktionen den Status neu laden.
