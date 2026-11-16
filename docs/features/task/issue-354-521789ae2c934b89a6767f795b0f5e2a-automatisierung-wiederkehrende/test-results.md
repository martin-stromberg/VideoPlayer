# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

Hinweis zum Status: Kein automatisierter Test ist fehlgeschlagen (die zwei
Playwright-Ausfälle im Volllauf sind dokumentierte Flakes und bei Einzelwiederholung grün).
Der Status folgt ausschließlich der Regel aus `/run-tests` Schritt 4: Die reale
End-to-End-Abnahme der sechs Akzeptanzszenarien ist vor der Promotion `staging` → `main`
technisch nicht durchführbar (`schedule` und `workflow_dispatch` laufen nur vom
Default-Branch `main` — bestätigte Restriktion, siehe plan.md Scope-Ausschluss 6).
`Nicht durchführbar` gilt nicht als Erfolg; die Szenarien bleiben offen, bis der Anwender
sie nach der Promotion manuell abnimmt (geplanter Umsetzungsschritt 8) oder dokumentiert
verzichtet. Alle lokal durchführbaren Nachweise wurden erbracht und sind bestanden
(siehe Abschnitt „Reale End-to-End-Abnahme").

## Fehlgeschlagene Tests

### VideoWebPlayer.Tests (Volllauf, einmalige Ausfälle — bei Einzelwiederholung grün)

- **LiveTvClipBlocksE2ETests.LiveTv_ClipRun_RendersAggregatedBlockRow_WithInlineDetail** — `PlaywrightException: Locator expected to have count '3' But was: '0'` (Timeout 5 s); Einzelwiederholung: **Bestanden** (7 s). Flake-Kategorie „Playwright-Timing unter Last", dokumentiert.
- **TvSenderOnAirE2ETests.Admin_TogglesBackOnAir_StreamAndPlayerResume** — `System.TimeoutException` in `CapturePlaylistUrlAsync()` (TvSenderOnAirE2ETests.cs:47); Einzelwiederholung: **Bestanden** (8 s). Gleiche Flake-Kategorie wie der in `inventory/tests.md` dokumentierte `TvSenderOnAirE2ETests.Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage` (selbe Testklasse).

### Reale End-to-End-Abnahme — offene Szenarien (Nicht durchführbar vor Promotion)

- **Akzeptanzszenarien 1–6 (plan.md)** — `schedule`-/`workflow_dispatch`-Workflows laufen nur vom Default-Branch `main`; die Workflows existieren dort noch nicht (`gh workflow list` zeigt keinen der drei Review-Workflows). Verifikation ist als definierter Schritt 8 nach der Promotion vorgesehen; Entscheidung über manuelle Abnahme oder Verzicht liegt beim Anwender.

## E2E-Abdeckung

Keine geplanten E2E-Szenarien im Repo-Test-Sinne. Begründung aus plan.md („E2E-Tests"):
Die Anforderung berührt keinen Anwendungs-Benutzerfluss (keine UI, keine Nutzeraktion,
kein erreichbarer Code-Pfad); der Systemablauf ist der GitHub-Actions-Lauf selbst, dessen
Nachweis nur real per `workflow_dispatch` nach der Promotion erfolgen kann (Schritt 8).

## Reale End-to-End-Abnahme

Erforderlich: Die Anforderung berührt externe Dienste (GitHub-API via `gh`-CLI in den
Workflows). Die untenstehenden Szenarien wurden lokal so weit wie technisch möglich
durchgespielt; der reale Workflow-Lauf auf GitHub ist vor der Promotion nicht möglich.

| Szenario | Durchgeführt über | Ergebnis |
|----------|-------------------|----------|
| Happy Path — Ersterzeugung (Szenario 1) | Teilweise real: YAML der Composite Action validiert (parst fehlerfrei); Guard-Befehl `gh issue list --state open --label automated-review-architecture-security --json number --jq 'length' -R martin-stromberg/VideoPlayer` **real gegen GitHub ausgeführt** → `0` (Ausgangszustand ohne Label/Issue korrekt); Bash-Skript der Action mit `gh`-Stub lokal ausgeführt → `gh issue create` mit Titel `Architektur- und Sicherheitsreview (2026-11)` aufgerufen; Issue-Body wörtlich mit Anforderung abgeglichen (identisch, beginnt mit `@softwarewerkstatt`) | Nicht durchführbar (realer Lauf erst nach Promotion; alle lokal prüfbaren Teile bestanden) |
| Duplikatvermeidung / Skip-Fall (Szenario 2) | Bash-Skript der Action mit `gh`-Stub (`issue list` → `1`) lokal ausgeführt → `::notice::Ein offenes Issue des Typs '...' existiert bereits` ausgegeben, `exit 0`, kein `issue create` | Nicht durchführbar (Skip-Logik lokal verifiziert und bestanden; realer Zweitlauf erst nach Promotion) |
| Zyklus-Fortsetzung nach Abschluss (Szenario 3) | Dieselbe Stub-Simulation, Guard `0` → Create-Pfad | Nicht durchführbar (Logik verifiziert; realer Lauf erst nach Promotion) |
| Wöchentliche und jährliche Prüfung (Szenario 4) | Alle drei Workflows + Action YAML-validiert; Cron-Syntax geprüft (`0 4 1 * *`, `0 5 * * 1`, `0 4 1 1 *`); Stub-Lauf erzeugte Titel `Wöchentliche NuGet-Paketprüfung (2026-W46)` und `Jährliches Compliance- und Copyright-Review (2026)`; beide Issue-Bodies wörtlich mit Anforderung abgeglichen (identisch, wöchentlicher Text inkl. PR-Umsetzungs-Anweisung) | Nicht durchführbar (realer Lauf erst nach Promotion; lokale Teile bestanden) |
| Unabhängigkeit der Typen (Szenario 5) | Guard je Typ gegen **echtes Repo** ausgeführt: alle drei Labels (`automated-review-architecture-security`, `automated-review-nuget`, `automated-review-compliance`) liefern unabhängig `0`; Guard wertet ausschließlich das eigene Label aus | Nicht durchführbar (Label-Unabhängigkeit lokal/real am Guard verifiziert; realer Zwei-Workflow-Lauf erst nach Promotion) |
| Fehlende externe Voraussetzung `@softwarewerkstatt` (Szenario 6) | Erwähnung steht wörtlich am Anfang aller drei Issue-Bodies; externe Reaktion nicht im Repo verifizierbar (plan.md: Risiko + Fallback Assignee dokumentiert) | Nicht durchführbar (erst nach erstem realem Lauf beobachtbar) |

Hinweis zur Ausführung: `gh auth status` zeigt einen aktiven Account
(`softwarewerkstatt`, Token-Scopes u. a. `repo`, `workflow`). Es wurden **keine**
schreibenden Befehle (`gh label create`, `gh issue create`, `gh workflow run`) gegen
das echte Repository ausgeführt — nur lesende Guards und lokale Stub-Simulation, um
keine Seiteneffekte (Label/Issues) vorzeitig zu erzeugen.

## Zusammenfassung

- Gesamt: 2964
- Bestanden: 2962 (davon 2 zunächst fehlgeschlagen, bei Einzelwiederholung grün — siehe oben)
- Fehlgeschlagen: 0 (nach Einzelwiederholung der zwei Flakes)
- Übersprungen: 2

Vorausgehende Builds (DoD-Gate): `dotnet build VideoPlayer.sln` Debug **und** Release —
jeweils 0 Fehler, 0 Warnungen. `MarkdownLinkCheck`: bestanden (438 Dateien, 295 lokale
Links). YAML-Parse aller vier neuen Artefakte: fehlerfrei.

## Testabdeckung

**Abdeckung:** 95,95 % Zeilen (70,42 % Branch) — `dotnet test ... --collect:"XPlat Code Coverage"`, Cobertura-Report `VideoWebPlayer.Tests/TestResults/bdb6e292-dd2e-40d0-9ab4-a7bee9332514/coverage.cobertura.xml`.

| Datei | Abdeckung |
|-------|-----------|
| 38 Dateien mit 0 % Zeilenabdeckung | siehe „Fehlende Tests" |
| 152 weitere Dateien | < 80 % (u. a. `Services/SftpMediaSourceReader.cs` 11,4 %, `Components/Account/IdentityRevalidatingAuthenticationStateProvider.cs` 10,0 %, `Services/DemoData/FileSystemDemoDataSetService.cs` 24,3 %, `Services/Backups/ManualBackupJobService.cs` 29,5 %, `Controllers/FavoritesController.cs` 31,4 %, `Controllers/SourceGenresController.cs` 34,2 %, `Components/Pages/TV/TVShowDetails.razor` 34,8 %, `Services/FavoritesService.cs` 39,8 %, `Services/RecentEntryService.cs` 49,3 %) |

Alle Abdeckungslücken sind preexisting — dieser Branch fügt keinen Anwendungscode hinzu
(nur `.github/workflows/*.yml`, `.github/actions/create-review-issue/action.yml` und
Markdown-Dokumentation; YAML/Markdown haben keine Coverage-Metrik).

## Fehlende Tests

Quelle: `Coverage-Daten` (per Datei aggregiert; 38 Dateien mit 0 % Zeilenabdeckung — preexisting, keine neuen Quelldateien im Branch)

- `VideoWebPlayer.Client/Models/DtoRecentEntry.cs` — 0 % Abdeckung
- `VideoWebPlayer.Client/Models/ImpersonateRequest.cs` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/IdentityNoOpEmailSender.cs` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/ConfirmEmail.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/ConfirmEmailChange.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/ExternalLogin.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/ForgotPassword.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/LoginWith2fa.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/LoginWithRecoveryCode.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/ChangePassword.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/DeletePersonalData.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/Disable2fa.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/Email.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/EnableAuthenticator.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/ExternalLogins.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/GenerateRecoveryCodes.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/Index.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/PersonalData.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/ResetAuthenticator.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/SetPassword.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/TwoFactorAuthentication.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/RegisterConfirmation.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/ResendEmailConfirmation.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/ResetPassword.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Shared/RedirectToLogin.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Shared/ShowRecoveryCodes.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Actors/ActorDetails.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Actors/Actors.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Admin/UserManagement.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Errors/Error.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Samples/Auth.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Samples/Counter.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Samples/Weather.razor` — 0 % Abdeckung
- `VideoWebPlayer/Controllers/Attributes/ConnectionCheckAttribute.cs` — 0 % Abdeckung
- `VideoWebPlayer/Hubs/MediaUpdateHub.cs` — 0 % Abdeckung
- `VideoWebPlayer/Services/DemoData/IDemoDataSetService.cs` — 0 % Abdeckung
- `VideoWebPlayer/Services/SftpStreamWrapper.cs` — 0 % Abdeckung
- `VideoWebPlayer/Utils/LocalNetworkHelper.cs` — 0 % Abdeckung

## Hinweise

- Kosmetische Abweichung (aus plan-check.md bereits markiert): `title-suffix-format: "%G-W%V"` erzeugt `2026-W46`; plan.md/Szenario 4 beschreiben `(JJJJ-KWww)` — müsste `%G-KW%V` sein, falls das `KW`-Präfix gewollt ist. Kein Testfehler, aber Angleichung von Format oder Dokumentation anzuraten.
- Die zwei initialen Fehlschläge sind Playwright-Timeouts unter Volllast; beide Tests stehen in keinem Zusammenhang zu diesem Branch (kein Anwendungscode geändert) und gingen bei Einzelwiederholung sofort grün. Der dokumentierte Flake `Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage` trat in diesem Lauf **nicht** auf.
