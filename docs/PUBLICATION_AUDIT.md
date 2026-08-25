# Veroeffentlichungs-Audit

> **Dokumenttyp**: Release-Nachweis  
> **Zielgruppe**: Maintainer  
> **Stand**: 2026-08-25  
> **Web-Commit bei lokaler Pruefung vor Abschlusscommit**: `3f1e816565ae0ce90f96625854b8242533d38ba4`

Dieses Dokument haelt den lokal ausgefuehrten Pruefstand vor dem Umschalten des Web-Repositorys auf `public` fest. Es ersetzt keine Maintainer-Freigabe fuer externe Systeme wie GitHub-Repository-Einstellungen, produktive Secrets oder das ausgelagerte MAUI-Repository.

## Ergebnisuebersicht

| Gate | Status | Ergebnis |
|------|--------|----------|
| Web-Build | Bestanden | `dotnet build VideoPlayer.sln` lief erfolgreich; bestehende Compiler-/Analyzer-Warnungen bleiben bestehen. |
| Web-Tests | Bestanden | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build` lief erfolgreich: 188/188 Tests bestanden. |
| API-Vertragstest | Bestanden | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --filter ApiDocumentationContractTests` lief erfolgreich: 4/4 Tests bestanden. |
| MarkdownLinkCheck-Tests | Bestanden | `dotnet test tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj --no-build` lief erfolgreich: 5/5 Tests bestanden. |
| Markdown-Linkcheck | Bestanden | `dotnet run --no-restore --project tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj -- --root .` lief nach der Bereinigung erfolgreich: 61 Dateien, 101 lokale Links. |
| NuGet-Vulnerability-Scan Web | Bestanden | `dotnet list VideoPlayer.sln package --vulnerable --include-transitive` meldete fuer alle Projekte keine anfaelligen Pakete. |
| Hook-Pfad | Bestanden | `git config core.hooksPath .githooks` wurde gesetzt; `git config --get core.hooksPath` liefert `.githooks`. |
| Hook-Dateimodus | Bestanden | `git ls-files --stage .githooks/pre-commit` meldet `100755`. |
| Hook-Lauf in aktueller Windows-Umgebung | Eingeschraenkt | Der Hook ruft `sh` auf; `sh`/`bash` ist in dieser Windows-Umgebung nicht auf dem PATH. Der Linkcheck-Befehl des Hooks wurde separat erfolgreich ausgefuehrt. |
| Linux-Frischclone-Hook-Test | Offen | Muss in Linux oder WSL ausgefuehrt werden, siehe Anleitung unten. |
| Secret-Scan Web-Arbeitsbaum | Bestanden mit erwarteten Treffern | Keyword-Treffer sind Dokumentation, Konfigurationszugriffe und Testwerte; ein konkreter Token-Format-Scan auf GitHub-, OpenAI-, AWS-, Google- und Private-Key-Muster war leer. |
| Secret-Scan Web-Historie | Bestanden mit erwarteten Treffern | Der vereinbarte Keyword-Historienscan liefert nur Commit-Kontext zu bekannten Token-/Secret-Themen; der konkrete Token-Format-Historienscan war leer. |
| Lokale Git-Remote-URL | Bereinigt, Rotation erforderlich | Die lokale `origin`-URL enthielt initial ein eingebettetes GitHub-Token. Die URL wurde auf `https://github.com/martin-stromberg/VideoPlayer` bereinigt. Das zuvor eingebettete Token muss ausserhalb des Repositorys rotiert werden. |
| Private Pfade/interne Reste | Bereinigt | Alte `docs/features/`-Arbeitsartefakte mit lokalem Pfad wurden aus dem oeffentlichen Arbeitsbaum entfernt. |
| MAUI-Repository | Offen | In dieser Arbeitskopie existiert kein `Sub-Repository/`; Remote-Stand, drei vorbereitende Commits, Build, Tests, Vulnerability-Scan und Secret-Scan muessen im ausgelagerten Repository nachgewiesen werden. |
| GitHub-Repository-Einstellungen | Offen | `gh auth status` meldet einen ungueltigen `GITHUB_TOKEN`; Branch Protection, Actions Permissions, Issues/Discussions und Security Advisories muessen mit Maintainer-Zugriff geprueft werden. |
| Produktive Secret-Rotation | Offen | Muss ausserhalb des Repositorys erfolgen und dokumentiert freigegeben werden. |
| `public`-Umschaltung und Tagging | Offen | Erst nach Abschluss aller offenen Gates ausfuehren. |

## Ausgefuehrte Kommandos

