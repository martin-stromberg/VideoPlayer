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
using VideoWebPlayer.Events;

namespace VideoWebPlayer.Services;

/// <summary>
/// Implements playlist CRUD operations backed by <see cref="ApplicationDbContext"/>.
/// </summary>
public sealed class PlaylistService : IPlaylistService
{
    private readonly ApplicationDbContext _db;
    private readonly EventManager _eventManager;
    private readonly PlaylistSettings _playlistSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistService"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="eventManager">Event manager used for publishing playlist events.</param>
    /// <param name="playlistSettings">Optional playlist configuration.</param>
    public PlaylistService(
        ApplicationDbContext db,
        EventManager eventManager,
        IOptions<PlaylistSettings>? playlistSettings = null)
    {
        _db = db;
        _eventManager = eventManager;
        _playlistSettings = playlistSettings?.Value ?? new PlaylistSettings();
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

        var isDuplicate = await _db.Playlists
            .AsNoTracking()
            .AnyAsync(p => p.UserId == userId && p.Name.ToLower() == trimmedName.ToLower(), cancellationToken);
        if (isDuplicate)
            throw new InvalidOperationException("Ein Playlist mit diesem Namen existiert bereits.");

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
        _eventManager.Publish(new PlaylistCreatedEvent(playlist));

        return ToDto(playlist);
    }

    /// <inheritdoc />
    public async Task<DtoPlaylist> UpdatePlaylistAsync(long playlistId, string userId, string name, string? description, string? sortMode, CancellationToken cancellationToken = default)
    {
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        var trimmedName = ValidateName(name);
        var trimmedDescription = ValidateDescription(description);
        var resolvedSortMode = ParseSortMode(sortMode, playlist.SortMode);

        var isDuplicate = await _db.Playlists
            .AsNoTracking()
            .AnyAsync(p => p.UserId == userId && p.Id != playlistId && p.Name.ToLower() == trimmedName.ToLower(), cancellationToken);
        if (isDuplicate)
            throw new InvalidOperationException("Ein Playlist mit diesem Namen existiert bereits.");

        playlist.Name = trimmedName;
        playlist.Description = trimmedDescription;
        playlist.SortMode = resolvedSortMode;
        playlist.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        _eventManager.Publish(new PlaylistUpdatedEvent(playlist));

        return ToDto(playlist);
    }

    /// <inheritdoc />
    public async Task DeletePlaylistAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
    {
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        _db.Playlists.Remove(playlist);
        await _db.SaveChangesAsync(cancellationToken);
        _eventManager.Publish(new PlaylistDeletedEvent(playlistId, userId));
    }

    /// <inheritdoc />
    public async Task<DtoPlaylistEntry> AddMediaToPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        var parsedMediaType = ParseMediaType(mediaType);

        if (mediaId <= 0)
            throw new InvalidOperationException("MediaId muss groesser als 0 sein.");

        var mediaTitle = await GetMediaTitleAsync(mediaType, mediaId, cancellationToken);
        if (mediaTitle is null)
            throw new KeyNotFoundException("Medieninhalt wurde nicht gefunden.");

        var existingKeys = (await _db.PlaylistEntries
            .AsNoTracking()
            .Where(e => e.PlaylistId == playlistId)
            .Select(e => new { e.MediaType, e.MediaId })
            .ToListAsync(cancellationToken))
            .Select(e => (e.MediaType, e.MediaId))
            .ToHashSet();

        if (existingKeys.Contains((mediaType, mediaId)))
            throw new InvalidOperationException("Medieninhalt bereits in dieser Playlist vorhanden.");

        var cascadeEntries = await GetCascadeMediaIdsAsync(parsedMediaType, mediaId, cancellationToken);
        var newCascadeEntries = cascadeEntries
            .Select(e => (MediaType: e.MediaType.ToString(), e.MediaId))
            .Where(e => !existingKeys.Contains(e))
            .ToList();

        if (_playlistSettings.MaxPlaylistItemCount is int maxItemCount)
        {
            var newEntryCount = 1 + newCascadeEntries.Count;
            if (existingKeys.Count + newEntryCount > maxItemCount)
                throw new InvalidOperationException("Die maximale Anzahl an Playlist-Eintraegen wurde erreicht.");
        }

        var now = DateTime.UtcNow;
        var topLevelEntry = new PlaylistEntry
        {
            PlaylistId = playlistId,
            MediaType = mediaType,
            MediaId = mediaId,
            ParentMediaType = null,
            ParentMediaId = null,
            AddedAt = now
        };
        await _db.PlaylistEntries.AddAsync(topLevelEntry, cancellationToken);

