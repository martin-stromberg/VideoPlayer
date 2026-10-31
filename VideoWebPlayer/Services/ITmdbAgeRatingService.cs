namespace VideoWebPlayer.Services;

/// <summary>
/// Optionaler, fehlertoleranter Online-Lookup für Altersfreigaben über TMDB.
/// Alle Methoden liefern <c>null</c>, wenn der Dienst deaktiviert, nicht konfiguriert
/// oder nicht erreichbar ist — sie werfen keine Ausnahmen für fachliche Fehler.
/// </summary>
public interface ITmdbAgeRatingService
{
    /// <summary>
    /// Ermittelt die Altersfreigabe eines Films.
    /// </summary>
    /// <param name="title">Filmtitel (für die Titelsuche, wenn keine ID vorliegt).</param>
    /// <param name="year">Erscheinungsjahr zur Eingrenzung der Titelsuche.</param>
    /// <param name="tmdbId">TMDB-ID aus der NFO (<c>uniqueid type="tmdb"</c>/<c>tmdbid</c>), falls bekannt.</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Die gewählte Länderfreigabe oder <c>null</c>.</returns>
    Task<TmdbAgeRating?> GetMovieAgeRatingAsync(string title, int? year, string? tmdbId, CancellationToken cancellationToken);

    /// <summary>
    /// Ermittelt die Altersfreigabe einer Serie.
    /// </summary>
    /// <param name="title">Serientitel (für die Titelsuche, wenn keine ID vorliegt).</param>
    /// <param name="year">Startjahr zur Eingrenzung der Titelsuche.</param>
    /// <param name="tmdbId">TMDB-ID aus der NFO, falls bekannt.</param>
    /// <param name="cancellationToken">Abbruchtoken.</param>
    /// <returns>Die gewählte Länderfreigabe oder <c>null</c>.</returns>
    Task<TmdbAgeRating?> GetTVShowAgeRatingAsync(string title, int? year, string? tmdbId, CancellationToken cancellationToken);
}
