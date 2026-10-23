# Usability-Review: TVSenderAdmin — Trailer-Material

## Ergebnis

**Status:** Erfüllt

## Befund

- Die Trailer-Materialart wird als normale Checkbox „Trailer anstehender Filme"
  (`id="rule-material-trailer"`) im bestehenden Material-Bereich angeboten — analog zu den
  Genre-/Quellen-Checkboxen aus Schritt 9. Keine interne ID nötig; `MaterialId` bleibt 0.
- Sichtbarkeit folgt `AllowedMaterialTypes` (serverseitige Vorgabe aus
  `ProgramRuleService.GetAllowedMaterialTypes`) — die Option erscheint nur für Regelarten, für
  die Trailer-Material zulässig ist; die serverseitige Validierung bleibt die eigentliche
  Schranke (`ValidateRuleInputAsync` + `exists`-Switch).
- `MaterialLabel` zeigt „Trailer anstehender Filme" — kein Roh-Enum-Name.
- Umschalten räumt Genre-/Quellen-Auswahl (`SelectedGenreIds`/`SelectedMediaSourceIds`), wie bei
  Playlist/Genre-Mustern; Abwählen ruft `ClearMaterial` auf.
- Konsistenz: gleiche Checkbox-Interaktion wie Genre (kein neues UI-Muster eingeführt).
