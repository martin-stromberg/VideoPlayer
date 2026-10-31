namespace VideoWebPlayer.Services;

/// <summary>
/// Eine Länderfreigabe, wie sie die TMDB-API liefert (z. B. <c>Country</c> „DE",
/// <c>Certification</c> „16" oder „US"/„PG-13").
/// </summary>
/// <param name="Country">ISO-3166-1-Länderkennung der Freigabe.</param>
/// <param name="Certification">Freigabewert des Landes.</param>
public sealed record TmdbAgeRating(string Country, string Certification);
