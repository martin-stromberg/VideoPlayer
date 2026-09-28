namespace VideoWebPlayer.Services;

/// <summary>
/// The per-user data needed to check accessibility of playlist entries in-memory (see
/// <see cref="PlaylistEntryAccessResolver.CheckEntryAccessible"/>): the ids of movie collections and TV
/// shows individually unlocked for the user, and the ids of media sources the user has regular access to.
/// Bulk-loaded once per request by <see cref="PlaylistEntryAccessResolver.LoadAccessCheckDataAsync"/> and
/// then reused for every entry, instead of being re-queried per entry.
/// </summary>
/// <param name="UnlockedMovieCollectionIds">The ids of movie collections unlocked for the user.</param>
/// <param name="UnlockedTVShowIds">The ids of TV shows unlocked for the user.</param>
/// <param name="MediaSourceIds">The ids of media sources the user has regular access to.</param>
internal sealed record AccessCheckData(
    HashSet<long> UnlockedMovieCollectionIds,
    HashSet<long> UnlockedTVShowIds,
    HashSet<long> MediaSourceIds);
