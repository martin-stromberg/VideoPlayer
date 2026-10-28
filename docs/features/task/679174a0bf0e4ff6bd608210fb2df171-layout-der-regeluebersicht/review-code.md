# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TVSenderAdmin.razor (TVSenderAdmin)

- **Doppelter Code** — Im Toggle-Button (Zeilen 145–154) steht die
  `@if/@else`-Verzweigung um zwei vollständige `<svg>`-Elemente, deren
  gesamte Attributliste identisch ist (`aria-hidden="true"`, `width="18"`,
  `height="18"`, `viewBox="0 0 24 24"`, `fill="none"`, `stroke="currentColor"`,
  `stroke-width="2"`, `stroke-linecap="round"`, `stroke-linejoin="round"`).
  Nur die inneren Formen unterscheiden sich (zwei `<rect>`-Elemente für Pause
  vs. ein `<polygon>` für Play). Die ~110 Zeichen lange Attributliste ist damit
  ein nahezu identischer Codeblock, der bei Anpassungen des Icon-Stils
  auseinanderlaufen kann.

  Empfehlung: Ein einziges `<svg>`-Element mit der gemeinsamen Attributliste
  ausgeben und die `@if/@else`-Verzweigung nur um die inneren Formen legen:
  `<svg ...>@if (rule.IsActive) { <rect … /><rect … /> } else { <polygon … /> }</svg>`.

## Geprüfte Dateien

- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
- `VideoWebPlayer/wwwroot/app.css`
- `VideoWebPlayer.Tests/Components/TVSenderAdminTests.cs`
- `VideoWebPlayer.Tests/Components/TVSenderAdminTests_ClipGroups.cs`
- `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs`
- `VideoWebPlayer.Tests/TVSenderAdminClipGroupsE2ETests.cs`
- `docs/help/tv-sender.md`