        foreach (var (childMediaType, childMediaId) in newCascadeEntries)
        {
            await _db.PlaylistEntries.AddAsync(new PlaylistEntry
            {
                PlaylistId = playlistId,
                MediaType = childMediaType,
                MediaId = childMediaId,
                ParentMediaType = mediaType,
                ParentMediaId = mediaId,
                AddedAt = now
            }, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(topLevelEntry, mediaTitle, null);
    }

    /// <inheritdoc />
    public async Task RemoveMediaFromPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        var entry = await _db.PlaylistEntries.FirstOrDefaultAsync(
            e => e.PlaylistId == playlistId && e.MediaType == mediaType && e.MediaId == mediaId, cancellationToken)
            ?? throw new KeyNotFoundException("Eintrag wurde nicht gefunden.");

        _db.PlaylistEntries.Remove(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DtoPlaylistEntry[]> GetPlaylistEntriesAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        var entries = await _db.PlaylistEntries
            .Where(e => e.PlaylistId == playlistId)
            .ToListAsync(cancellationToken);

        var mediaIdsByType = new Dictionary<string, HashSet<long>>();
        foreach (var entry in entries)
        {
            AddId(mediaIdsByType, entry.MediaType, entry.MediaId);
            if (entry.ParentMediaType is not null && entry.ParentMediaId is not null)
                AddId(mediaIdsByType, entry.ParentMediaType, entry.ParentMediaId.Value);
        }

        var titlesByType = new Dictionary<string, Dictionary<long, string>>();
        foreach (var (mediaType, mediaIds) in mediaIdsByType)
            titlesByType[mediaType] = await GetMediaTitlesAsync(mediaType, mediaIds, cancellationToken);

        var result = new List<DtoPlaylistEntry>(entries.Count);
        var orphans = new List<PlaylistEntry>();

        foreach (var entry in entries)
        {
            if (!titlesByType[entry.MediaType].TryGetValue(entry.MediaId, out var mediaTitle))
            {
                orphans.Add(entry);
                continue;
            }

            string? parentMediaTitle = null;
            if (entry.ParentMediaType is not null && entry.ParentMediaId is not null)
                titlesByType[entry.ParentMediaType].TryGetValue(entry.ParentMediaId.Value, out parentMediaTitle);

            result.Add(ToDto(entry, mediaTitle, parentMediaTitle));
        }

        if (orphans.Count > 0)
        {
            _db.PlaylistEntries.RemoveRange(orphans);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return result.ToArray();

        static void AddId(Dictionary<string, HashSet<long>> map, string mediaType, long mediaId)
        {
            if (!map.TryGetValue(mediaType, out var ids))
                map[mediaType] = ids = new HashSet<long>();
            ids.Add(mediaId);
        }
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
        if (trimmedName.Length > 255)
            throw new InvalidOperationException("Name darf maximal 255 Zeichen lang sein.");

        return trimmedName;
    }

    private static string? ValidateDescription(string? description)
    {
        if (string.IsNullOrEmpty(description))
            return null;

        if (description.Length > 2000)
            throw new InvalidOperationException("Beschreibung darf maximal 2000 Zeichen lang sein.");

        return description;
    }

    private static PlaylistSortMode ParseSortMode(string? sortMode, PlaylistSortMode fallback)
    {
        if (string.IsNullOrWhiteSpace(sortMode))
            return fallback;

        if (!Enum.TryParse<PlaylistSortMode>(sortMode, ignoreCase: true, out var parsed))
            throw new InvalidOperationException("Ungueltiger Sortiermodus.");

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

    private sealed class MediaTypeHandler
    {
        public required Func<ApplicationDbContext, IReadOnlyCollection<long>, CancellationToken, Task<Dictionary<long, string>>> LoadTitlesAsync { get; init; }

        public Func<ApplicationDbContext, long, CancellationToken, Task<List<(MediaType MediaType, long MediaId)>>>? LoadCascadeChildrenAsync { get; init; }
    }

    private static readonly IReadOnlyDictionary<MediaType, MediaTypeHandler> MediaTypeHandlers = new Dictionary<MediaType, MediaTypeHandler>
    {
        [MediaType.Movie] = new MediaTypeHandler
        {
            LoadTitlesAsync = (db, ids, ct) => db.Movies.AsNoTracking().Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Name, ct)
        },
        [MediaType.TVShowEpisode] = new MediaTypeHandler
        {
            LoadTitlesAsync = (db, ids, ct) => db.TVShowEpisodes.AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Name, ct)
        },
        [MediaType.TVShowSeason] = new MediaTypeHandler
        {
            LoadTitlesAsync = (db, ids, ct) => db.TVShowSeasons.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct),
            LoadCascadeChildrenAsync = async (db, id, ct) =>
            {
                var episodeIds = await db.TVShowEpisodes.AsNoTracking().Where(e => e.TVShowSeasonId == id).Select(e => e.Id).ToListAsync(ct);
                return episodeIds.Select(episodeId => (MediaType.TVShowEpisode, episodeId)).ToList();
            }
        },
        [MediaType.TVShow] = new MediaTypeHandler
        {
            LoadTitlesAsync = (db, ids, ct) => db.TVShows.AsNoTracking().Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct),
            LoadCascadeChildrenAsync = async (db, id, ct) =>
            {
                var seasonIds = await db.TVShowSeasons.AsNoTracking().Where(s => s.TVShowId == id).Select(s => s.Id).ToListAsync(ct);
                var episodeIds = await db.TVShowEpisodes.AsNoTracking().Where(e => seasonIds.Contains(e.TVShowSeasonId)).Select(e => e.Id).ToListAsync(ct);

                var result = new List<(MediaType MediaType, long MediaId)>();
                result.AddRange(seasonIds.Select(seasonId => (MediaType.TVShowSeason, seasonId)));
                result.AddRange(episodeIds.Select(episodeId => (MediaType.TVShowEpisode, episodeId)));
                return result;
            }
        },
        [MediaType.MovieCollection] = new MediaTypeHandler
        {
            LoadTitlesAsync = (db, ids, ct) => db.MovieCollections.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct),
            LoadCascadeChildrenAsync = async (db, id, ct) =>
            {
                var movieIds = await db.Movies.AsNoTracking().Where(m => m.MovieCollectionId == id).Select(m => m.Id).ToListAsync(ct);
                return movieIds.Select(movieId => (MediaType.Movie, movieId)).ToList();
            }
        }
    };

    /// <summary>
    /// Parses and validates a raw media type string (one of the values in <c>MediaTypeValues</c>)
    /// into the internal <see cref="MediaType"/> enum.
    /// </summary>
    /// <param name="mediaType">The raw media type string to parse.</param>
    /// <returns>The parsed <see cref="MediaType"/> value.</returns>
    private static MediaType ParseMediaType(string mediaType)
    {
        if (!Enum.TryParse<MediaType>(mediaType, ignoreCase: true, out var parsed) || !MediaTypeHandlers.ContainsKey(parsed))
            throw new InvalidOperationException("Ungueltiger Medientyp.");

        return parsed;
    }

    private async Task<IEnumerable<(MediaType MediaType, long MediaId)>> GetCascadeMediaIdsAsync(MediaType mediaType, long mediaId, CancellationToken cancellationToken)
    {
        if (!MediaTypeHandlers.TryGetValue(mediaType, out var handler) || handler.LoadCascadeChildrenAsync is null)
            return Enumerable.Empty<(MediaType MediaType, long MediaId)>();

        return await handler.LoadCascadeChildrenAsync(_db, mediaId, cancellationToken);
    }

    private async Task<string?> GetMediaTitleAsync(string mediaType, long mediaId, CancellationToken cancellationToken)
    {
        var titles = await GetMediaTitlesAsync(mediaType, new[] { mediaId }, cancellationToken);
        return titles.TryGetValue(mediaId, out var title) ? title : null;
    }

    private async Task<Dictionary<long, string>> GetMediaTitlesAsync(string mediaType, IReadOnlyCollection<long> mediaIds, CancellationToken cancellationToken)
    {
        if (mediaIds.Count == 0 || !Enum.TryParse<MediaType>(mediaType, ignoreCase: true, out var parsedType) || !MediaTypeHandlers.TryGetValue(parsedType, out var handler))
            return new Dictionary<long, string>();

        return await handler.LoadTitlesAsync(_db, mediaIds, cancellationToken);
    }

    private static DtoPlaylistEntry ToDto(PlaylistEntry playlistEntry, string mediaTitle, string? parentMediaTitle) => new()
    {
        Id = playlistEntry.Id,
        PlaylistId = playlistEntry.PlaylistId,
        MediaType = playlistEntry.MediaType,
        MediaId = playlistEntry.MediaId,
        MediaTitle = mediaTitle,
        ParentMediaType = playlistEntry.ParentMediaType,
        ParentMediaId = playlistEntry.ParentMediaId,
        ParentMediaTitle = parentMediaTitle,
        AddedAt = playlistEntry.AddedAt
    };
}