```powershell
git config core.hooksPath .githooks
git config --get core.hooksPath
git ls-files --stage .githooks/pre-commit
dotnet build VideoPlayer.sln
dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --logger "console;verbosity=minimal"
dotnet test tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj --no-build --logger "console;verbosity=minimal"
dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --filter ApiDocumentationContractTests --logger "console;verbosity=minimal"
dotnet run --no-restore --project tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj -- --root .
dotnet list VideoPlayer.sln package --vulnerable --include-transitive
git grep -n -I -E "Jwt:Key|Jwt__Key|ApiToken|Authorization: Bearer|password|secret|token" -- .
git log --all --source --decorate -G "Jwt:Key|Jwt__Key|ApiToken|Authorization: Bearer|password|secret|token" -- .
git grep -n -I -E "ghp_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]+|sk-[A-Za-z0-9]{20,}|AKIA[0-9A-Z]{16}|AIza[0-9A-Za-z_-]{35}|-----BEGIN (RSA |OPENSSH |EC |DSA )?PRIVATE KEY-----" -- .
git log --all --source --decorate -G "ghp_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]+|sk-[A-Za-z0-9]{20,}|AKIA[0-9A-Z]{16}|AIza[0-9A-Za-z_-]{35}|-----BEGIN (RSA |OPENSSH |EC |DSA )?PRIVATE KEY-----" -- .
```

## Noch manuell auszufuehren

### MAUI-Repository

Im ausgelagerten MAUI-Repository:

```bash
git status -sb
git log --oneline --decorate -n 10
git fetch --all --prune
git branch -vv
dotnet restore VideoPlayer.App.sln
dotnet test VideoWebPlayer.Maui.Tests/VideoWebPlayer.Maui.Tests.csproj
dotnet build VideoWebPlayer.Maui/VideoWebPlayer.Maui.csproj -p:MauiClientApiToken="<ENTWICKLUNGS_MAUI_API_TOKEN>"
dotnet list VideoPlayer.App.sln package --vulnerable --include-transitive
git grep -n -I -E "Jwt:Key|Jwt__Key|ApiToken|Authorization: Bearer|password|secret|token" -- .
git log --all --source --decorate -G "Jwt:Key|Jwt__Key|ApiToken|Authorization: Bearer|password|secret|token" -- .
```

Pruefen und dokumentieren:

- Arbeitsbaum sauber.
- Die drei vorbereitenden Commits liegen auf dem Remote.
- Keine High-Advisories ohne dokumentierte Freigabe.
- Keine ungeklarten Secret-Treffer.

### Linux-Frischclone-Hook-Test

In einer Linux- oder WSL-Umgebung:

```bash
tmpdir="$(mktemp -d)"
git clone https://github.com/martin-stromberg/VideoPlayer "$tmpdir/VideoPlayer"
cd "$tmpdir/VideoPlayer"
git checkout <FREIGABE_COMMIT>
git config core.hooksPath .githooks
git ls-files --stage .githooks/pre-commit
dotnet restore tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj
dotnet build tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj --no-restore
.githooks/pre-commit
```

Erwartung:

- `git ls-files --stage .githooks/pre-commit` beginnt mit `100755`.
- Der Hook laeuft mit installiertem `dotnet` erfolgreich.

Fehlerpfad ohne `dotnet` pruefen:

```bash
env PATH="/usr/bin:/bin" .githooks/pre-commit
```

Erwartung:

- Exit-Code `1`.
- Fehlermeldung: `Markdown link check failed: dotnet was not found.`

### GitHub-Repository-Einstellungen

Mit Maintainer-Zugriff pruefen:

- Visibility bleibt bis Abschluss aller Gates `private`.
- Branch Protection fuer `main` und `staging` ist aktiv und verlangt passende CI-Checks.
- Actions Permissions sind auf benoetigte Schreibrechte begrenzt.
- Issues und Discussions sind bewusst aktiviert oder deaktiviert.
- Security Advisories und private vulnerability reporting sind fuer ein oeffentliches Repository passend gesetzt.
- Release- und Promotion-Workflows duerfen nur aus den vorgesehenen Branches laufen.
- Lokale oder CI-Umgebungsvariable `GITHUB_TOKEN` ist gueltig oder wird entfernt, wenn sie nicht benoetigt wird.

### Produktive Secrets

Vor `public` ausserhalb des Repositorys:

- GitHub-Token aus der lokalen Remote-URL rotieren oder widerrufen.
- `Jwt:Key`, `Jwt:ApiToken:Web`, `Jwt:ApiToken:Maui` neu erzeugen.
- Pipeline-, Hosting- und MAUI-Build-Konfiguration aktualisieren.
- Bestaetigen, dass keine Platzhalterwerte aus der Dokumentation produktiv verwendet werden.

### Finale Freigabe

Erst nach Abschluss der offenen Gates:

```bash
git fetch --all --prune
git status -sb
git log --oneline --decorate -n 5
git tag <RELEASE_TAG> <FREIGABE_COMMIT>
git push origin <RELEASE_TAG>
```

Danach die Repository-Sichtbarkeit in GitHub auf `public` setzen und dieses Audit um den finalen Freigabe-Commit, den Tag und den Zeitpunkt der Sichtbarkeitsaenderung ergaenzen.
