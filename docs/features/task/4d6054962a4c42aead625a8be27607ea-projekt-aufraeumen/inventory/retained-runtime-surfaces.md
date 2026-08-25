# Erhaltene Laufzeitoberflächen

## Webanwendung

`VideoWebPlayer/Program.cs` registriert die Anwendungsdienste, migriert die Datenbank, aktiviert die Webanwendung und startet die UDP-Discovery auf Port `5001`. Die Host-Adresse und der Host-Port werden für die Discovery-Antwort aus der Konfiguration verwendet.

Die API wird durch die Webanwendung selbst bereitgestellt. Der gemeinsame Client enthält unter anderem Authentifizierung, Media-Aufrufe, Bild-/Stream-Aufrufe und Fortschrittsmeldungen; beispielhaft verwendet `VideoWebPlayer.Client/VideoWebPlayerClient.cs` den Endpunkt `api/auth/login`.

## Mobile Anwendung

`VideoWebPlayer.Maui/MauiProgram.cs` registriert `MauiVideoWebPlayerClient`. Die Basisadresse wird zur Laufzeit aus den Einstellungen geladen. `ConnectionService` prüft die Verbindung per `HttpClient`, und `SettingsService` nutzt die UDP-Discovery des Servers.

Die MAUI-Anwendung erbt mit `MauiVideoWebPlayerClient` vom gemeinsamen `VideoWebPlayerClient`. Dadurch sind die API-Verträge und die Authentifizierungs-/Medienaufrufe direkt an `VideoWebPlayer.Client` gekoppelt.

## Schutzregeln für die Bereinigung

1. `VideoWebPlayer`, `VideoWebPlayer.Maui` und `VideoWebPlayer.Client` einschließlich aller Unterdateien erhalten.
2. `VideoWebPlayer.Tests` und `VideoWebPlayer.Maui.Tests` zunächst erhalten, da sie in der Solution liegen und auf Kernprojekte verweisen.
3. `lib/msTools.Updater` und die von `VideoWebPlayer.csproj` referenzierte DLL erhalten.
4. API-Endpunkte, gemeinsame Modelle, MAUI-Clientregistrierung und UDP-Discovery nicht verändern.
