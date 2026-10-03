# Bestandsaufnahme (Runde 2, Delta): Update überschreibt `web.config` — Integration `msTools.Updater` 0.11.0-rc.1

Delta-Bestandsaufnahme für die vertagten Tasks V1–V5 aus Runde 1 (siehe `requirement.md`). Die vollständige Runde-1-Bestandsaufnahme liegt in der Git-Historie: `git show 8aafb9f:docs/features/task/issue-226-…/inventory.md` plus Detaildokumente unter `…/inventory/` — sie bleibt maßgeblich für alle hier nicht wiederholten Bereiche (Deployment-Pipeline, Update-Mechanik 0.10.4, Datenmodell, Interfaces, Testklassen im Detail). Seit `8aafb9f` gab es **keine** Produktivcode-Änderung (einziger Folgecommit `b319a93`: Anforderungsdokument `docs/Anforderung_msTools_Updater_Dateierhalt.md`).

## Zusammenfassung

- **Neue Bibliotheks-API verifiziert:** `msTools.Updater.0.11.0-rc.1.nupkg` liegt unter `lib/packages/` (MIT-Lizenz, Commit `61e3b0e`); `AutoUpdateOptions.ProtectedFiles` (`AutoUpdate:ProtectedFiles` bindbar), `AutoUpdateProtectedFile` (`Path` mit `*`/`?`, `Strategy` `Preserve`/`Merge`, `XmlElements`-XPath, `XmlAttributes` `{xpath}@{attr}`, `JsonKeys` `:`-Pfade) sowie Fluent `PreserveFile`/`MergeFile`/`ProtectFile` — verifiziert per Decompilierung und XML-Doku. Details: [Bibliothek 0.11.0](inventory/bibliothek-0.11.md).
- **Skriptmechanik:** Alle Skriptvarianten (Windows Dienst/Exe **und** IIS-AppPool, Linux bash) führen Backup → Paketkopie → Merge/Restore strikt vor Lock-Entfernung und Neustart aus; Sicherungen unter `Updates/backup/` (bleiben liegen, kein Aufräumen/Retention); Fehler sind nur Warnungen, brechen nicht ab. Linux-`Merge` erfordert `python3` (sonst übersprungen, Update läuft weiter); der Linux-XML-Merge unterstützt nur die ElementTree-XPath-Teilmenge (keine Attributprädikate).
- **Vorrang:** Fluent-Einträge **ersetzen** die gesamte gebundene Liste (all-or-nothing, `ReapplyExplicitValues` nach der Bindung). Ohne Fluent-Aufrufe greift allein `AutoUpdate:ProtectedFiles` aus der Konfiguration — passt zur Projektkonvention (bindbare Werte in `appsettings.json`, Lambda nur für Quelle/Unit-Name).
- **Einschaltstellen unverändert:** `AutoUpdateExtensions.cs:29–35` (Fluent-Anker), `Program.cs:30`, `UpdateSettingsService.ApplyToRuntimeOptions` (`Services/Updates/UpdateSettingsService.cs:167–181`) mutiert `ProtectedFiles` **nicht** → kein Konflikt mit DB-Laufzeiteinstellungen. Beide `PackageReference`-Stellen (`VideoWebPlayer.csproj:53`, `VideoWebPlayer.Tests.csproj:22`) stehen auf `0.10.4-rc.1`; altes nupkg noch in `lib/packages/`. CI/NuGet.config brauchen keine Anpassung. Details: [Einschaltstellen](inventory/einschaltstellen.md).
- **Migrationsrelevanz:** Das Installationsskript erzeugt die **laufende alte** Version — der Schutz greift erst ab dem Update **nach** der Version, die ihn einführt. Bestandsinstallationen müssen `web.config`-/`appsettings`-Anpassungen einmalig nachtragen.
- **Nebenbefund:** Eine nicht paketierte `appsettings.Local.json` ist schon heute update-sicher (das Skript überschreibt nur, was im Paket liegt) — V3-Mehrwert beschränkt sich auf Override-Schlüssel außerhalb der `JsonKeys`-Liste.
- **Doku:** Alle Runde-1-Warnstellen zeilengenau erfasst (6 Dateien, 12 Stellen) → [Dokumentation](inventory/dokumentation.md).

Test-Ausgangszustand: Volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` in Release — **1607 Tests: 1606 bestanden, 1 übersprungen, 0 fehlgeschlagen** (identisch zu Runde 1; der Übersprungene ist ein bewusst deaktivierter, nicht auftragsbezogener E2E-Befund). Details und Nachweise: [Tests](inventory/tests.md).

## Details

- [Bibliothek `msTools.Updater` 0.11.0-rc.1 — neue API und Skriptmechanik](inventory/bibliothek-0.11.md)
- [Einschaltstellen im Repository](inventory/einschaltstellen.md)
- [Dokumentationsstellen für V5](inventory/dokumentation.md)
- [Tests](inventory/tests.md)

Runde-1-Detaildokumente (unverändert gültig, in der Historie @ `8aafb9f`): `inventory/deployment.md`, `inventory/logic.md`, `inventory/models.md`, `inventory/enums.md`, `inventory/interfaces.md`, `inventory/dokumentation.md`.
