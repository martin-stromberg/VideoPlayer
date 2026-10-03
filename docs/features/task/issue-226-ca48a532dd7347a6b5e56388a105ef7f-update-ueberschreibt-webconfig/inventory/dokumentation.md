# Dokumentationsstellen für V5 (Delta Runde 2)

Runde 1 hat Warnhinweise in sechs Dokumente eingefügt bzw. geschärft (Wordlaut „bei `msTools.Updater` angefordert"). Alle Stellen sind mit exakten Zeilennummern verifiziert (Stand `b319a93`). Für V5 sind sie auf das neue Verhalten (Schutzliste aktiv, `Merge`/`Preserve`) umzustellen — plus den Migrationshinweis, dass der Schutz erst ab dem **Folge-Update** greift (Skript der alten Version installiert diese Version), und den `python3`-Hinweis für Linux-`Merge`.

| Datei | Zeile(n) | Aktueller Inhalt (Kurz) | Anpassung in V5 |
|-------|----------|-------------------------|-----------------|
| `docs/TECH_Auto_Update.md` | 88–89 | `AppPoolName`/`SiteName`-Tabellenzeilen | `ProtectedFiles`-Zeile(n) in die Konfigurationstabelle (Z. 77–90): Pfad/Wildcards, `Strategy`, `XmlElements`, `XmlAttributes`, `JsonKeys`; Vorrang Fluent > Bindung |
| `docs/TECH_Auto_Update.md` | 94–101 | Warnblock „Dateierhalt bei Updates": „geht verloren … Mechanismus angefordert … bis dahin maschinenweite Umgebungsvariablen" | Neues Verhalten beschreiben (Backup `Updates/backup/`, Reihenfolge Backup→Kopieren→Merge, Fehler nur Warnung; Linux-`Merge` braucht `python3`); Migrationshinweis „greift ab dem übernächsten Update" |
| `docs/TECH_Auto_Update.md` | ~126 | „Es ersetzt daher keinen Dateierhalt beim [Update]" (Backup-vor-Installation-Abschnitt) | Formulierung an Mechanismus anpassen (Backup sichert Daten, `ProtectedFiles` schützt Dateien) |
| `docs/TECH_Auto_Update.md` | 8–20, 114–123 | **veraltet** (Runde-1-Restaufgabe 11): DLL-Referenz `lib/msTools.Updater/`, `gh release download`, `main-release.yml`/`create-update-manifest.sh` | Optional eigener Commit außerhalb des Auftrags: nupkg-Referenz `lib/packages`, `build-and-package/action.yml` |
| `docs/GUIDE_Installation.md` | 108 | IIS-Hinweis „Produktive Konfiguration": „geht derzeit bei jedem Programmupdate verloren … bis `msTools.Updater` den Erhalt unterstützt" | Auf Schutzliste verweisen (`environmentVariables` wird per `XmlElements` gemerged); Empfehlung maschinenweite Env-Variablen neu bewerten; Migrationshinweis |
| `docs/GUIDE_Installation.md` | 16, 182–184 | **veraltet**: `lib/msTools.Updater/msTools.Updater.dll` | Optional eigener Commit (Restaufgabe 11) |
| `docs/SECRETS_MANAGEMENT.md` | ~65 | „Update-Sicherheit der Ablage": `web.config`-`environmentVariables` nicht update-sicher; Empfehlung maschinenweite `Jwt__*` | Empfehlung final festlegen: geschützte `web.config` wieder gleichwertig? Hinweis auf `JsonKeys`/`XmlElements`-Konfiguration; Migrationshinweis (einmaliges Nachtragen nach dem Update auf diese Version) |
| `docs/help/updates.md` | 38 | „Wichtig — deployment-seitige Dateien"-Warnblock | Auf neues Verhalten umstellen (welche Dateien wie geschützt sind; `Updates/backup/` als Restore-Ablage) |
| `docs/help/updates.md` | 71 | `AppPoolName`/`SiteName`: „appsettings*.json werden bei Update ersetzt → Umgebungsvariablen update-sicher" | Nach `JsonKeys`-Entscheid: sind die Schlüssel gemerged, relativiert sich die Empfehlung |
| `docs/help/updates.md` | 77 | „Update-Backup sichert nur Anwendungsdaten — Konfigurationsdateien nicht enthalten" | Ergänzen: Dateischutz läuft über `ProtectedFiles`-Skriptmechanismus (getrennt vom Datenbackup) |
| `docs/help/backups.md` | ~61 | IIS-`web.config`-Absatz: „Da jede Update-Installation die Datei ersetzt, sind Anpassungen nach jedem Update nachzuziehen" | Neues Verhalten + Migrationshinweis; `appcmd`-Workaround bleibt als manuelle Alternative |
| `docs/help/backups.md` | ~96 | „Konfigurationsdateien … nicht Teil der Sicherung — auch nicht beim Update-Backup" | Ergänzen: `Updates/backup/` (updater-eigene Dateisicherung) ist **kein** Restore-Backup der Anwendungsdaten |
| `docs/RELEASE_NOTES.md` | 5 | EN: „Known limitation: an update installation replaces all files …" | In Fix-Eintrag umwandeln (Mechanismus aktiv seit dieser Version) + Migrationshinweis: Schutz greift erst beim **nächsten** Update, Bestandsinstallationen einmalig nachtragen; Linux-`Merge` braucht `python3` |
| `docs/RELEASE_NOTES.md` | 74 | DE: „Bekannte Einschränkung: …" | analog deutsch |

## Weitere Stellen

- `docs/Anforderung_msTools_Updater_Dateierhalt.md` (neu seit `b319a93`): bleibt als Anforderungsdokument stehen; kein Pflicht-Nachzug, aber Referenz für die Schutzlisten-Konfiguration.
- `README.md`: keine `web.config`-/Update-Abschnitte — kein Nachzug nötig.
- `docs/help/updates.md` Z. 67–71 (AppPool-Konfiguration): betroffen nur indirekt über Zeile 71 (s. o.).
