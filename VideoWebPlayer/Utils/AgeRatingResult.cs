namespace VideoWebPlayer.Utils;

/// <summary>
/// Ergebnis der Altersfreigabe-Normalisierung: kanonischer Anzeigewert plus
/// vergleichbares Mindestalter (für spätere Bereichsvergleiche wie „≤ FSK 12").
/// </summary>
/// <param name="DisplayValue">Kanonischer Anzeigewert (z. B. „FSK 12", „PG-13") oder der getrimmte Rohwert bei unbekannten Angaben.</param>
/// <param name="MinAge">Vergleichbares Mindestalter oder <c>null</c>, wenn der Rohwert keiner bekannten Stufe entspricht.</param>
public sealed record AgeRatingResult(string? DisplayValue, int? MinAge);
