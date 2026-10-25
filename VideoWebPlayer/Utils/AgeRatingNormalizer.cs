namespace VideoWebPlayer.Utils;

/// <summary>
/// Normalisiert Altersfreigabe-Rohwerte (z. B. „FSK 12", „Rated PG-13", „Germany:16")
/// auf kanonische Stufen und ein vergleichbares Mindestalter. Reine Hilfsklasse ohne
/// Zustand — aufrufbar aus NFO-Parsing, Scan, Backfill und Metadaten-Editor.
/// </summary>
public static class AgeRatingNormalizer
{
    private static readonly (string Canonical, int MinAge)[] MpaaRatings =
    {
        ("NC-17", 18),
        ("PG-13", 13),
        ("PG", 8),
        ("R", 17),
        ("G", 0),
    };

    private static readonly int[] ValidFskAges = { 0, 6, 12, 16, 18 };

    /// <summary>
    /// Normalisiert einen Altersfreigabe-Rohwert.
    /// </summary>
    /// <param name="raw">Der Rohwert aus NFO oder Online-Quelle.</param>
    /// <returns>
    /// Kanonischer Anzeigewert plus Mindestalter; bei unbekannten Werten der getrimmte
    /// Rohwert mit <see cref="AgeRatingResult.MinAge"/> = <c>null</c>, bei leerer Eingabe
    /// zweimal <c>null</c>.
    /// </returns>
    public static AgeRatingResult Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new AgeRatingResult(null, null);

        var trimmed = raw.Trim();
        var candidate = trimmed;

        // Länderpräfixe („Germany:16", „DE:6", „US:PG-13") abtrennen — der Wert nach dem
        // Doppelpunkt trägt die eigentliche Stufe.
        var colonIndex = candidate.IndexOf(':');
        if (colonIndex >= 0)
            candidate = candidate[(colonIndex + 1)..].Trim();

        // „Rated PG-13" → „PG-13"
        if (candidate.StartsWith("Rated ", StringComparison.OrdinalIgnoreCase))
            candidate = candidate["Rated ".Length..].Trim();

        foreach (var (canonical, minAge) in MpaaRatings)
        {
            if (string.Equals(candidate, canonical, StringComparison.OrdinalIgnoreCase))
                return new AgeRatingResult(canonical, minAge);
        }

        // FSK-Formen: „FSK 12", „FSK12", „FSK-12"
        if (candidate.StartsWith("FSK", StringComparison.OrdinalIgnoreCase))
        {
            var rest = candidate["FSK".Length..].TrimStart(' ', '-');
            if (int.TryParse(rest, out var fskAge) && Array.IndexOf(ValidFskAges, fskAge) >= 0)
                return new AgeRatingResult($"FSK {fskAge}", fskAge);
            return new AgeRatingResult(trimmed, null);
        }

        // Nackte Zahl (z. B. „16" aus „Germany:16" oder einem TMDB-Länderwert)
        if (int.TryParse(candidate, out var bareAge) && Array.IndexOf(ValidFskAges, bareAge) >= 0)
            return new AgeRatingResult($"FSK {bareAge}", bareAge);

        return new AgeRatingResult(trimmed, null);
    }
}
