# Umsetzungsplan

## Überblick

Die Wiederherstellung wird aus dem Blazor-Request herausgelöst und als in-process Hintergrundjob ausgeführt. Der Job erhält Fortschritt aus der Backup-Restore-Pipeline und stellt einen Snapshot für UI, Status-API und Restore-Blockade bereit.

## Umsetzung

1. Fortschrittsmodell im Backup-Kern ergänzen
   - `BackupRestoreProgress` mit Datenbestandsname, Datenbestand-Index/-Gesamt, Datensatz-Index/-Gesamt und Meldung einführen.
   - `BackupRestoreContext` um `IProgress<BackupRestoreProgress>?` erweitern.

2. Zweistufigen Fortschritt im Datenprovider melden
   - `VideoWebPlayerBackupDataProvider.RestoreAsync` meldet Tabellenfortschritt beim Löschen und Import.
   - `TableDataPayload` erhält die Datensatzanzahl aus der Entitätsdatei; beim Insert wird `y von z` gemeldet.
   - Validierung bleibt unverändert vor destruktiven Operationen.

3. Background-Restore-Job einführen
   - Neuer `RestoreBackupJobService` analog zu `ManualBackupJobService`.
   - Akzeptiert Datei, UserId und Bestätigung.
   - Verhindert parallele Restores.
   - Führt `VideoWebPlayerBackupFacade.RestoreAsync` außerhalb des Request-CancellationTokens aus.
   - Hält Status `Idle`, `Queued`, `Running`, `Succeeded`, `Failed` plus Fortschritt und Fehlermeldung.

4. UI anpassen
   - `Backups.razor` startet Restore über den Job-Service.
   - Statusanzeige zeigt Datenbestand `w von x` und Datensatz `y von z`.
   - Polling lädt Restore-Status regelmäßig und aktualisiert Listen/Historie bei Abschluss.
   - Restore-/Upload-/Delete-/Settings-Aktionen werden während Restore deaktiviert.

5. Inhalts-/API-Blockade
   - Neue Middleware vor Razor/Controller-Endpunkten.
   - Während Restore:
     - Admin-Backup-Routen und statische Assets werden durchgelassen.
     - API-Requests erhalten `503 Service Unavailable` mit JSON-Status.
     - Inhaltsseiten erhalten eine knappe Statusseite ohne Inhaltsdaten.
   - Statusantwort enthält Restore-Flag, Meldung und Fortschritt.

6. Registrierung
   - `RestoreBackupJobService` als Singleton registrieren.
   - Middleware in `UseVideoWebPlayer` einhängen.

7. Tests
   - Unit-Test für `RestoreBackupJobService`: startet asynchron, verhindert parallele Restores, setzt Erfolgs-/Fehlerstatus.
   - Provider-Test: Restore meldet Tabellen- und Datensatzfortschritt.
   - Middleware-Test: API wird während Restore mit Status-JSON blockiert, Backup-Adminroute bleibt erreichbar.
   - Bestehende Tests an neue Kontextsignatur anpassen.

## Offene Punkte

Keine.
