# Bestandsaufnahme

## Relevante Dateien

- `docs/PUBLICATION_CHECKLIST.md`: zentrale Release-Gate-Liste.
- `docs/SECRETS_MANAGEMENT.md`: dokumentierte Secret-Scan-Kommandos und Rotationserwartung.
- `docs/API.md`: MAUI-relevanter API-Vertrag.
- `docs/GUIDE_Installation.md`: Setup, Hook-Aktivierung, Build- und Testanleitung.
- `docs/RELEASE_NOTES.md`: Release-Text fuer GitHub Release.
- `.githooks/pre-commit`: versionierter lokaler Markdown-Linkcheck-Hook.
- `VideoPlayer.sln`, `VideoWebPlayer.Tests/`, `tools/MarkdownLinkCheck.Tests/`: lokale Build- und Testoberflaechen.

## Lokale Beobachtungen

- Aktueller Branch: `task/issue-148-00dddce5162b4054b3226cd237445e29-veroeffentlichung-final-vorber`.
- `.githooks/pre-commit` ist im Index mit Modus `100755` versioniert.
- `core.hooksPath` war initial lokal nicht gesetzt.
- Die lokale `origin`-URL enthaelt ein eingebettetes GitHub-Token. Der konkrete Wert wird nicht dokumentiert; Token-Rotation ist vor `public` erforderlich.

## Externe Abhaengigkeiten

- MAUI-Repository muss separat verfuegbar sein und auf Remote-Stand geprueft werden.
- Linux-Frischclone-Pruefung braucht eine Linux-Umgebung oder WSL.
- GitHub-Repository-Einstellungen und Sichtbarkeit brauchen Maintainer-Zugriff.
- Produktive Secret-Rotation erfolgt ausserhalb des Repositorys.

## Detaildokumente

- `inventory/release-gates.md`
