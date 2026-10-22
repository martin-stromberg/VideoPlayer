# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Maskottchen-Clips werden als Füller eingeplant | `MediaSourceClips`-Materialart + `ExpandMediaSourceClipsAsync` + `FillGaps`-Ring | `FillerRule_ClipSource_FillsGaps`, `ClipSource_RotationContinuesAcrossRuns` | Abgedeckt |
| Clips tauchen nicht als reguläre Inhalte auf | Kein Marker nötig (NFO-Mechanik dokumentiert); Expansion filtert eingeordnete Items heraus | `ClipSource_ExcludesClassifiedItems` | Abgedeckt |
| Bevorzugung als Füller / Rotation in der Clip-Sammlung | `Filler`-Regelart + `RuleState`-Ring mit Cursor-Fortsetzung | `ClipSource_RotationContinuesAcrossRuns` | Abgedeckt |
| Duration-Pfad gilt automatisch | Probe läuft auf `MediaItem`-Ebene; Expansion verlangt `Duration != null` | `ClipSource_SkipsItemsWithoutDuration` | Abgedeckt |
| Zugriff/Freigabe-Regeln gelten | `AccessContext.Allows(sourceId)` | `ClipSource_RequiresSharedSource` | Abgedeckt |
| Material über benannte Entität in UI wählbar | Quell-`<select>` mit Quell-Namen (Muster: Playlist-Dropdown) | bUnit + E2E-Erweiterung | Abgedeckt |
| Programmauskunft zeigt sinnvollen Titel | `MediaItem.Name`-Fallback | `GetNowAsync_ClipSlot_FallsBackToMediaItemName` | Abgedeckt |
| 3–6 Drehbücher (deutsch, ~10–30 s) | 6 Markdowns unter `maskottchen/drehbuecher/` | Review-Artefakte (keine automatisierten Tests sinnvoll) | Abgedeckt |
| Tatsächlich generierte MP4-Clips | `generate_clips.py` → `clips/*.mp4`, ffprobe-Verifikation | ffprobe-Protokoll im Generator; Review | Abgedeckt |
| Werkzeug-Evaluierung dokumentiert | Designentscheidung im Plan + Handover-Abschnitt | Artefakt `handover.md` | Abgedeckt |
| Einbindungs-Anleitung | `docs/help/tv-sender.md`-Abschnitt | Doku-Schritt | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Admin wählt Clip-Quellen-Material für Füller-Regel, speichert, sieht Material-Label | Erweiterung `Admin_CreateEditDeactivateDeleteRule` (zweite Regel im selben App-Lauf) | Abgedeckt |
| Endanwender-Fluss (Live-TV) | Kein neuer E2E — `LiveTv`-Anzeige nutzt unverändert `DtoProgramGuideEntry` (Titel-Fallback durch Service-Test abgedeckt); kein neuer sichtbarer Interaktionsfluss | Nicht erforderlich mit Begründung |

## Hinweise

- Die bewusste Nicht-Umsetzung eines `IsTrailer`-analogen Markers ist als Designentscheidung
  mit Begründung im Plan festgehalten — Erfüllung der Auftragsklausel „nur wenn wirklich nötig"
  durch begründete Alternative (NFO-Mechanik + Expand-Filter).
- Keine Migration/Backup-Änderung: Enum-Erweiterung, nullable `ProgramSlot`-Referenzen
  existieren bereits.
