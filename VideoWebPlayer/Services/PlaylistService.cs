using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistService"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="unlockedMediaService">Service used to check per-entry unlock/access status.</param>
    /// <param name="playlistSettings">Playlist configuration.</param>
    public PlaylistService(
        ApplicationDbContext db,
        IUnlockedMediaService unlockedMediaService,
        IOptions<PlaylistSettings> playlistSettings)
    {
        _db = db;
        _playlistSettings = playlistSettings.Value;
        _accessResolver = new PlaylistEntryAccessResolver(db, unlockedMediaService);
        _reorderService = new PlaylistEntryReorderService(db);
    }

    /// <inheritdoc />
    public async Task<DtoPlaylist[]> GetPlaylistsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var playlists = await _db.Playlists
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .ToListAsync(cancellationToken);

        return playlists.Select(ToDto).ToArray();
    }

    /// <inheritdoc />
    public async Task<DtoPlaylist?> GetPlaylistAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists.AsNoTracking().FirstOrDefaultAsync(p => p.Id == playlistId, cancellationToken);
        if (playlist is null)
            return null;

        if (playlist.UserId != userId)
            throw new PlaylistAccessDeniedException("Sie haben keinen Zugriff auf diese Playlist.");

        return ToDto(playlist);
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

        return ToDto(playlist);
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

        return ToDto(playlist);
    }

    /// <inheritdoc />
    public async Task DeletePlaylistAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
    {
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        _db.Playlists.Remove(playlist);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DtoPlaylistAddResult> AddMediaToPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken cancellationToken = default)
    {
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        var parsedMediaType = MediaHierarchyRegistry.ParseMediaType(mediaType);
        var normalizedMediaType = parsedMediaType.ToString();

        if (mediaId <= 0)
            throw new InvalidOperationException("MediaId muss groesser als 0 sein.");

        var mediaTitle = await GetMediaTitleAsync(mediaType, mediaId, cancellationToken);
        if (mediaTitle is null)
            throw new KeyNotFoundException("Medieninhalt wurde nicht gefunden.");

        var (entriesToAdd, skippedDuplicateCount, topLevelEntry, existingEntryCount) =
            await BuildEntriesToAddAsync(playlistId, parsedMediaType, normalizedMediaType, mediaId, cancellationToken);

        if (_playlistSettings.MaxPlaylistItemCount is int maxItemCount
            && existingEntryCount + entriesToAdd.Count > maxItemCount)
            throw new InvalidOperationException("Die maximale Anzahl an Playlist-Eintraegen wurde erreicht.");

        await AssignSortOrderForNewEntriesAsync(playlist, entriesToAdd, cancellationToken);

        await _db.PlaylistEntries.AddRangeAsync(entriesToAdd, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await BuildAddResultAsync(entriesToAdd, skippedDuplicateCount, topLevelEntry, userId, cancellationToken);
    }

    /// <summary>
    /// Assigns <see cref="PlaylistEntry.SortOrder"/> to newly built entries before they are persisted:
    /// ascending values appended after the playlist's current maximum when the playlist is in
    /// <see cref="PlaylistSortMode.Manual"/> mode, or <c>null</c> for <see cref="PlaylistSortMode.ByReleaseDate"/>.
    /// </summary>
    private async Task AssignSortOrderForNewEntriesAsync(Playlist playlist, List<PlaylistEntry> entriesToAdd, CancellationToken cancellationToken)
    {
        if (entriesToAdd.Count == 0)
            return;

        if (playlist.SortMode == PlaylistSortMode.Manual)
        {
            var maxSortOrder = await _reorderService.GetMaxSortOrderAsync(playlist.Id, cancellationToken) ?? -1;

            foreach (var entry in entriesToAdd)
                entry.SortOrder = ++maxSortOrder;
        }
        else
        {
            foreach (var entry in entriesToAdd)
                entry.SortOrder = null;
        }
    }

    /// <inheritdoc />
    public async Task RemoveMediaFromPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        var normalizedMediaType = MediaHierarchyRegistry.ParseMediaType(mediaType).ToString();

        var entry = await _db.PlaylistEntries.FirstOrDefaultAsync(
            e => e.PlaylistId == playlistId && e.MediaType == normalizedMediaType && e.MediaId == mediaId, cancellationToken)
            ?? throw new KeyNotFoundException("Eintrag wurde nicht gefunden.");

        _db.PlaylistEntries.Remove(entry);
        await _db.SaveChangesAsync(cancellationToken);
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
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        var validEntries = await LoadValidPlaylistEntriesAsync(playlistId, cancellationToken);
        var sortedEntries = await SortPlaylistEntriesForModeAsync(playlist.SortMode, validEntries, cancellationToken);
        return await BuildEntryDtosAsync(sortedEntries, userId, cancellationToken);
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
            throw new ArgumentException("Ungueltiger Sortiermodus-Wert.");

        return parsed;
    }

    private static DtoPlaylist ToDto(Playlist playlist) => new()
    {
        Id = playlist.Id,
        Name = playlist.Name,
        Description = playlist.Description,
        SortMode = playlist.SortMode.ToString(),
        CreatedAt = playlist.CreatedAt,
        UpdatedAt = playlist.UpdatedAt
    };

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
                $"{entriesToAdd.Count} Titel hinzugefuegt, {skippedDuplicateCount} bereits vorhanden und uebersprungen.",
            > 0 =>
                $"{entriesToAdd.Count} Titel hinzugefuegt.",
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
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        var validEntries = await LoadValidPlaylistEntriesAsync(playlistId, cancellationToken);
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
            var validEntries = await LoadValidPlaylistEntriesAsync(playlistId, cancellationToken);
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

        return ToDto(playlist);
    }

    /// <summary>
    /// Loads all <see cref="PlaylistEntry"/> rows of a playlist, removing (and persisting the removal of)
    /// orphaned entries whose referenced media no longer exists. Only checks for existence of the referenced
    /// media (not its title), so the cost of this call does not depend on how many entries are actually
    /// going to be displayed by the caller.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The playlist entries whose referenced media still exists.</returns>
    private async Task<List<PlaylistEntry>> LoadValidPlaylistEntriesAsync(long playlistId, CancellationToken cancellationToken)
    {
        var entries = await _db.PlaylistEntries
            .Where(e => e.PlaylistId == playlistId)
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

        if (orphans.Count > 0)
        {
            _db.PlaylistEntries.RemoveRange(orphans);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return validEntries;
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
}
