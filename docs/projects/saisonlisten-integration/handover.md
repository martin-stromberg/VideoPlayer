# Übergabe: Integration saisonaler Genre-Listen

## Bearbeiter

| Schritt | Bearbeiter | Status | Branch |
|---|---|---|---|
| 2 – Integration auf aktuellem staging | Codex | aktiv | task/saisonlisten-schritt-2-server-integration |

## Auftrag und Grundlage

Nur den saisonalen Diff der mobilen Lieferung (`eae60a6003ac0619958331351d21c5d983cb175f` bis `38027cb9242f5eec8a834ed240ca64b6e19a833d`) auf aktuellem `origin/staging` integrieren. Integrationsbasis: `6a89228edf77a756563abbc7c211b8abe634533f`. Es wird keine fremde Feature-Historie zusammengeführt.

Der ursprüngliche Servercheckout bleibt unverändert. Main-Promotion und Laufzeit-Deployment sind nicht Teil dieses Integrationsschritts. Unabhängiges Review und CI müssen vor einem Merge erfolgen.

## Stand

- Eigener Integrationsklon erstellt und Zielbranch geprüft.
- Saisonfunktion auf aktuellem staging noch nicht vorhanden.
- Direkter Patchcheck ergibt ausschließlich Kontextkonflikte in README und Release Notes; Produktdiff muss noch übernommen und geprüft werden.
- Mobile Vollsuite bleibt wegen nativem WinRT-Shutdownfehler offen; keine mobile Freigabe behauptet.

## Prüfungen

Ausstehend: Debug-/Release-Build, vollständige Serversuite, saisonale API-/Web-E2E, unabhängige Integrationsabnahme, CI.
