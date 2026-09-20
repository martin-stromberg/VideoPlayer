using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.PlaylistCover;

namespace VideoWebPlayer.Services;

/// <summary>
/// Implements playlist CRUD operations backed by <see cref="ApplicationDbContext"/>.
/// </summary>
public sealed class PlaylistService : IPlaylistService
{
    private readonly ApplicationDbContext _db;
    private readonly PlaylistSettings _playlistSettings;
    private readonly PlaylistEntryAccessResolver _accessResolver;
    private readonly PlaylistEntryReorderService _reorderService;
    private readonly PlaylistGenreService _genreService;
    private readonly PlaylistCoverValidator _coverValidator;
    private readonly PlaylistCoverImageGenerator _coverImageGenerator;
    private readonly IServiceProvider? _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistService"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="unlockedMediaService">Service used to check per-entry unlock/access status.</param>
    /// <param name="playlistSettings">Playlist configuration.</param>
    /// <param name="serviceProvider">
    /// Used to lazily resolve <see cref="ContinueWatchingService"/> in <see cref="DeletePlaylistAsync"/>,
    /// after this instance's own construction has completed: <see cref="ContinueWatchingService"/> itself
    /// depends on <see cref="IPlaylistService"/>, so resolving it eagerly through the constructor would be
    /// a circular dependency. <c>null</c> (e.g. in tests that construct this class directly) simply skips
    /// that conflict resolution.
    /// </param>
    /// <param name="coverValidator">
    /// Validates uploaded cover images (Entwicklungsschritt 10). Optional so existing call sites (tests,
    /// other constructor overload users) keep compiling unchanged; falls back to a plain instance built
    /// from <paramref name="playlistSettings"/> when not supplied via dependency injection.
    /// </param>
    /// <param name="coverImageGenerator">
    /// Generates automatic cover collages (Entwicklungsschritt 10). Optional for the same reason as
    /// <paramref name="coverValidator"/>.
    /// </param>
    public PlaylistService(
        ApplicationDbContext db,
        IUnlockedMediaService unlockedMediaService,
        IOptions<PlaylistSettings> playlistSettings,
        IServiceProvider? serviceProvider = null,
        PlaylistCoverValidator? coverValidator = null,
        PlaylistCoverImageGenerator? coverImageGenerator = null)
    {
        _db = db;
        _playlistSettings = playlistSettings.Value;
        _accessResolver = new PlaylistEntryAccessResolver(db, unlockedMediaService);
        _reorderService = new PlaylistEntryReorderService(db);
        _genreService = new PlaylistGenreService(db);
        _coverValidator = coverValidator ?? new PlaylistCoverValidator(playlistSettings);
        _coverImageGenerator = coverImageGenerator ?? new PlaylistCoverImageGenerator(db, playlistSettings, NullLogger<PlaylistCoverImageGenerator>.Instance);
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    /// <remarks>
    /// When <paramref name="genreId"/> is given, a playlist matches as soon as any of its genres - not
    /// just the ones actually displayed (<see cref="Data.Playlist.MaxDisplayedGenres"/>) - matches, mirroring
    /// how genre filtering works for other content types (see <c>ItemsController</c>).
    /// </remarks>
    public async Task<DtoPlaylist[]> GetPlaylistsAsync(string userId, long? genreId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Playlists.AsNoTracking().Where(p => p.UserId == userId);
        if (genreId is long id)
            query = query.Where(p => _db.PlaylistGenres.Any(pg => pg.PlaylistId == p.Id && pg.GenreId == id));

        var playlists = await query
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .ToListAsync(cancellationToken);

        return await ToDtosAsync(playlists, userId, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Lists every playlist currently marked public (<see cref="Playlist.IsPublic"/>), including the
    /// requesting user's own public playlists. Nothing about private playlists is ever returned.
    /// </remarks>
    public async Task<DtoPlaylist[]> GetPublicPlaylistsAsync(string userId, long? genreId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Playlists.AsNoTracking().Where(p => p.IsPublic);
        if (genreId is long id)
            query = query.Where(p => _db.PlaylistGenres.Any(pg => pg.PlaylistId == p.Id && pg.GenreId == id));

        var playlists = await query
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .ToListAsync(cancellationToken);

        return await ToDtosAsync(playlists, userId, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// READ access: the owner, or any user while the playlist is public (Entwicklungsschritt 11). The DTO is
    /// scoped to the requester (see <see cref="ToDto"/>).
    /// </remarks>
    public async Task<DtoPlaylist?> GetPlaylistAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists.AsNoTracking().FirstOrDefaultAsync(p => p.Id == playlistId, cancellationToken);
        if (playlist is null)
            return null;

        EnsureReadable(playlist, userId);

        return await ToDtoAsync(playlist, userId, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Entwicklungsschritt 11. Only administrators may set or clear the flag, and only on playlists they own
    /// themselves (conservative reading of "ihre Playlists bleiben stets privat" / "nur der Besitzer"): a
    /// non-owner - including an administrator - is refused, so an administrator can neither publish another
    /// user's playlist nor learn anything about it. Refusals are <see cref="PlaylistAccessDeniedException"/>
    /// (403). Clearing the flag revokes other users' access immediately; their playlist-bound
    /// continue-watching entries are detached in the same <c>SaveChangesAsync</c> (see
    /// <see cref="ContinueWatchingService.DetachOtherUsersFromPlaylistAsync"/>).
    /// </remarks>
    public async Task<DtoPlaylist> SetPlaylistPublicAsync(long playlistId, string userId, bool requesterIsAdmin, bool isPublic, CancellationToken cancellationToken = default)
    {
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        if (!requesterIsAdmin)
            throw new PlaylistAccessDeniedException("Nur Administratoren dürfen Playlists als öffentlich kennzeichnen.");

        if (playlist.IsPublic == isPublic)
            return await ToDtoAsync(playlist, userId, cancellationToken);

        playlist.IsPublic = isPublic;

        var continueWatchingService = _serviceProvider?.GetService<ContinueWatchingService>();
        IReadOnlyCollection<string> affectedUserIds = Array.Empty<string>();
        if (!isPublic && continueWatchingService is not null)
            affectedUserIds = await continueWatchingService.DetachOtherUsersFromPlaylistAsync(playlistId, userId, cancellationToken);
        else
            await _db.SaveChangesAsync(cancellationToken);

        if (continueWatchingService is not null)
            await continueWatchingService.NotifyUsersAsync(affectedUserIds, cancellationToken);

        return await ToDtoAsync(playlist, userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DtoPlaylist> CreatePlaylistAsync(string userId, string name, string? description, string? sortMode, CancellationToken cancellationToken = default)
    {
        var trimmedName = ValidateName(name);
        var trimmedDescription = ValidateDescription(description);
        var resolvedSortMode = ParseSortMode(sortMode, PlaylistSortMode.ByReleaseDate);

        await EnsureNameNotDuplicateAsync(userId, trimmedName, null, cancellationToken);

        if (_playlistSettings.MaxPlaylistsPerUser is int maxPlaylists)
        {
            var currentCount = await _db.Playlists.AsNoTracking().CountAsync(p => p.UserId == userId, cancellationToken);
            if (currentCount >= maxPlaylists)
                throw new InvalidOperationException("Die maximale Anzahl an Playlists wurde erreicht.");
        }

        var now = DateTime.UtcNow;
        var playlist = new Playlist
        {
            UserId = userId,
            Name = trimmedName,
            Description = trimmedDescription,
            SortMode = resolvedSortMode,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _db.Playlists.AddAsync(playlist, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(playlist, userId, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Intentionally ignores <paramref name="sortMode"/>: switching a playlist's sort mode has side
    /// effects on every entry's <see cref="PlaylistEntry.SortOrder"/> (population when switching to
    /// <see cref="PlaylistSortMode.Manual"/>, confirmation-gated clearing when switching away from it)
    /// that only <see cref="ChangeSortModeAsync"/> performs correctly. Accepting the parameter here purely
    /// for signature symmetry with <see cref="CreatePlaylistAsync"/> - and silently discarding it - would
    /// let a plain "edit name/description" form bypass that logic and corrupt the manual order without
    /// warning, so the parameter is kept (for interface/DTO symmetry) but never applied; callers that want
    /// to change the sort mode must use <see cref="ChangeSortModeAsync"/> instead.
    /// </remarks>
    public async Task<DtoPlaylist> UpdatePlaylistAsync(long playlistId, string userId, string name, string? description, string? sortMode, CancellationToken cancellationToken = default)
    {
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        var trimmedName = ValidateName(name);
        var trimmedDescription = ValidateDescription(description);

        await EnsureNameNotDuplicateAsync(userId, trimmedName, playlistId, cancellationToken);

        playlist.Name = trimmedName;
        playlist.Description = trimmedDescription;
        playlist.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(playlist, userId, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Resolves unique-index conflicts on <see cref="Data.ContinueWatchingEntry"/> before deleting the
    /// playlist itself (see <see cref="ContinueWatchingService.ResolvePlaylistDeletionConflictsAsync"/>):
    /// without this, the database's <c>ON DELETE SET NULL</c> foreign-key action can collide with the
    /// unique index on playlist-less entries when a playlist-less entry for the same media already exists,
    /// failing the subsequent <c>SaveChangesAsync</c> call with a UNIQUE constraint violation.
    /// </remarks>
    public async Task DeletePlaylistAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
    {
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        // Resolved for EVERY user with entries bound to this playlist (viewers of a public playlist
        // included), not just the owner - their rows would otherwise collide the same way.
        var continueWatchingService = _serviceProvider?.GetService<ContinueWatchingService>();
        IReadOnlyCollection<string> affectedUserIds = Array.Empty<string>();
        if (continueWatchingService is not null)
            affectedUserIds = await continueWatchingService.ResolvePlaylistDeletionConflictsAsync(playlistId, cancellationToken);

        // Entwicklungsschritt 10: Cover-Picture (hochgeladen oder generiert) verwaist sonst in der
        // Pictures-Tabelle - EF loescht das abhaengige Playlist zuerst (Fremdschluessel liegt auf
        // Playlist.CoverPictureId), das Picture-Remove hier ist daher innerhalb derselben SaveChangesAsync
        // unproblematisch.
        if (playlist.CoverPictureId is long coverPictureId)
        {
            var coverPicture = await _db.Pictures.FirstOrDefaultAsync(p => p.Id == coverPictureId, cancellationToken);
            if (coverPicture is not null)
                _db.Pictures.Remove(coverPicture);
        }

        _db.Playlists.Remove(playlist);
        await _db.SaveChangesAsync(cancellationToken);

        if (continueWatchingService is not null)
            await continueWatchingService.NotifyUsersAsync(affectedUserIds.Where(id => id != userId), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DtoPlaylistAddResult> AddMediaToPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken cancellationToken = default)
    {
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        var parsedMediaType = MediaHierarchyRegistry.ParseMediaType(mediaType);
        var normalizedMediaType = parsedMediaType.ToString();

        if (mediaId <= 0)
            throw new InvalidOperationException("MediaId muss größer als 0 sein.");

        var mediaTitle = await GetMediaTitleAsync(mediaType, mediaId, cancellationToken);
        if (mediaTitle is null)
            throw new KeyNotFoundException("Medieninhalt wurde nicht gefunden.");

        var (entriesToAdd, skippedDuplicateCount, topLevelEntry, existingEntryCount) =
            await BuildEntriesToAddAsync(playlistId, parsedMediaType, normalizedMediaType, mediaId, cancellationToken);

        if (_playlistSettings.MaxPlaylistItemCount is int maxItemCount
            && existingEntryCount + entriesToAdd.Count > maxItemCount)
            throw new InvalidOperationException("Die maximale Anzahl an Playlist-Einträgen wurde erreicht.");

        await _reorderService.AssignSortOrderForNewEntriesAsync(playlist, entriesToAdd, cancellationToken);

        // A manual add is an explicit, unambiguous inclusion decision: if the user previously removed one of
        // these exact (media type, media id) references from this playlist (recorded as a
        // PlaylistEntryExclusion by RemoveMediaFromPlaylistAsync), that earlier removal is superseded now -
        // see the "lifted on manual re-add" remark on PlaylistEntryExclusion. Without this, manually
        // re-adding an entire series after having removed a single episode from it would silently drop that
        // episode again (BuildCascadeEntries does not consult exclusions - the automatic backfill mechanism
        // is the only thing that does), and a plain single re-add of a previously removed title would leave
        // a stale exclusion row around that no future backfill run could ever clear on its own.
        await ClearExclusionsAsync(playlistId, entriesToAdd, cancellationToken);

        await _db.PlaylistEntries.AddRangeAsync(entriesToAdd, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        if (entriesToAdd.Count > 0)
            await _genreService.RecomputeGenresAsync(playlist, cancellationToken);

        return await BuildAddResultAsync(entriesToAdd, skippedDuplicateCount, topLevelEntry, userId, cancellationToken);
    }

    /// <summary>
    /// Removes any <see cref="PlaylistEntryExclusion"/> rows matching the (media type, media id) references
    /// of the given entries, for the given playlist - see the remark on <see cref="AddMediaToPlaylistAsync"/>.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="entries">The entries whose (media type, media id) references should no longer be excluded.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task ClearExclusionsAsync(long playlistId, List<PlaylistEntry> entries, CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
            return;

        var mediaTypes = entries.Select(e => e.MediaType).Distinct().ToList();
        var candidateExclusions = await _db.PlaylistEntryExclusions
            .Where(x => x.PlaylistId == playlistId && mediaTypes.Contains(x.MediaType))
            .ToListAsync(cancellationToken);

        if (candidateExclusions.Count == 0)
            return;

        var addedRefs = entries.Select(e => new MediaRef(e.MediaType, e.MediaId)).ToHashSet();
        var toRemove = candidateExclusions.Where(x => addedRefs.Contains(new MediaRef(x.MediaType, x.MediaId))).ToList();
        if (toRemove.Count > 0)
            _db.PlaylistEntryExclusions.RemoveRange(toRemove);
    }

    /// <summary>
    /// Records that the given (media type, media id) reference was deliberately removed from the given
    /// playlist by the user, so the automatic backfill mechanism (<see cref="PlaylistBackfillService"/>)
    /// will not re-add it. A no-op if such a record already exists (removing something twice - not possible
    /// through normal use since the entry is gone after the first removal, but kept defensive - must not
    /// throw a unique-constraint violation or reset <see cref="PlaylistEntryExclusion.ExcludedAt"/>).
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="mediaType">The media type of the removed reference.</param>
    /// <param name="mediaId">The media id of the removed reference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task RecordExclusionAsync(long playlistId, string mediaType, long mediaId, CancellationToken cancellationToken)
    {
        var alreadyExcluded = await _db.PlaylistEntryExclusions.AnyAsync(
            x => x.PlaylistId == playlistId && x.MediaType == mediaType && x.MediaId == mediaId, cancellationToken);
        if (alreadyExcluded)
            return;

        await _db.PlaylistEntryExclusions.AddAsync(new PlaylistEntryExclusion
        {
            PlaylistId = playlistId,
            MediaType = mediaType,
            MediaId = mediaId,
            ExcludedAt = DateTime.UtcNow
        }, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Before removing the entry, checks whether a <see cref="Data.ContinueWatchingEntry"/> bound to this
    /// same playlist (<see cref="Data.ContinueWatchingEntry.PlaylistId"/>) still references it (only
    /// possible for a directly playable entry - <see cref="PlaylistEntryMediaTypeResolver.IsPlayable"/> -
    /// since continue-watching entries only ever reference a movie or episode, never a collection entry
    /// such as TVShow/TVShowSeason/MovieCollection). If so and <paramref name="confirmContinueWatchingRemoval"/>
    /// was not given as <see langword="true"/>, throws <see cref="ContinueWatchingConfirmationRequiredException"/>
    /// without removing anything, so the caller can surface a confirmation prompt. Once removal proceeds
    /// (either because there was no such reference, or it was confirmed), the affected continue-watching
    /// entry - if any - is resolved via <see cref="ContinueWatchingService.ResolvePlaylistEntryRemovalAsync"/>:
    /// replaced with the next playable and accessible entry of this playlist (resolved via
    /// <see cref="FindAdjacentPlayableEntryAsync"/> <b>before</b> the entry is actually removed, since that
    /// method locates it by its still-valid <see cref="PlaylistEntry.Id"/>), or removed if no such entry exists.
    /// </remarks>
    public async Task RemoveMediaFromPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, bool confirmContinueWatchingRemoval = false, CancellationToken cancellationToken = default)
    {
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        var normalizedMediaType = MediaHierarchyRegistry.ParseMediaType(mediaType).ToString();

        var entry = await _db.PlaylistEntries.FirstOrDefaultAsync(
            e => e.PlaylistId == playlistId && e.MediaType == normalizedMediaType && e.MediaId == mediaId, cancellationToken)
            ?? throw new KeyNotFoundException("Eintrag wurde nicht gefunden.");

        // Per user affected by this removal (the owner, and - on a public playlist - every viewer with a
        // playlist-bound continue-watching entry for this title): the next playable title THAT user may
        // access, resolved before the entry disappears (its position is looked up by its still-valid id).
        var continueWatchingService = _serviceProvider?.GetService<ContinueWatchingService>();
        var replacements = new List<(string UserId, PlaylistEntry? Next)>();
        if (continueWatchingService is not null && PlaylistEntryMediaTypeResolver.IsPlayable(normalizedMediaType))
        {
            var affectedUserIds = await continueWatchingService.GetUserIdsWithPlaylistBoundEntryAsync(
                playlistId, normalizedMediaType, mediaId, cancellationToken);

            // The Sicherheitsabfrage only ever concerns the OWNER's own continue-watching entry; viewers'
            // entries are resolved silently (they do not trigger the removal) and never surface here, so
            // nothing about other users leaks through the 409 response.
            if (affectedUserIds.Contains(userId) && !confirmContinueWatchingRemoval)
                throw new ContinueWatchingConfirmationRequiredException("Dieser Eintrag befindet sich in deiner Weiterschauen-Liste. Entfernen?");

            if (affectedUserIds.Count > 0)
            {
                var validEntries = await LoadValidPlaylistEntriesAsync(playlist, removeOrphans: true, cancellationToken);
                if (_db.Entry(entry).State == EntityState.Detached)
                    return; // The entry itself turned out to be an orphan and was just swept away.

                var sortedEntries = await SortPlaylistEntriesForModeAsync(playlist.SortMode, validEntries, cancellationToken);
                var removedIndex = sortedEntries.FindIndex(e => e.Id == entry.Id);
                var survivors = validEntries.Where(e => e.Id != entry.Id).ToList();
                replacements = await ResolveNextEntriesPerUserAsync(
                    sortedEntries, removedIndex, survivors, affectedUserIds, new Dictionary<string, Dictionary<PlaylistEntry, bool>>(), cancellationToken);
            }
        }

        await RecordExclusionAsync(playlistId, normalizedMediaType, mediaId, cancellationToken);
        _db.PlaylistEntries.Remove(entry);
        await _db.SaveChangesAsync(cancellationToken);
        await _genreService.RecomputeGenresAsync(playlist, cancellationToken);

        foreach (var (affectedUserId, nextEntry) in replacements)
        {
            await continueWatchingService!.ResolvePlaylistEntryRemovalAsync(
                playlistId, affectedUserId, normalizedMediaType, mediaId, nextEntry?.MediaType, nextEntry?.MediaId, cancellationToken);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Sorted the same way as <see cref="GetPlaylistEntriesPagedAsync"/> (via <see cref="SortPlaylistEntriesForModeAsync"/>):
    /// previously this method returned entries in unspecified (insertion) order regardless of
    /// <see cref="Data.Playlist.SortMode"/>, while the paged variant already sorted - an inconsistency
    /// between the two "read entries" endpoints with no reason for callers to expect a different order
    /// depending on which one they call.
    /// </remarks>
    public async Task<DtoPlaylistEntry[]> GetPlaylistEntriesAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
    {
        var (playlist, isOwner) = await GetReadablePlaylistAsync(playlistId, userId, cancellationToken);

        var validEntries = await LoadValidPlaylistEntriesAsync(playlist, removeOrphans: isOwner, cancellationToken);
        var sortedEntries = await SortPlaylistEntriesForModeAsync(playlist.SortMode, validEntries, cancellationToken);
        return await BuildEntryDtosAsync(sortedEntries, userId, cancellationToken);
    }

    /// <summary>
    /// Loads a playlist for a READ operation: allowed for the owner and - while <see cref="Playlist.IsPublic"/>
    /// is set - for any other user (Entwicklungsschritt 11). Every mutating operation must use
    /// <see cref="GetOwnedPlaylistAsync"/> instead.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The playlist and whether <paramref name="userId"/> is its owner.</returns>
    /// <exception cref="KeyNotFoundException">The playlist does not exist.</exception>
    /// <exception cref="PlaylistAccessDeniedException">The playlist is private and owned by somebody else.</exception>
    private async Task<(Playlist Playlist, bool IsOwner)> GetReadablePlaylistAsync(long playlistId, string userId, CancellationToken cancellationToken)
    {
        var playlist = await _db.Playlists.FirstOrDefaultAsync(p => p.Id == playlistId, cancellationToken)
            ?? throw new KeyNotFoundException("Playlist wurde nicht gefunden.");

        EnsureReadable(playlist, userId);

        return (playlist, playlist.UserId == userId);
    }

    private static void EnsureReadable(Playlist playlist, string userId)
    {
        if (playlist.UserId != userId && !playlist.IsPublic)
            throw new PlaylistAccessDeniedException("Sie haben keinen Zugriff auf diese Playlist.");
    }

    private async Task<Playlist> GetOwnedPlaylistAsync(long playlistId, string userId, CancellationToken cancellationToken)
    {
        var playlist = await _db.Playlists.FirstOrDefaultAsync(p => p.Id == playlistId, cancellationToken)
            ?? throw new KeyNotFoundException("Playlist wurde nicht gefunden.");

        if (playlist.UserId != userId)
            throw new PlaylistAccessDeniedException("Sie haben keinen Zugriff auf diese Playlist.");

        return playlist;
    }

    private static string ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Playlist-Name ist erforderlich.");

        var trimmedName = name.Trim();
        if (trimmedName.Length > Playlist.NameMaxLength)
            throw new InvalidOperationException($"Name darf maximal {Playlist.NameMaxLength} Zeichen lang sein.");

        return trimmedName;
    }

    private static string? ValidateDescription(string? description)
    {
        if (string.IsNullOrEmpty(description))
            return null;

        if (description.Length > Playlist.DescriptionMaxLength)
            throw new InvalidOperationException($"Beschreibung darf maximal {Playlist.DescriptionMaxLength} Zeichen lang sein.");

        return description;
    }

    private async Task EnsureNameNotDuplicateAsync(string userId, string name, long? excludePlaylistId, CancellationToken cancellationToken)
    {
        var isDuplicate = await _db.Playlists
            .AsNoTracking()
            .AnyAsync(p => p.UserId == userId && p.Id != excludePlaylistId && p.Name == name, cancellationToken);
        if (isDuplicate)
            throw new PlaylistNameAlreadyExistsException("Ein Playlist mit diesem Namen existiert bereits.");
    }

    private static PlaylistSortMode ParseSortMode(string? sortMode, PlaylistSortMode fallback)
    {
        if (string.IsNullOrWhiteSpace(sortMode))
            return fallback;

        return ParseSortModeOrThrow(sortMode);
    }

    /// <summary>
    /// Parses a raw sort mode string into a <see cref="PlaylistSortMode"/>, throwing when it is missing
    /// or not one of the known values. Used directly by <see cref="ChangeSortModeAsync"/> (where a sort
    /// mode is always required, so there is no fallback to apply) and via <see cref="ParseSortMode"/> by
    /// <see cref="CreatePlaylistAsync"/> (where an absent value falls back to a default instead of erroring).
    /// </summary>
    /// <param name="sortMode">The raw sort mode string to parse.</param>
    /// <returns>The parsed <see cref="PlaylistSortMode"/>.</returns>
    private static PlaylistSortMode ParseSortModeOrThrow(string sortMode)
    {
        if (!Enum.TryParse<PlaylistSortMode>(sortMode, ignoreCase: true, out var parsed))
            throw new ArgumentException("Ungültiger Sortiermodus-Wert.");

        return parsed;
    }

    /// <summary>
    /// Converts a single <see cref="Playlist"/> to its DTO, including its genres. Thin wrapper around
    /// <see cref="ToDtosAsync"/> for the many call sites that only ever have one playlist at hand.
    /// </summary>
    /// <param name="playlist">The playlist to convert.</param>
    /// <param name="requesterId">The id of the requesting user (decides the requester-scoped fields, see <see cref="ToDto"/>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The converted DTO.</returns>
    private async Task<DtoPlaylist> ToDtoAsync(Playlist playlist, string requesterId, CancellationToken cancellationToken)
    {
        var dtos = await ToDtosAsync(new List<Playlist> { playlist }, requesterId, cancellationToken);
        return dtos[0];
    }

    /// <summary>
    /// Converts the given playlists to their DTOs, bulk-loading every playlist's <see cref="PlaylistGenre"/>
    /// rows in a single query (via <see cref="PlaylistGenreService.LoadPlaylistGenresAsync"/>) rather than
    /// one query per playlist - matters for <see cref="GetPlaylistsAsync"/>, the one caller that converts
    /// more than a single playlist at once.
    /// </summary>
    /// <param name="playlists">The playlists to convert.</param>
    /// <param name="requesterId">The id of the requesting user (decides the requester-scoped fields, see <see cref="ToDto"/>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The converted DTOs, in the same order as <paramref name="playlists"/>.</returns>
    private async Task<DtoPlaylist[]> ToDtosAsync(List<Playlist> playlists, string requesterId, CancellationToken cancellationToken)
    {
        var playlistIds = playlists.Select(p => p.Id).ToList();
        var genresByPlaylist = await _genreService.LoadPlaylistGenresAsync(playlistIds, cancellationToken);

        return playlists.Select(playlist => ToDto(playlist, genresByPlaylist, requesterId)).ToArray();
    }

    /// <summary>
    /// Converts a single <see cref="Playlist"/> to its DTO using already-loaded genre data, resolving
    /// <see cref="DtoPlaylist.Genres"/> (frequency descending, name ascending as tie-break, capped to
    /// <see cref="Playlist.MaxDisplayedGenres"/> - a purely presentational limit) and
    /// <see cref="DtoPlaylist.AllGenreIds"/> (every derived/assigned genre id, uncapped, used to prefill
    /// the manual-override editor) from it.
    /// </summary>
    /// <remarks>
    /// The DTO is scoped to the requester (Entwicklungsschritt 11): a viewer who is not the owner of a public
    /// playlist only receives what is needed to view and play it - <see cref="DtoPlaylist.IsOwner"/> is
    /// <see langword="false"/> and the owner-only editing state (<see cref="DtoPlaylist.AllGenreIds"/>,
    /// <see cref="DtoPlaylist.GenresManuallyOverridden"/>, <see cref="DtoPlaylist.CoverPictureIsUserUploaded"/>)
    /// is withheld. The DTO never contains the owner's user id.
    /// </remarks>
    /// <param name="playlist">The playlist to convert.</param>
    /// <param name="genresByPlaylist">Every playlist's genre rows, keyed by playlist id (see <see cref="ToDtosAsync"/>).</param>
    /// <param name="requesterId">The id of the requesting user.</param>
    /// <returns>The converted DTO.</returns>
    private static DtoPlaylist ToDto(Playlist playlist, Dictionary<long, List<(long GenreId, string GenreName, int Count)>> genresByPlaylist, string requesterId)
    {
        var isOwner = playlist.UserId == requesterId;
        var genreRows = genresByPlaylist.TryGetValue(playlist.Id, out var rows) ? rows : new List<(long GenreId, string GenreName, int Count)>();

        var displayGenres = genreRows
            .OrderByDescending(r => r.Count)
            .ThenBy(r => r.GenreName, StringComparer.OrdinalIgnoreCase)
            .Take(Playlist.MaxDisplayedGenres)
            .Select(r => new DtoGenreOption { Id = r.GenreId, Name = r.GenreName })
            .ToArray();

        return new DtoPlaylist
        {
            Id = playlist.Id,
            Name = playlist.Name,
            Description = playlist.Description,
            SortMode = playlist.SortMode.ToString(),
            CreatedAt = playlist.CreatedAt,
            UpdatedAt = playlist.UpdatedAt,
            Genres = displayGenres,
            AllGenreIds = isOwner ? genreRows.Select(r => r.GenreId).ToArray() : Array.Empty<long>(),
            GenresManuallyOverridden = isOwner && playlist.GenresManuallyOverridden,
            CoverPictureId = playlist.CoverPictureId,
            CoverPictureIsUserUploaded = isOwner && playlist.CoverPictureIsUserUploaded,
            IsPublic = playlist.IsPublic,
            IsOwner = isOwner
        };
    }

    private readonly record struct MediaRef(string MediaType, long MediaId);

    /// <summary>
    /// Builds the top-level entry and cascade-child entries (if any) to add for a single "add media to
    /// playlist" request, skipping any that already exist in the playlist. Split into
    /// <see cref="LoadExistingMediaRefsAsync"/>, <see cref="BuildTopLevelEntry"/> and
    /// <see cref="BuildCascadeEntries"/> so each step stays short and independently readable.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="parsedMediaType">The parsed media type being added.</param>
    /// <param name="normalizedMediaType">The string form of <paramref name="parsedMediaType"/>.</param>
    /// <param name="mediaId">The media identifier being added.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="EntriesToAdd">(Return tuple field.) The entries to add.</param>
    /// <param name="SkippedDuplicateCount">(Return tuple field.) How many requested entries were skipped as duplicates.</param>
    /// <param name="TopLevelEntry">(Return tuple field.) The top-level entry, if it was not itself a duplicate.</param>
    /// <returns>
    /// The entries to add, how many requested entries were skipped as duplicates, the top-level entry
    /// (if it was not itself a duplicate), and the playlist's existing entry count.
    /// </returns>
    private async Task<(List<PlaylistEntry> EntriesToAdd, int SkippedDuplicateCount, PlaylistEntry? TopLevelEntry, int ExistingEntryCount)> BuildEntriesToAddAsync(
        long playlistId, MediaType parsedMediaType, string normalizedMediaType, long mediaId, CancellationToken cancellationToken)
    {
        var existingKeys = await LoadExistingMediaRefsAsync(playlistId, cancellationToken);
        var now = DateTime.UtcNow;

        var (topLevelEntry, topLevelSkippedCount) = BuildTopLevelEntry(playlistId, normalizedMediaType, mediaId, existingKeys, now);

        var entriesToAdd = new List<PlaylistEntry>();
        if (topLevelEntry is not null)
            entriesToAdd.Add(topLevelEntry);

        var cascadeMediaRefs = await GetCascadeMediaIdsAsync(parsedMediaType, mediaId, cancellationToken);
        var (cascadeEntries, cascadeSkippedCount) = BuildCascadeEntries(playlistId, normalizedMediaType, mediaId, cascadeMediaRefs, existingKeys, now);
        entriesToAdd.AddRange(cascadeEntries);

        return (entriesToAdd, topLevelSkippedCount + cascadeSkippedCount, topLevelEntry, existingKeys.Count);
    }

    private async Task<HashSet<MediaRef>> LoadExistingMediaRefsAsync(long playlistId, CancellationToken cancellationToken)
    {
        var rows = await _db.PlaylistEntries
            .AsNoTracking()
            .Where(e => e.PlaylistId == playlistId)
            .Select(e => new { e.MediaType, e.MediaId })
            .ToListAsync(cancellationToken);

        return rows.Select(e => new MediaRef(e.MediaType, e.MediaId)).ToHashSet();
    }

    /// <summary>
    /// Builds the top-level entry for an "add media to playlist" request, or <c>null</c> (with a skipped
    /// count of 1) if that media reference is already in the playlist.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="normalizedMediaType">The string form of the media type being added.</param>
    /// <param name="mediaId">The media identifier being added.</param>
    /// <param name="existingKeys">The (media type, media id) references already in the playlist.</param>
    /// <param name="now">The timestamp to stamp the new entry's <see cref="PlaylistEntry.AddedAt"/> with.</param>
    /// <param name="Entry">(Return tuple field.) The built entry, or <c>null</c> if it was a duplicate.</param>
    /// <returns>The built entry (or <c>null</c> if it was a duplicate) and how many were skipped.</returns>
    private static (PlaylistEntry? Entry, int SkippedCount) BuildTopLevelEntry(
        long playlistId, string normalizedMediaType, long mediaId, HashSet<MediaRef> existingKeys, DateTime now)
    {
        if (existingKeys.Contains(new MediaRef(normalizedMediaType, mediaId)))
            return (null, 1);

        var entry = new PlaylistEntry
        {
            PlaylistId = playlistId,
            MediaType = normalizedMediaType,
            MediaId = mediaId,
            ParentMediaType = null,
            ParentMediaId = null,
            AddedAt = now
        };
        return (entry, 0);
    }

    /// <summary>
    /// Builds the cascade-child entries (e.g. a TV show's episodes) for an "add media to playlist"
    /// request, skipping any that already exist in the playlist.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="normalizedMediaType">The string form of the parent media type being added.</param>
    /// <param name="mediaId">The parent media identifier being added.</param>
    /// <param name="cascadeMediaRefs">The cascade-child (media type, media id) references to build entries for.</param>
    /// <param name="existingKeys">The (media type, media id) references already in the playlist.</param>
    /// <param name="now">The timestamp to stamp the new entries' <see cref="PlaylistEntry.AddedAt"/> with.</param>
    /// <param name="Entries">(Return tuple field.) The built cascade-child entries.</param>
    /// <returns>The built cascade-child entries and how many were skipped as duplicates.</returns>
    private static (List<PlaylistEntry> Entries, int SkippedCount) BuildCascadeEntries(
        long playlistId, string normalizedMediaType, long mediaId, IEnumerable<MediaRef> cascadeMediaRefs, HashSet<MediaRef> existingKeys, DateTime now)
    {
        var entries = new List<PlaylistEntry>();
        var skippedCount = 0;

        foreach (var cascadeEntry in cascadeMediaRefs)
        {
            if (existingKeys.Contains(cascadeEntry))
            {
                skippedCount++;
                continue;
            }

            entries.Add(new PlaylistEntry
            {
                PlaylistId = playlistId,
                MediaType = cascadeEntry.MediaType,
                MediaId = cascadeEntry.MediaId,
                ParentMediaType = normalizedMediaType,
                ParentMediaId = mediaId,
                AddedAt = now
            });
        }

        return (entries, skippedCount);
    }

    private async Task<DtoPlaylistAddResult> BuildAddResultAsync(
        List<PlaylistEntry> entriesToAdd, int skippedDuplicateCount, PlaylistEntry? topLevelEntry, string userId, CancellationToken cancellationToken)
    {
        var addedEntries = await BuildEntryDtosAsync(entriesToAdd, userId, cancellationToken);

        var message = entriesToAdd.Count switch
        {
            > 0 when skippedDuplicateCount > 0 =>
                $"{entriesToAdd.Count} Titel hinzugefügt, {skippedDuplicateCount} bereits vorhanden und übersprungen.",
            > 0 =>
                $"{entriesToAdd.Count} Titel hinzugefügt.",
            _ =>
                $"Alle {skippedDuplicateCount} Titel waren bereits vorhanden."
        };

        return new DtoPlaylistAddResult
        {
            TopLevelEntry = topLevelEntry is null ? null : addedEntries[entriesToAdd.IndexOf(topLevelEntry)],
            AddedEntries = addedEntries,
            SkippedDuplicateCount = skippedDuplicateCount,
            Message = message
        };
    }

    private async Task<IEnumerable<MediaRef>> GetCascadeMediaIdsAsync(MediaType mediaType, long mediaId, CancellationToken cancellationToken)
    {
        if (!MediaHierarchyRegistry.Handlers.TryGetValue(mediaType, out var handler) || handler.LoadCascadeChildrenAsync is null)
            return Enumerable.Empty<MediaRef>();

        var children = await handler.LoadCascadeChildrenAsync(_db, mediaId, cancellationToken);
        return children.Select(c => new MediaRef(c.MediaType.ToString(), c.MediaId));
    }

    private async Task<string?> GetMediaTitleAsync(string mediaType, long mediaId, CancellationToken cancellationToken)
    {
        var titles = await GetMediaTitlesAsync(mediaType, new[] { mediaId }, cancellationToken);
        return titles.TryGetValue(mediaId, out var title) ? title : null;
    }

    private async Task<Dictionary<long, string>> GetMediaTitlesAsync(string mediaType, IReadOnlyCollection<long> mediaIds, CancellationToken cancellationToken)
    {
        if (mediaIds.Count == 0 || !MediaHierarchyRegistry.TryParseKnownMediaType(mediaType, out var parsedType))
            return new Dictionary<long, string>();

        var handler = MediaHierarchyRegistry.Handlers[parsedType];
        return await handler.LoadTitlesAsync(_db, mediaIds, cancellationToken);
    }

    /// <summary>
    /// Checks which of the given media ids of the given type still reference existing media, without
    /// resolving their titles. Used for orphan detection, where the title text itself is not needed.
    /// </summary>
    /// <param name="mediaType">The media type of the ids.</param>
    /// <param name="mediaIds">The media ids to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The subset of <paramref name="mediaIds"/> that still reference existing media.</returns>
    private async Task<HashSet<long>> GetExistingMediaIdsAsync(string mediaType, IReadOnlyCollection<long> mediaIds, CancellationToken cancellationToken)
    {
        if (mediaIds.Count == 0 || !MediaHierarchyRegistry.TryParseKnownMediaType(mediaType, out var parsedType))
            return new HashSet<long>();

        var handler = MediaHierarchyRegistry.Handlers[parsedType];
        return await handler.LoadExistingIdsAsync(_db, mediaIds, cancellationToken);
    }

    /// <summary>
    /// Builds the <see cref="DtoPlaylistEntry"/> array for the given entries, resolving titles, resolved
    /// picture ids and unlock/access status in bulk (a handful of queries scaled to media type, not to
    /// the number of entries), analogous to <see cref="LoadTitlesForMediaRefsAsync"/>. The entries' own
    /// (media type, media id) ids are grouped by type exactly once (<paramref name="entries"/> is scanned
    /// a single time via <c>ownIdsByType</c>) and that grouping is reused for titles, picture ids and both
    /// access-resolver lookups, instead of each of those four steps re-grouping the same entries itself.
    /// </summary>
    /// <param name="entries">The playlist entries to convert.</param>
    /// <param name="userId">The id of the user the accessibility check is performed for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resulting <see cref="DtoPlaylistEntry"/> array, in the same order as <paramref name="entries"/>.</returns>
    private async Task<DtoPlaylistEntry[]> BuildEntryDtosAsync(List<PlaylistEntry> entries, string userId, CancellationToken cancellationToken)
    {
        var ownIdsByType = MediaHierarchyRegistry.GroupMediaIdsByType(entries);

        var titlesByType = await LoadTitlesForIdsByTypeAsync(ownIdsByType, cancellationToken);
        var parentTitlesByType = await LoadTitlesForMediaRefsAsync(GetParentMediaRefs(entries), cancellationToken);
        var pictureIdsByType = await LoadPictureIdsForIdsByTypeAsync(ownIdsByType, cancellationToken);
        var accessibilityByEntry = await _accessResolver.ResolveAccessibilityAsync(entries, userId, ownIdsByType, cancellationToken);

        var dtos = new DtoPlaylistEntry[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];

            dtos[i] = new DtoPlaylistEntry
            {
                Id = entry.Id,
                PlaylistId = entry.PlaylistId,
                MediaType = entry.MediaType,
                MediaId = entry.MediaId,
                MediaTitle = GetMediaTitle(entry, titlesByType),
                ParentMediaType = entry.ParentMediaType,
                ParentMediaId = entry.ParentMediaId,
                ParentMediaTitle = GetParentTitle(entry, parentTitlesByType),
                AddedAt = entry.AddedAt,
                ResolvedPictureId = GetPictureId(entry, pictureIdsByType),
                IsAccessible = accessibilityByEntry[entry],
                SortOrder = entry.SortOrder
            };
        }

        return dtos;
    }

    /// <summary>
    /// Loads the resolved picture ids (poster, falling back to banner and then fanart) for the given
    /// (media type, media id) references, grouped by media type, scaled to only the references actually
    /// passed in (e.g. a single page of entries) rather than an entire playlist. Mirrors
    /// <see cref="LoadTitlesForMediaRefsAsync"/>.
    /// </summary>
    /// <param name="mediaRefs">The (media type, media id) references to resolve picture ids for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary of media type to a dictionary of media id to resolved picture id.</returns>
    private async Task<Dictionary<string, Dictionary<long, long?>>> LoadPictureIdsForMediaRefsAsync(
        IEnumerable<MediaRef> mediaRefs, CancellationToken cancellationToken)
        => await LoadPictureIdsForIdsByTypeAsync(GroupMediaIdsByType(mediaRefs), cancellationToken);

    /// <summary>
    /// Same as <see cref="LoadPictureIdsForMediaRefsAsync"/>, taking an already-grouped ids-by-type map
    /// instead of grouping the underlying references itself.
    /// </summary>
    /// <param name="idsByType">The media ids to resolve picture ids for, grouped by media type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary of media type to a dictionary of media id to resolved picture id.</returns>
    private async Task<Dictionary<string, Dictionary<long, long?>>> LoadPictureIdsForIdsByTypeAsync(
        Dictionary<string, HashSet<long>> idsByType, CancellationToken cancellationToken)
    {
        var pictureIdsByType = new Dictionary<string, Dictionary<long, long?>>();
        foreach (var (mediaType, mediaIds) in idsByType)
        {
            if (!MediaHierarchyRegistry.TryParseKnownMediaType(mediaType, out var parsedType))
                continue;

            var handler = MediaHierarchyRegistry.Handlers[parsedType];
            pictureIdsByType[mediaType] = await handler.LoadPictureIdsAsync(_db, mediaIds, cancellationToken);
        }

        return pictureIdsByType;
    }

    private static long? GetPictureId(PlaylistEntry entry, Dictionary<string, Dictionary<long, long?>> pictureIdsByType)
        => pictureIdsByType.TryGetValue(entry.MediaType, out var pictureIds) && pictureIds.TryGetValue(entry.MediaId, out var pictureId)
            ? pictureId
            : null;

    /// <inheritdoc />
    public async Task<DtoPlaylistEntriesPagedResult> GetPlaylistEntriesPagedAsync(long playlistId, string userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var (playlist, isOwner) = await GetReadablePlaylistAsync(playlistId, userId, cancellationToken);

        var validEntries = await LoadValidPlaylistEntriesAsync(playlist, removeOrphans: isOwner, cancellationToken);
        var sortedEntries = await SortPlaylistEntriesForModeAsync(playlist.SortMode, validEntries, cancellationToken);

        var totalCount = sortedEntries.Count;
        var skip = (pageNumber - 1) * pageSize;
        var pageEntries = sortedEntries.Skip(skip).Take(pageSize).ToList();

        // Title/picture/accessibility resolution is scaled to the returned page only (not the whole
        // playlist), since these are the only per-entry lookups that aren't already required for orphan
        // detection or sorting.
        var dtoEntries = await BuildEntryDtosAsync(pageEntries, userId, cancellationToken);

        return new DtoPlaylistEntriesPagedResult
        {
            Entries = dtoEntries,
            TotalCount = totalCount,
            HasNextPage = skip + pageSize < totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Sorts the given entries according to the playlist's sort mode: by release date (via
    /// <see cref="SortPlaylistEntriesByReleaseDateAsync"/>) for <see cref="PlaylistSortMode.ByReleaseDate"/>,
    /// or by <see cref="PlaylistEntry.SortOrder"/> (falling back to <see cref="PlaylistEntry.AddedAt"/> for
    /// entries without one) for <see cref="PlaylistSortMode.Manual"/>. <see cref="PlaylistSortMode"/> has
    /// exactly these two values, so a plain if/else covers every case without an unreachable branch.
    /// </summary>
    /// <param name="sortMode">The playlist's sort mode.</param>
    /// <param name="entries">The entries to sort.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entries in the order matching <paramref name="sortMode"/>.</returns>
    private async Task<List<PlaylistEntry>> SortPlaylistEntriesForModeAsync(PlaylistSortMode sortMode, List<PlaylistEntry> entries, CancellationToken cancellationToken)
    {
        if (sortMode == PlaylistSortMode.Manual)
            return entries.OrderBy(e => e.SortOrder).ThenBy(e => e.AddedAt).ToList();

        return await SortPlaylistEntriesByReleaseDateAsync(entries, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReorderPlaylistEntryAsync(long playlistId, string userId, long entryId, long newSortOrder, CancellationToken cancellationToken = default)
    {
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        await _reorderService.ReorderEntryAsync(playlist, entryId, newSortOrder, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DtoPlaylistEntry[]> BatchReorderPlaylistEntriesAsync(
        long playlistId, string userId, List<(long EntryId, long NewSortOrder)> reorderOperations, CancellationToken cancellationToken = default)
    {
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        var entries = await _reorderService.BatchReorderEntriesAsync(playlist, reorderOperations, cancellationToken);

        return await BuildEntryDtosAsync(entries, userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long?> GetMaxSortOrderAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        return await _reorderService.GetMaxSortOrderAsync(playlistId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DtoPlaylistEntry> MoveEntryToBeginningAsync(long playlistId, string userId, long entryId, CancellationToken cancellationToken = default)
    {
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        var entry = await _reorderService.MoveEntryToBeginningAsync(playlist, entryId, cancellationToken);

        var dtos = await BuildEntryDtosAsync(new List<PlaylistEntry> { entry }, userId, cancellationToken);
        return dtos[0];
    }

    /// <inheritdoc />
    public async Task MoveEntryBetweenAsync(long playlistId, string userId, long entryId, long targetSortOrder, CancellationToken cancellationToken = default)
    {
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        await _reorderService.MoveEntryBetweenAsync(playlist, entryId, targetSortOrder, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DtoPlaylist> ChangeSortModeAsync(long playlistId, string userId, string newSortMode, bool? confirmLossOfManualOrder, CancellationToken cancellationToken = default)
    {
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        var targetMode = ParseSortModeOrThrow(newSortMode);

        if (targetMode == PlaylistSortMode.Manual && playlist.SortMode != PlaylistSortMode.Manual)
        {
            var validEntries = await LoadValidPlaylistEntriesAsync(playlist, removeOrphans: true, cancellationToken);
            var sortedEntries = await SortPlaylistEntriesByReleaseDateAsync(validEntries, cancellationToken);

            for (var i = 0; i < sortedEntries.Count; i++)
                sortedEntries[i].SortOrder = i;
        }
        else if (targetMode == PlaylistSortMode.ByReleaseDate && playlist.SortMode == PlaylistSortMode.Manual)
        {
            if (confirmLossOfManualOrder != true)
                throw new ManualSortOrderConfirmationRequiredException("Die manuelle Reihenfolge geht beim Wechsel verloren.");

            var entries = await _db.PlaylistEntries.Where(e => e.PlaylistId == playlistId).ToListAsync(cancellationToken);
            foreach (var entry in entries)
                entry.SortOrder = null;
        }

        playlist.SortMode = targetMode;
        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(playlist, userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DtoPlaylist> SetPlaylistGenresAsync(long playlistId, string userId, long[] genreIds, CancellationToken cancellationToken = default)
    {
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        await _genreService.SetManualGenresAsync(playlist, genreIds, cancellationToken);

        return await ToDtoAsync(playlist, userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DtoPlaylist> ResetPlaylistGenresAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
    {
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        await _genreService.ResetGenresAsync(playlist, cancellationToken);

        return await ToDtoAsync(playlist, userId, cancellationToken);
    }

    /// <summary>
    /// Loads all <see cref="PlaylistEntry"/> rows of a playlist, removing (and persisting the removal of)
    /// orphaned entries whose referenced media no longer exists. Only checks for existence of the referenced
    /// media (not its title), so the cost of this call does not depend on how many entries are actually
    /// going to be displayed by the caller.
    /// </summary>
    /// <remarks>
    /// Before persisting the orphan removal, resolves any <see cref="Data.ContinueWatchingEntry"/> bound to
    /// this playlist that references one of the orphaned entries (see
    /// <see cref="ResolveOrphanContinueWatchingReplacementsAsync"/>) - the "silent" counterpart to
    /// <see cref="RemoveMediaFromPlaylistAsync"/>'s user-confirmed removal: here the title disappeared from
    /// the media library itself (not a user-triggered removal), so no confirmation is asked, but the same
    /// replace-with-next-available-title-or-remove behavior applies.
    /// <para>
    /// In practice this only ever resolves to "no matching entry" for a <see cref="MediaTypeValues.Movie"/>
    /// or <see cref="MediaTypeValues.TVShowEpisode"/> orphan, because <c>ContinueWatchingEntryConfiguration</c>
    /// already declares a real foreign key with cascading delete from <c>ContinueWatchingEntry.MovieId</c>/
    /// <c>TVShowEpisodeId</c> to the underlying <c>Movies</c>/<c>TVShowEpisodes</c> row (pre-existing, from
    /// before this replace-with-next-available-title behavior was added): the moment that row is actually
    /// deleted, the database removes any continue-watching entry still referencing it immediately - well
    /// before this lazy, next-load orphan sweep (which exists only because <see cref="PlaylistEntry"/> has
    /// no such real foreign key to the media it references) gets a chance to look for one to replace. This
    /// code path is kept regardless, both for correctness should that cascade ever not apply and as the
    /// single, symmetric implementation of the "silent" replace-or-remove rule described by the requirement
    /// text; changing the cascading-delete behavior itself, to make the replace branch reachable for this
    /// case too, was judged out of scope here (it would affect continue-watching cleanup for every movie/
    /// episode deletion in the application, not just the playlist-bound case).
    /// </para>
    /// </remarks>
    /// <param name="playlist">The playlist (its <see cref="Playlist.Id"/> and <see cref="Playlist.SortMode"/>
    /// are used to resolve entries and continue-watching replacements).</param>
    /// <param name="removeOrphans">
    /// Whether orphaned entries are also deleted (and continue-watching/genres updated accordingly).
    /// Entwicklungsschritt 11 decision: <see langword="true"/> only when the OWNER is the requester. A viewer
    /// of a public playlist merely does not see orphaned entries - a read-only access must not modify the
    /// owner's playlist (or, through the replacement logic, other users' continue-watching entries); the
    /// owner's next access performs the cleanup as before.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The playlist entries whose referenced media still exists.</returns>
    private async Task<List<PlaylistEntry>> LoadValidPlaylistEntriesAsync(Playlist playlist, bool removeOrphans, CancellationToken cancellationToken)
    {
        var entries = await _db.PlaylistEntries
            .Where(e => e.PlaylistId == playlist.Id)
            .ToListAsync(cancellationToken);

        var mediaIdsByType = MediaHierarchyRegistry.GroupMediaIdsByType(entries);

        var existingIdsByType = new Dictionary<string, HashSet<long>>();
        foreach (var (mediaType, mediaIds) in mediaIdsByType)
            existingIdsByType[mediaType] = await GetExistingMediaIdsAsync(mediaType, mediaIds, cancellationToken);

        var validEntries = new List<PlaylistEntry>(entries.Count);
        var orphans = new List<PlaylistEntry>();

        foreach (var entry in entries)
        {
            if (existingIdsByType.TryGetValue(entry.MediaType, out var ids) && ids.Contains(entry.MediaId))
                validEntries.Add(entry);
            else
                orphans.Add(entry);
        }

        if (orphans.Count > 0 && removeOrphans)
        {
            var continueWatchingService = _serviceProvider?.GetService<ContinueWatchingService>();
            if (continueWatchingService is not null)
                await ResolveOrphanContinueWatchingReplacementsAsync(playlist, entries, orphans, validEntries, continueWatchingService, cancellationToken);

            _db.PlaylistEntries.RemoveRange(orphans);
            await _db.SaveChangesAsync(cancellationToken);
            await _genreService.RecomputeGenresAsync(playlist, cancellationToken);
        }

        return validEntries;
    }

    /// <summary>
    /// Resolves continue-watching replacements for orphaned entries that just disappeared from the media
    /// library (see <see cref="LoadValidPlaylistEntriesAsync"/>), the silent counterpart of
    /// <see cref="RemoveMediaFromPlaylistAsync"/>'s confirmed-removal path: for every orphan that is
    /// directly playable (<see cref="PlaylistEntryMediaTypeResolver.IsPlayable"/> - a continue-watching
    /// entry never references a collection entry), finds the next playable and accessible entry that
    /// follows it in the playlist's current sort order among the entries that are <b>not</b> themselves
    /// orphaned (<paramref name="validEntries"/>), and asks <paramref name="continueWatchingService"/> to
    /// replace (or remove, if none) the matching continue-watching entry. Sorts <paramref name="allEntries"/>
    /// (orphans included) once up front to establish each orphan's original position, rather than calling
    /// <see cref="FindAdjacentPlayableEntryAsync"/> per orphan: that method locates its starting point by
    /// <see cref="PlaylistEntry.Id"/> via a fresh <see cref="LoadValidPlaylistEntriesAsync"/> call, which
    /// would no longer find an already-orphaned entry (it is excluded from <c>validEntries</c> by
    /// definition), and would also try to persist the very same orphan removal this method is already in
    /// the middle of - reusing it here would be both incorrect and reentrant.
    /// </summary>
    /// <param name="playlist">The owning playlist.</param>
    /// <param name="allEntries">Every entry of the playlist (valid and orphaned), before orphan removal.</param>
    /// <param name="orphans">The subset of <paramref name="allEntries"/> whose referenced media no longer exists.</param>
    /// <param name="validEntries">The subset of <paramref name="allEntries"/> that is staying (not orphaned).</param>
    /// <param name="continueWatchingService">The service used to resolve each orphan's continue-watching replacement.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task ResolveOrphanContinueWatchingReplacementsAsync(
        Playlist playlist, List<PlaylistEntry> allEntries, List<PlaylistEntry> orphans, List<PlaylistEntry> validEntries,
        ContinueWatchingService continueWatchingService, CancellationToken cancellationToken)
    {
        var playableOrphans = orphans.Where(o => PlaylistEntryMediaTypeResolver.IsPlayable(o.MediaType)).ToList();
        if (playableOrphans.Count == 0)
            return;

        var fullSortedEntries = await SortPlaylistEntriesForModeAsync(playlist.SortMode, allEntries, cancellationToken);
        var accessibilityCache = new Dictionary<string, Dictionary<PlaylistEntry, bool>>();

        foreach (var orphan in playableOrphans)
        {
            // Entwicklungsschritt 11: every user with a playlist-bound entry for the vanished title (the
            // owner and, on a public playlist, viewers), each with the next title THEY may access.
            var affectedUserIds = await continueWatchingService.GetUserIdsWithPlaylistBoundEntryAsync(
                playlist.Id, orphan.MediaType, orphan.MediaId, cancellationToken);
            if (affectedUserIds.Count == 0)
                continue;

            var orphanIndex = fullSortedEntries.FindIndex(e => e.Id == orphan.Id);
            if (orphanIndex < 0)
                continue;

            var replacements = await ResolveNextEntriesPerUserAsync(
                fullSortedEntries, orphanIndex, validEntries, affectedUserIds, accessibilityCache, cancellationToken);

            foreach (var (affectedUserId, nextEntry) in replacements)
            {
                await continueWatchingService.ResolvePlaylistEntryRemovalAsync(
                    playlist.Id, affectedUserId, orphan.MediaType, orphan.MediaId, nextEntry?.MediaType, nextEntry?.MediaId, cancellationToken);
            }
        }
    }

    /// <summary>
    /// For each of <paramref name="userIds"/>, finds the next playable entry after position
    /// <paramref name="removedIndex"/> in <paramref name="sortedEntries"/> that survives the removal
    /// (<paramref name="survivors"/>) AND is accessible to that user - accessibility is always resolved for
    /// the user whose continue-watching entry is being replaced (owner or viewer), never for the owner on
    /// behalf of others: a viewer must not be pointed at a title only the owner has unlocked.
    /// </summary>
    /// <param name="sortedEntries">Every entry of the playlist (the removed one included), in sort order.</param>
    /// <param name="removedIndex">The position of the removed entry within <paramref name="sortedEntries"/>.</param>
    /// <param name="survivors">The entries that remain after the removal.</param>
    /// <param name="userIds">The users whose continue-watching entry must be replaced.</param>
    /// <param name="accessibilityCache">Per-user accessibility of <paramref name="survivors"/>, filled lazily and reused across calls.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Per user the entry to replace with, or <see langword="null"/> when no such entry exists.</returns>
    private async Task<List<(string UserId, PlaylistEntry? Next)>> ResolveNextEntriesPerUserAsync(
        List<PlaylistEntry> sortedEntries, int removedIndex, List<PlaylistEntry> survivors, IEnumerable<string> userIds,
        Dictionary<string, Dictionary<PlaylistEntry, bool>> accessibilityCache, CancellationToken cancellationToken)
    {
        var survivorSet = new HashSet<PlaylistEntry>(survivors);
        var result = new List<(string UserId, PlaylistEntry? Next)>();

        foreach (var userId in userIds)
        {
            if (removedIndex < 0)
            {
                result.Add((userId, null));
                continue;
            }

            if (!accessibilityCache.TryGetValue(userId, out var accessibility))
            {
                var idsByType = MediaHierarchyRegistry.GroupMediaIdsByType(survivors);
                accessibility = await _accessResolver.ResolveAccessibilityAsync(survivors, userId, idsByType, cancellationToken);
                accessibilityCache[userId] = accessibility;
            }

            PlaylistEntry? next = null;
            for (var i = removedIndex + 1; i < sortedEntries.Count; i++)
            {
                var candidate = sortedEntries[i];
                if (!survivorSet.Contains(candidate) || !PlaylistEntryMediaTypeResolver.IsPlayable(candidate.MediaType))
                    continue;

                if (accessibility.TryGetValue(candidate, out var isAccessible) && isAccessible)
                {
                    next = candidate;
                    break;
                }
            }

            result.Add((userId, next));
        }

        return result;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Structurally mirrors <see cref="ResolveOrphanContinueWatchingReplacementsAsync"/> (same per-playlist
    /// sort + accessibility resolution, then a forward search for the next playable and accessible entry),
    /// but is driven by <see cref="Data.ContinueWatchingEntry"/> rows rather than by already-orphaned
    /// <see cref="PlaylistEntry"/> rows: at the point this runs (before <c>DeleteMediaSourceAsync</c> has
    /// deleted anything), every entry referencing the doomed source's media is still a completely ordinary,
    /// non-orphaned <see cref="PlaylistEntry"/> - what makes it "about to be removed" here is that its media
    /// belongs to <paramref name="mediaSourceId"/>, not that it fails an existence check. The "next
    /// available title" candidate set is therefore every playlist entry that does <b>not</b> itself belong to
    /// <paramref name="mediaSourceId"/> (<c>survivorEntries</c>/<c>survivorSet</c> below), mirroring
    /// <c>validEntries</c>/<c>validSet</c> there. Only playlists that actually have at least one
    /// playlist-bound continue-watching entry pointing at the doomed source are touched (bulk-resolved via
    /// a single query up front, then grouped by playlist), so the cost of this method scales with the number
    /// of affected continue-watching entries, not with the size of the media source being deleted.
    /// </remarks>
    public async Task ResolvePlaylistBoundContinueWatchingReplacementsForSourceDeletionAsync(
        long mediaSourceId, CancellationToken cancellationToken = default)
    {
        var continueWatchingService = _serviceProvider?.GetService<ContinueWatchingService>();
        if (continueWatchingService is null)
            return;

        var affectedEntries = await _db.ContinueWatchingEntries
            .Where(cwe => cwe.PlaylistId != null &&
                          ((cwe.MovieId != null && cwe.Movie!.MediaSourceId == mediaSourceId) ||
                           (cwe.TVShowEpisodeId != null && cwe.TVShowEpisode!.TVShowSeason.TVShow.MediaSourceId == mediaSourceId)))
            .Select(cwe => new { cwe.UserId, PlaylistId = cwe.PlaylistId!.Value, cwe.MovieId, cwe.TVShowEpisodeId })
            .ToListAsync(cancellationToken);

        if (affectedEntries.Count == 0)
            return;

        // Every movie/episode belonging to the doomed source, so candidates for "next available title" can
        // exclude them: all of them disappear together with the source, so none is actually available.
        var movieIdsInSource = (await _db.Movies.AsNoTracking()
            .Where(m => m.MediaSourceId == mediaSourceId)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken)).ToHashSet();

        var episodeIdsInSource = (await _db.TVShowEpisodes.AsNoTracking()
            .Where(e => e.TVShowSeason.TVShow.MediaSourceId == mediaSourceId)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken)).ToHashSet();

        bool BelongsToDoomedSource(string mediaType, long mediaId) =>
            (string.Equals(mediaType, MediaTypeValues.Movie, StringComparison.OrdinalIgnoreCase) && movieIdsInSource.Contains(mediaId)) ||
            (string.Equals(mediaType, MediaTypeValues.TVShowEpisode, StringComparison.OrdinalIgnoreCase) && episodeIdsInSource.Contains(mediaId));

        foreach (var group in affectedEntries.GroupBy(e => e.PlaylistId))
        {
            var playlistId = group.Key;
            var playlist = await _db.Playlists.AsNoTracking().FirstOrDefaultAsync(p => p.Id == playlistId, cancellationToken);
            if (playlist is null)
                continue;

            var allEntries = await _db.PlaylistEntries.Where(e => e.PlaylistId == playlistId).ToListAsync(cancellationToken);
            var fullSortedEntries = await SortPlaylistEntriesForModeAsync(playlist.SortMode, allEntries, cancellationToken);

            var survivorEntries = allEntries.Where(e => !BelongsToDoomedSource(e.MediaType, e.MediaId)).ToList();
            var accessibilityCache = new Dictionary<string, Dictionary<PlaylistEntry, bool>>();

            // One row per (user, title): on a public playlist several users may have a playlist-bound entry
            // for the same doomed title, each replaced with the next title THAT user may access.
            foreach (var affected in group)
            {
                var (removedMediaType, removedMediaId) = affected.MovieId is not null
                    ? (MediaTypeValues.Movie, affected.MovieId.Value)
                    : (MediaTypeValues.TVShowEpisode, affected.TVShowEpisodeId!.Value);

                var doomedIndex = fullSortedEntries.FindIndex(e =>
                    string.Equals(e.MediaType, removedMediaType, StringComparison.OrdinalIgnoreCase) && e.MediaId == removedMediaId);
                if (doomedIndex < 0)
                    continue;

                var replacements = await ResolveNextEntriesPerUserAsync(
                    fullSortedEntries, doomedIndex, survivorEntries, new[] { affected.UserId }, accessibilityCache, cancellationToken);

                foreach (var (affectedUserId, nextEntry) in replacements)
                {
                    await continueWatchingService.ResolvePlaylistEntryRemovalAsync(
                        playlistId, affectedUserId, removedMediaType, removedMediaId, nextEntry?.MediaType, nextEntry?.MediaId, cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Loads the media titles for the given (media type, media id) references, grouped by media type.
    /// Used both to resolve entries' own titles and the titles of their parents, scaled to only the
    /// references actually passed in (e.g. a single page of entries) rather than an entire playlist.
    /// </summary>
    /// <param name="mediaRefs">The (media type, media id) references to resolve titles for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary of media type to a dictionary of media id to title.</returns>
    private async Task<Dictionary<string, Dictionary<long, string>>> LoadTitlesForMediaRefsAsync(
        IEnumerable<MediaRef> mediaRefs, CancellationToken cancellationToken)
        => await LoadTitlesForIdsByTypeAsync(GroupMediaIdsByType(mediaRefs), cancellationToken);

    /// <summary>
    /// Same as <see cref="LoadTitlesForMediaRefsAsync"/>, taking an already-grouped ids-by-type map
    /// instead of grouping the underlying references itself.
    /// </summary>
    /// <param name="idsByType">The media ids to resolve titles for, grouped by media type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary of media type to a dictionary of media id to title.</returns>
    private async Task<Dictionary<string, Dictionary<long, string>>> LoadTitlesForIdsByTypeAsync(
        Dictionary<string, HashSet<long>> idsByType, CancellationToken cancellationToken)
    {
        var titlesByType = new Dictionary<string, Dictionary<long, string>>();
        foreach (var (mediaType, mediaIds) in idsByType)
            titlesByType[mediaType] = await GetMediaTitlesAsync(mediaType, mediaIds, cancellationToken);

        return titlesByType;
    }

    /// <summary>
    /// Projects the (parent media type, parent media id) references of the given entries, skipping entries
    /// that have no parent.
    /// </summary>
    /// <param name="entries">The entries to project parent references from.</param>
    /// <returns>The (media type, media id) references of the entries' parents.</returns>
    private static IEnumerable<MediaRef> GetParentMediaRefs(IEnumerable<PlaylistEntry> entries) =>
        entries
            .Where(e => e.ParentMediaType is not null && e.ParentMediaId is not null)
            .Select(e => new MediaRef(e.ParentMediaType!, e.ParentMediaId!.Value));

    private static string? GetParentTitle(PlaylistEntry entry, Dictionary<string, Dictionary<long, string>> parentTitlesByType)
    {
        if (entry.ParentMediaType is null || entry.ParentMediaId is null)
            return null;

        return parentTitlesByType.TryGetValue(entry.ParentMediaType, out var titles) && titles.TryGetValue(entry.ParentMediaId.Value, out var title)
            ? title
            : null;
    }

    /// <summary>
    /// Resolves an entry's own media title from <paramref name="titlesByType"/>, falling back to an empty
    /// string (matching <see cref="DtoPlaylistEntry.MediaTitle"/>'s default) if the entry's media no longer
    /// exists. <see cref="GetMediaTitlesAsync"/> only returns titles for media that is still present, so an
    /// orphaned entry (e.g. one reordered via <see cref="BatchReorderPlaylistEntriesAsync"/>, which does not
    /// filter entries the way <see cref="LoadValidPlaylistEntriesAsync"/> does) has no corresponding key.
    /// </summary>
    /// <param name="entry">The entry to resolve the title for.</param>
    /// <param name="titlesByType">The entries' own media titles, grouped by media type.</param>
    /// <returns>The resolved title, or an empty string if the entry's media no longer exists.</returns>
    private static string GetMediaTitle(PlaylistEntry entry, Dictionary<string, Dictionary<long, string>> titlesByType)
        => titlesByType.TryGetValue(entry.MediaType, out var titles) && titles.TryGetValue(entry.MediaId, out var title)
            ? title
            : string.Empty;

    private static Dictionary<string, HashSet<long>> GroupMediaIdsByType(IEnumerable<MediaRef> mediaRefs)
    {
        var idsByType = new Dictionary<string, HashSet<long>>();
        foreach (var mediaRef in mediaRefs)
            MediaHierarchyRegistry.AddMediaId(idsByType, mediaRef.MediaType, mediaRef.MediaId);
        return idsByType;
    }

    private async Task<List<PlaylistEntry>> SortPlaylistEntriesByReleaseDateAsync(List<PlaylistEntry> entries, CancellationToken cancellationToken)
    {
        var mediaIdsByType = MediaHierarchyRegistry.GroupMediaIdsByType(entries);

        var releaseDatesByType = new Dictionary<string, Dictionary<long, DateTime?>>();
        var hierarchyByType = new Dictionary<string, Dictionary<long, (long? ParentId, int? SequenceNumber)>>();
        foreach (var (mediaType, mediaIds) in mediaIdsByType)
        {
            if (!MediaHierarchyRegistry.TryParseKnownMediaType(mediaType, out var parsedType))
                continue;

            var handler = MediaHierarchyRegistry.Handlers[parsedType];
            releaseDatesByType[mediaType] = await handler.LoadReleaseDateAsync(_db, mediaIds, cancellationToken);
            hierarchyByType[mediaType] = await handler.GetHierarchySequenceAsync(_db, mediaIds, cancellationToken);
        }

        var sortKeys = new Dictionary<PlaylistEntry, (DateTime? ReleaseDate, long? ParentId, int? SequenceNumber, DateTime? AddedAt)>();
        foreach (var entry in entries)
        {
            var releaseDate = releaseDatesByType.TryGetValue(entry.MediaType, out var dates) && dates.TryGetValue(entry.MediaId, out var date)
                ? date
                : null;
            var sequenceInfo = hierarchyByType.TryGetValue(entry.MediaType, out var hierarchy) && hierarchy.TryGetValue(entry.MediaId, out var info)
                ? info
                : (ParentId: null, SequenceNumber: null);

            sortKeys[entry] = BuildPlaylistEntriesSortKey(entry, releaseDate, sequenceInfo);
        }

        return entries
            .OrderBy(e => sortKeys[e].ReleaseDate)
            .ThenBy(e => sortKeys[e].ParentId)
            .ThenBy(e => sortKeys[e].SequenceNumber)
            .ThenBy(e => sortKeys[e].AddedAt)
            .ToList();
    }

    private static (DateTime? ReleaseDate, long? ParentId, int? SequenceNumber, DateTime? AddedAt) BuildPlaylistEntriesSortKey(
        PlaylistEntry entry, DateTime? releaseDate, (long? ParentId, int? SequenceNumber) sequenceInfo)
    {
        return (releaseDate, sequenceInfo.ParentId, sequenceInfo.SequenceNumber, entry.AddedAt);
    }

    /// <inheritdoc />
    public async Task<DtoPlaylistNavigationResult?> GetNextPlaylistEntryAsync(long playlistId, string userId, long currentEntryId, CancellationToken cancellationToken = default)
    {
        var (entry, position) = await FindAdjacentPlayableEntryAsync(playlistId, userId, currentEntryId, forward: true, cancellationToken);
        if (entry is null)
            return null;

        var dtos = await BuildEntryDtosAsync(new List<PlaylistEntry> { entry }, userId, cancellationToken);
        var startPositionSeconds = await GetContinueWatchingPositionSecondsAsync(playlistId, userId, entry, cancellationToken);
        return new DtoPlaylistNavigationResult { Entry = dtos[0], Position = position, StartPositionSeconds = startPositionSeconds };
    }

    /// <inheritdoc />
    public async Task<DtoPlaylistNavigationResult?> GetPreviousPlaylistEntryAsync(long playlistId, string userId, long currentEntryId, CancellationToken cancellationToken = default)
    {
        var (entry, position) = await FindAdjacentPlayableEntryAsync(playlistId, userId, currentEntryId, forward: false, cancellationToken);
        if (entry is null)
            return null;

        var dtos = await BuildEntryDtosAsync(new List<PlaylistEntry> { entry }, userId, cancellationToken);
        var startPositionSeconds = await GetContinueWatchingPositionSecondsAsync(playlistId, userId, entry, cancellationToken);
        return new DtoPlaylistNavigationResult { Entry = dtos[0], Position = position, StartPositionSeconds = startPositionSeconds };
    }

    /// <inheritdoc />
    public Task<DtoPlaylistNavigationResult?> AdvancePlaylistAsync(long playlistId, string userId, long currentEntryId, CancellationToken cancellationToken = default)
        => GetNextPlaylistEntryAsync(playlistId, userId, currentEntryId, cancellationToken);

    /// <inheritdoc />
    public async Task<DtoPlaylistPlaybackStart> StartPlaylistAsync(long playlistId, string userId, long? entryId, CancellationToken cancellationToken = default)
    {
        // READ access (owner, or any user on a public playlist). Accessibility is always resolved for the
        // REQUESTING user (userId) - a viewer must never see/play titles that only the owner has unlocked.
        var (playlist, isOwner) = await GetReadablePlaylistAsync(playlistId, userId, cancellationToken);

        var validEntries = await LoadValidPlaylistEntriesAsync(playlist, removeOrphans: isOwner, cancellationToken);
        var sortedEntries = await SortPlaylistEntriesForModeAsync(playlist.SortMode, validEntries, cancellationToken);

        var idsByType = MediaHierarchyRegistry.GroupMediaIdsByType(sortedEntries);
        var accessibilityByEntry = await _accessResolver.ResolveAccessibilityAsync(sortedEntries, userId, idsByType, cancellationToken);

        var startEntry = entryId.HasValue
            ? ResolveExplicitStartEntry(sortedEntries, entryId.Value, accessibilityByEntry)
            : ResolveFirstPlayableEntry(sortedEntries, accessibilityByEntry);

        var startDtos = await BuildEntryDtosAsync(new List<PlaylistEntry> { startEntry }, userId, cancellationToken);
        var startDto = startDtos[0];
        var position = sortedEntries.FindIndex(e => e.Id == startEntry.Id) + 1;
        var startPositionSeconds = await GetContinueWatchingPositionSecondsAsync(playlist.Id, userId, startEntry, cancellationToken);

        return new DtoPlaylistPlaybackStart
        {
            PlaylistId = playlist.Id,
            PlaylistName = playlist.Name,
            TotalCount = sortedEntries.Count,
            CurrentPosition = position,
            CurrentEntryId = startEntry.Id,
            CurrentEntry = startDto,
            StreamUrl = BuildStreamUrl(startEntry.MediaType, startEntry.MediaId),
            MediaType = PlaylistEntryMediaTypeResolver.ToPlayerMediaType(startEntry.MediaType),
            MediaId = startEntry.MediaId,
            StartPositionSeconds = startPositionSeconds
        };
    }

    /// <summary>
    /// Resolves the playback position, in seconds, to resume <paramref name="startEntry"/>'s media at,
    /// from the matching <see cref="Data.ContinueWatchingEntry.Position"/> for <paramref name="userId"/>
    /// within playlist <paramref name="playlistId"/>, or <c>0</c> if none exists.
    /// </summary>
    /// <param name="playlistId">The id of the playlist being started.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="startEntry">The resolved start entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The playback position in seconds, or <c>0</c> if no matching entry exists.</returns>
    private async Task<long> GetContinueWatchingPositionSecondsAsync(long playlistId, string userId, PlaylistEntry startEntry, CancellationToken cancellationToken)
    {
        var position = await _db.ContinueWatchingEntries
            .Where(x => x.UserId == userId && x.PlaylistId == playlistId &&
                ((startEntry.MediaType == MediaTypeValues.Movie && x.MovieId == startEntry.MediaId) ||
                 (startEntry.MediaType == MediaTypeValues.TVShowEpisode && x.TVShowEpisodeId == startEntry.MediaId)))
            .Select(x => x.Position)
            .FirstOrDefaultAsync(cancellationToken);

        return (long)position.TotalSeconds;
    }

    /// <summary>
    /// Resolves the entry to start playback at when the caller explicitly requested one: it must belong
    /// to the playlist (<see cref="KeyNotFoundException"/> otherwise, mapped to 404), be directly playable
    /// (<see cref="InvalidOperationException"/> otherwise, mapped to 400 - a collection entry such as a
    /// TVShow/TVShowSeason/MovieCollection has no media of its own to stream, so its <see cref="PlaylistEntry.MediaId"/>
    /// must never be interpreted as a movie/episode id) and be accessible to the user
    /// (<see cref="PlaylistAccessDeniedException"/> otherwise, mapped to 403). Mirrors the playability check
    /// <see cref="ResolveFirstPlayableEntry"/> and <see cref="FindAdjacentPlayableEntryAsync"/> already
    /// perform for implicit/adjacent navigation, which this explicit-entry path previously omitted.
    /// </summary>
    /// <param name="sortedEntries">The playlist's entries, sorted by its current sort mode.</param>
    /// <param name="entryId">The id of the requested entry.</param>
    /// <param name="accessibilityByEntry">The bulk-resolved accessibility of each entry.</param>
    /// <returns>The resolved entry.</returns>
    private static PlaylistEntry ResolveExplicitStartEntry(
        List<PlaylistEntry> sortedEntries, long entryId, Dictionary<PlaylistEntry, bool> accessibilityByEntry)
    {
        var entry = sortedEntries.FirstOrDefault(e => e.Id == entryId)
            ?? throw new KeyNotFoundException("Der angegebene Eintrag gehört nicht zu dieser Playlist.");

        if (!PlaylistEntryMediaTypeResolver.IsPlayable(entry.MediaType))
            throw new InvalidOperationException("Der angegebene Eintrag ist nicht abspielbar.");

        if (!accessibilityByEntry.TryGetValue(entry, out var isAccessible) || !isAccessible)
            throw new PlaylistAccessDeniedException("Sie haben keinen Zugriff auf diesen Eintrag.");

        return entry;
    }

    /// <summary>
    /// Resolves the first playable and accessible entry of the playlist, for <see cref="StartPlaylistAsync"/>
    /// when no explicit entry id was requested.
    /// </summary>
    /// <param name="sortedEntries">The playlist's entries, sorted by its current sort mode.</param>
    /// <param name="accessibilityByEntry">The bulk-resolved accessibility of each entry.</param>
    /// <returns>The first playable and accessible entry.</returns>
    private static PlaylistEntry ResolveFirstPlayableEntry(List<PlaylistEntry> sortedEntries, Dictionary<PlaylistEntry, bool> accessibilityByEntry)
    {
        foreach (var entry in sortedEntries)
        {
            if (PlaylistEntryMediaTypeResolver.IsPlayable(entry.MediaType) && accessibilityByEntry.TryGetValue(entry, out var isAccessible) && isAccessible)
                return entry;
        }

        throw new InvalidOperationException("Diese Playlist enthält keine abspielbaren Einträge.");
    }

    /// <summary>
    /// Loads and sorts the playlist's entries the same way <see cref="GetPlaylistEntriesAsync"/> does,
    /// then finds the first playable and accessible entry strictly after (<paramref name="forward"/>
    /// <c>true</c>) or before (<paramref name="forward"/> <c>false</c>) <paramref name="currentEntryId"/>,
    /// skipping non-playable collection entries and inaccessible entries. Also resolves the found entry's
    /// actual 1-based position in <paramref name="playlistId"/>'s current sort order (the same computation
    /// <see cref="StartPlaylistAsync"/> already performs via <c>sortedEntries.FindIndex(...) + 1</c>), so
    /// callers can report the correct position even when entries were skipped to reach it. Shared by
    /// <see cref="GetNextPlaylistEntryAsync"/> and <see cref="GetPreviousPlaylistEntryAsync"/>.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="currentEntryId">The id of the playlist entry currently playing.</param>
    /// <param name="forward">Whether to search forward (next) or backward (previous).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The found entry and its 1-based position, or <c>(null, 0)</c> if none matches.</returns>
    private async Task<(PlaylistEntry? Entry, int Position)> FindAdjacentPlayableEntryAsync(long playlistId, string userId, long currentEntryId, bool forward, CancellationToken cancellationToken)
    {
        var (playlist, isOwner) = await GetReadablePlaylistAsync(playlistId, userId, cancellationToken);

        var validEntries = await LoadValidPlaylistEntriesAsync(playlist, removeOrphans: isOwner, cancellationToken);
        var sortedEntries = await SortPlaylistEntriesForModeAsync(playlist.SortMode, validEntries, cancellationToken);

        var currentIndex = sortedEntries.FindIndex(e => e.Id == currentEntryId);
        if (currentIndex < 0)
            throw new InvalidOperationException("Der aktuelle Eintrag gehört nicht zu dieser Playlist.");

        var idsByType = MediaHierarchyRegistry.GroupMediaIdsByType(sortedEntries);
        var accessibilityByEntry = await _accessResolver.ResolveAccessibilityAsync(sortedEntries, userId, idsByType, cancellationToken);

        var candidates = forward
            ? sortedEntries.Skip(currentIndex + 1)
            : Enumerable.Reverse(sortedEntries.Take(currentIndex));

        foreach (var candidate in candidates)
        {
            if (!PlaylistEntryMediaTypeResolver.IsPlayable(candidate.MediaType))
                continue;

            if (accessibilityByEntry.TryGetValue(candidate, out var isAccessible) && isAccessible)
                return (candidate, sortedEntries.FindIndex(e => e.Id == candidate.Id) + 1);
        }

        return (null, 0);
    }

    /// <summary>
    /// Builds the relative stream URL for a playable entry's media, matching the convention already used
    /// by the movie/TV show detail pages (<c>/api/items/{type}/{id}/stream</c>). Does not include an
    /// <c>access_token</c> query parameter: <see cref="PlaylistService"/> has no access to the caller's
    /// bearer token, so the client appends its own (already known) token before using the URL, exactly as
    /// <c>MovieCollectionDetails.razor</c> and <c>TVShowDetails.razor</c> already do for their own players.
    /// </summary>
    /// <param name="mediaType">The playlist entry's media type.</param>
    /// <param name="mediaId">The playlist entry's media id.</param>
    /// <returns>The relative stream URL, without an <c>access_token</c> query parameter.</returns>
    private static string BuildStreamUrl(string mediaType, long mediaId)
        => $"/api/items/{PlaylistEntryMediaTypeResolver.ResolveItemStreamType(mediaType)}/{mediaId}/stream";

    /// <summary>
    /// Regenerates a playlist's cover image as a collage of its current contents' poster pictures (see
    /// <see cref="PlaylistCoverImageGenerator.GeneratePlaylistCoverAsync"/> for the priority order), and
    /// replaces the playlist's current cover (if any) with it. An uploaded cover
    /// (<see cref="Playlist.CoverPictureIsUserUploaded"/>) has priority over a generated one ("Ein
    /// hochgeladenes Bild hat immer Vorrang"), so replacing it is only allowed with an explicit
    /// confirmation (<paramref name="confirmReplaceUploadedCover"/>) - otherwise
    /// <see cref="UploadedCoverReplacementConfirmationRequiredException"/> is thrown and nothing changes
    /// (same pattern as <see cref="ChangeSortModeAsync"/> and <see cref="RemoveMediaFromPlaylistAsync"/>).
    /// The confirmation is only demanded once a collage could actually be composed: if no source images
    /// are available, nothing is replaced and the uploaded cover stays untouched without any prompt.
    ///
    /// DESIGN DECISION (Schritt 10, Anforderung): Regeneration only ever happens here, i.e. only when
    /// explicitly triggered via the "Neu erzeugen" UI action calling this method through
    /// <c>PlaylistsController</c>. Unlike genre derivation (Schritt 9), which recomputes automatically
    /// whenever playlist content changes (<see cref="PlaylistGenreService.RecomputeGenresAsync"/>), the
    /// cover is intentionally left untouched by <see cref="AddMediaToPlaylistAsync"/>/
    /// <see cref="RemoveMediaFromPlaylistAsync"/> and the automatic backfill mechanism: the requirement
    /// only asks that regeneration "let itself be triggered" (on-demand), not that every content change
    /// regenerate the cover - which would also mean discarding a deliberately uploaded cover on the next
    /// unrelated content edit.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="confirmReplaceUploadedCover">Whether the user confirmed replacing an uploaded cover image.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The id of the newly generated cover picture, or <c>null</c> if no source images were available.</returns>
    /// <exception cref="UploadedCoverReplacementConfirmationRequiredException">An uploaded cover would be replaced without confirmation.</exception>
    public async Task<long?> GeneratePlaylistCoverAsync(long playlistId, string userId, bool confirmReplaceUploadedCover = false, CancellationToken cancellationToken = default)
    {
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        var collageBytes = await _coverImageGenerator.GeneratePlaylistCoverAsync(playlistId, cancellationToken);
        if (collageBytes is null)
            return null;

        if (playlist.CoverPictureIsUserUploaded && !confirmReplaceUploadedCover)
            throw new UploadedCoverReplacementConfirmationRequiredException(
                "Das hochgeladene Bild wird durch ein automatisch erzeugtes ersetzt. Bitte bestätigen.");

        var newPicture = new Picture
        {
            Type = "cover",
            Data = collageBytes,
            ContentType = "image/jpeg",
            Width = _playlistSettings.GeneratedCoverWidthPixels,
            Height = _playlistSettings.GeneratedCoverHeightPixels,
            IsGeneratedBackground = true,
            PlaylistId = playlistId
        };

        return await ReplaceCoverPictureAsync(playlist, newPicture, isUserUploaded: false, cancellationToken);
    }

    /// <summary>
    /// Saves an uploaded image as a playlist's cover, replacing its current cover (if any) - including a
    /// previously generated one.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="pictureData">The raw uploaded image bytes.</param>
    /// <param name="contentType">The MIME type reported for the upload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The id of the newly saved cover picture.</returns>
    /// <exception cref="InvalidOperationException">The upload failed <see cref="PlaylistCoverValidator.ValidateUploadAsync"/> (invalid format, too large, or not a genuine image).</exception>
    public async Task<long> SetPlaylistCoverAsync(long playlistId, string userId, byte[] pictureData, string? contentType, CancellationToken cancellationToken = default)
    {
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        var validation = await _coverValidator.ValidateUploadAsync(pictureData, contentType, pictureData?.Length ?? 0, cancellationToken);
        if (!validation.IsValid)
            throw new InvalidOperationException(validation.ErrorMessage);

        var newPicture = new Picture
        {
            Type = "cover",
            Data = pictureData!,
            // The format the bytes actually contain (detected by the validator), not the client-supplied header.
            ContentType = validation.ContentType!,
            Width = validation.Width,
            Height = validation.Height,
            IsGeneratedBackground = false,
            PlaylistId = playlistId
        };

        return await ReplaceCoverPictureAsync(playlist, newPicture, isUserUploaded: true, cancellationToken);
    }

    /// <summary>
    /// Loads the picture currently used as a playlist's cover for the image-delivery endpoint - READ access
    /// (Entwicklungsschritt 11): the owner, or any user while the playlist is public. A private playlist's
    /// cover is a collage of its contents' posters, so it is no longer handed out to other users.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cover <see cref="Picture"/>, or <c>null</c> if the playlist does not exist or has no cover set.</returns>
    public async Task<Picture?> GetPlaylistCoverAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists.AsNoTracking().FirstOrDefaultAsync(p => p.Id == playlistId, cancellationToken);
        if (playlist is null)
            return null;

        EnsureReadable(playlist, userId);

        if (playlist.CoverPictureId is not long pictureId)
            return null;

        return await _db.Pictures.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pictureId, cancellationToken);
    }

    /// <summary>
    /// Clears a playlist's cover (if any) and deletes the underlying picture. A no-op if the playlist has
    /// no cover set.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="userId">The id of the requesting (owning) user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DeletePlaylistCoverAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
    {
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);
        if (playlist.CoverPictureId is not long pictureId)
            return;

        var picture = await _db.Pictures.FirstOrDefaultAsync(p => p.Id == pictureId, cancellationToken);

        playlist.CoverPictureId = null;
        playlist.CoverPictureIsUserUploaded = false;

        if (picture is not null)
            _db.Pictures.Remove(picture);

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Saves <paramref name="newPicture"/> as <paramref name="playlist"/>'s new cover, deleting the
    /// previously referenced cover picture (if any). Shared by <see cref="GeneratePlaylistCoverAsync"/> and
    /// <see cref="SetPlaylistCoverAsync"/> - see the "Picture Cleanup" design decision in the implementation
    /// plan: an old cover (uploaded or generated) is always deleted, never left orphaned.
    /// </summary>
    /// <remarks>
    /// Everything below is staged against the change tracker and persisted via a single
    /// <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> call (assigning
    /// <see cref="Playlist.CoverPicture"/> rather than <see cref="Playlist.CoverPictureId"/> lets EF Core
    /// resolve the new picture's not-yet-known id itself once it inserts the row), instead of two separate
    /// calls with the new <see cref="Picture"/> committed first: with two calls, a failure of the second one
    /// (e.g. a dropped connection) would leave the already-committed new picture permanently orphaned in the
    /// <c>Pictures</c> table, referenced by no playlist. A single call makes the insert of the new picture,
    /// the update of <paramref name="playlist"/>'s foreign key and the deletion of the old picture (if any)
    /// atomic - either all of them apply, or none do.
    /// </remarks>
    /// <param name="playlist">The tracked playlist entity to update.</param>
    /// <param name="newPicture">The new cover picture to save (not yet added to the context).</param>
    /// <param name="isUserUploaded">The value to set <see cref="Playlist.CoverPictureIsUserUploaded"/> to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The id of the newly saved cover picture.</returns>
    private async Task<long> ReplaceCoverPictureAsync(Playlist playlist, Picture newPicture, bool isUserUploaded, CancellationToken cancellationToken)
    {
        var oldPictureId = playlist.CoverPictureId;

        await _db.Pictures.AddAsync(newPicture, cancellationToken);

        playlist.CoverPicture = newPicture;
        playlist.CoverPictureIsUserUploaded = isUserUploaded;

        if (oldPictureId.HasValue)
        {
            var oldPicture = await _db.Pictures.FirstOrDefaultAsync(p => p.Id == oldPictureId.Value, cancellationToken);
            if (oldPicture is not null)
                _db.Pictures.Remove(oldPicture);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return newPicture.Id;
    }
}
