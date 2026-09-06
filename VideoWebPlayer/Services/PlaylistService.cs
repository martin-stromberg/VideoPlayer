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
    private readonly IUnlockedMediaService _unlockedMediaService;
    private readonly PlaylistSettings _playlistSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistService"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="eventManager">Event manager used for publishing playlist events.</param>
    /// <param name="unlockedMediaService">Service used to check per-entry unlock/access status.</param>
    /// <param name="playlistSettings">Optional playlist configuration.</param>
    public PlaylistService(
        ApplicationDbContext db,
        EventManager eventManager,
        IUnlockedMediaService unlockedMediaService,
        IOptions<PlaylistSettings>? playlistSettings = null)
    {
        _db = db;
        _eventManager = eventManager;
        _unlockedMediaService = unlockedMediaService;
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

        await EnsureNameNotDuplicateAsync(userId, trimmedName, playlistId, cancellationToken);

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
    public async Task<DtoPlaylistAddResult> AddMediaToPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        var parsedMediaType = ParseMediaType(mediaType);
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

        await _db.PlaylistEntries.AddRangeAsync(entriesToAdd, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await BuildAddResultAsync(entriesToAdd, skippedDuplicateCount, topLevelEntry, userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveMediaFromPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        var normalizedMediaType = ParseMediaType(mediaType).ToString();

        var entry = await _db.PlaylistEntries.FirstOrDefaultAsync(
            e => e.PlaylistId == playlistId && e.MediaType == normalizedMediaType && e.MediaId == mediaId, cancellationToken)
            ?? throw new KeyNotFoundException("Eintrag wurde nicht gefunden.");

        _db.PlaylistEntries.Remove(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DtoPlaylistEntry[]> GetPlaylistEntriesAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        var validEntries = await LoadValidPlaylistEntriesAsync(playlistId, cancellationToken);
        return await BuildEntryDtosAsync(validEntries, userId, cancellationToken);
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

    private async Task EnsureNameNotDuplicateAsync(string userId, string name, long? excludePlaylistId, CancellationToken cancellationToken)
    {
        var isDuplicate = await _db.Playlists
            .AsNoTracking()
            .AnyAsync(p => p.UserId == userId && p.Id != excludePlaylistId && p.Name == name, cancellationToken);
        if (isDuplicate)
            throw new InvalidOperationException("Ein Playlist mit diesem Namen existiert bereits.");
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

        /// <summary>
        /// Checks which of the given ids still reference existing media, without fetching their titles.
        /// Used for orphan detection over an entire playlist, where resolving titles would be wasted work
        /// for every entry that is not part of the page actually being returned to the caller.
        /// </summary>
        public required Func<ApplicationDbContext, IReadOnlyCollection<long>, CancellationToken, Task<HashSet<long>>> LoadExistingIdsAsync { get; init; }

        public Func<ApplicationDbContext, long, CancellationToken, Task<List<(MediaType MediaType, long MediaId)>>>? LoadCascadeChildrenAsync { get; init; }

        public required Func<ApplicationDbContext, IReadOnlyCollection<long>, CancellationToken, Task<Dictionary<long, DateTime?>>> LoadReleaseDateAsync { get; init; }

        public required Func<ApplicationDbContext, IReadOnlyCollection<long>, CancellationToken, Task<Dictionary<long, (long? ParentId, int? SequenceNumber)>>> GetHierarchySequenceAsync { get; init; }

        /// <summary>
        /// Bulk-loads the resolved picture id (poster, falling back to banner and then fanart) for each of
        /// the given ids in a single query, matching the fallback convention used by <c>MediaBaseEntryList.razor</c>.
        /// </summary>
        public required Func<ApplicationDbContext, IReadOnlyCollection<long>, CancellationToken, Task<Dictionary<long, long?>>> LoadPictureIdsAsync { get; init; }
    }

    private static readonly IReadOnlyDictionary<MediaType, MediaTypeHandler> MediaTypeHandlers = new Dictionary<MediaType, MediaTypeHandler>
    {
        [MediaType.Movie] = new MediaTypeHandler
        {
            LoadTitlesAsync = (db, ids, ct) => db.Movies.AsNoTracking().Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Name, ct),
            LoadExistingIdsAsync = (db, ids, ct) => db.Movies.AsNoTracking().Where(m => ids.Contains(m.Id)).Select(m => m.Id).ToHashSetAsync(ct),
            LoadReleaseDateAsync = (db, ids, ct) => db.Movies.AsNoTracking().Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.ReleaseDate ?? m.PremieredAt, ct),
            GetHierarchySequenceAsync = NoHierarchyAsync,
            LoadPictureIdsAsync = async (db, ids, ct) =>
            {
                var pictures = await db.Movies.AsNoTracking()
                    .Where(m => ids.Contains(m.Id))
                    .Select(m => new { m.Id, m.PosterPictureId, m.BannerPictureId, m.FanartPictureId })
                    .ToListAsync(ct);
                return pictures.ToDictionary(m => m.Id, m => m.PosterPictureId ?? m.BannerPictureId ?? m.FanartPictureId);
            }
        },
        [MediaType.TVShowEpisode] = new MediaTypeHandler
        {
            LoadTitlesAsync = (db, ids, ct) => db.TVShowEpisodes.AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Name, ct),
            LoadExistingIdsAsync = (db, ids, ct) => db.TVShowEpisodes.AsNoTracking().Where(e => ids.Contains(e.Id)).Select(e => e.Id).ToHashSetAsync(ct),
            LoadReleaseDateAsync = async (db, ids, ct) =>
            {
                var episodes = await db.TVShowEpisodes.AsNoTracking()
                    .Where(e => ids.Contains(e.Id))
                    .Select(e => new { e.Id, e.ReleaseDate, e.PremieredAt, ShowPremieredAt = e.TVShowSeason.TVShow.PremieredAt })
                    .ToListAsync(ct);
                return episodes.ToDictionary(e => e.Id, e => e.ReleaseDate ?? e.PremieredAt ?? e.ShowPremieredAt);
            },
            GetHierarchySequenceAsync = async (db, ids, ct) =>
            {
                var episodes = await db.TVShowEpisodes.AsNoTracking()
                    .Where(e => ids.Contains(e.Id))
                    .Select(e => new { e.Id, e.TVShowSeasonId, e.Number })
                    .ToListAsync(ct);
                return episodes.ToDictionary(e => e.Id, e => ((long?)e.TVShowSeasonId, (int?)e.Number));
            },
            LoadPictureIdsAsync = async (db, ids, ct) =>
            {
                var pictures = await db.TVShowEpisodes.AsNoTracking()
                    .Where(e => ids.Contains(e.Id))
                    .Select(e => new { e.Id, e.PosterPictureId, e.BannerPictureId, e.FanartPictureId })
                    .ToListAsync(ct);
                return pictures.ToDictionary(e => e.Id, e => e.PosterPictureId ?? e.BannerPictureId ?? e.FanartPictureId);
            }
        },
        [MediaType.TVShowSeason] = new MediaTypeHandler
        {
            LoadTitlesAsync = (db, ids, ct) => db.TVShowSeasons.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct),
            LoadExistingIdsAsync = (db, ids, ct) => db.TVShowSeasons.AsNoTracking().Where(s => ids.Contains(s.Id)).Select(s => s.Id).ToHashSetAsync(ct),
            LoadCascadeChildrenAsync = async (db, id, ct) =>
            {
                var episodeIds = await db.TVShowEpisodes.AsNoTracking().Where(e => e.TVShowSeasonId == id).Select(e => e.Id).ToListAsync(ct);
                return episodeIds.Select(episodeId => (MediaType.TVShowEpisode, episodeId)).ToList();
            },
            LoadReleaseDateAsync = async (db, ids, ct) =>
            {
                var seasons = await db.TVShowSeasons.AsNoTracking()
                    .Where(s => ids.Contains(s.Id))
                    .Select(s => new
                    {
                        s.Id,
                        s.PremieredAt,
                        FirstEpisodeDate = s.Episodes.OrderBy(e => e.Number).Select(e => (DateTime?)e.ReleaseDate).FirstOrDefault(),
                        ShowPremieredAt = s.TVShow.PremieredAt
                    })
                    .ToListAsync(ct);
                return seasons.ToDictionary(s => s.Id, s => s.PremieredAt ?? s.FirstEpisodeDate ?? s.ShowPremieredAt);
            },
            GetHierarchySequenceAsync = async (db, ids, ct) =>
            {
                var seasons = await db.TVShowSeasons.AsNoTracking()
                    .Where(s => ids.Contains(s.Id))
                    .Select(s => new { s.Id, s.TVShowId })
                    .ToListAsync(ct);
                return seasons
                    .GroupBy(s => s.TVShowId)
                    .SelectMany(g => g.OrderBy(s => s.Id).Select((s, index) => (s.Id, ParentId: (long?)g.Key, SequenceNumber: (int?)(index + 1))))
                    .ToDictionary(x => x.Id, x => (x.ParentId, x.SequenceNumber));
            },
            LoadPictureIdsAsync = async (db, ids, ct) =>
            {
                var pictures = await db.TVShowSeasons.AsNoTracking()
                    .Where(s => ids.Contains(s.Id))
                    .Select(s => new { s.Id, s.PosterPictureId, s.BannerPictureId, s.FanartPictureId })
                    .ToListAsync(ct);
                return pictures.ToDictionary(s => s.Id, s => s.PosterPictureId ?? s.BannerPictureId ?? s.FanartPictureId);
            }
        },
        [MediaType.TVShow] = new MediaTypeHandler
        {
            LoadTitlesAsync = (db, ids, ct) => db.TVShows.AsNoTracking().Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct),
            LoadExistingIdsAsync = (db, ids, ct) => db.TVShows.AsNoTracking().Where(t => ids.Contains(t.Id)).Select(t => t.Id).ToHashSetAsync(ct),
            LoadCascadeChildrenAsync = async (db, id, ct) =>
            {
                var seasonIds = await db.TVShowSeasons.AsNoTracking().Where(s => s.TVShowId == id).Select(s => s.Id).ToListAsync(ct);
                var episodeIds = await db.TVShowEpisodes.AsNoTracking().Where(e => seasonIds.Contains(e.TVShowSeasonId)).Select(e => e.Id).ToListAsync(ct);

                var result = new List<(MediaType MediaType, long MediaId)>();
                result.AddRange(seasonIds.Select(seasonId => (MediaType.TVShowSeason, seasonId)));
                result.AddRange(episodeIds.Select(episodeId => (MediaType.TVShowEpisode, episodeId)));
                return result;
            },
            LoadReleaseDateAsync = async (db, ids, ct) =>
            {
                var shows = await db.TVShows.AsNoTracking()
                    .Where(t => ids.Contains(t.Id))
                    .Select(t => new
                    {
                        t.Id,
                        t.PremieredAt,
                        FirstEpisodeDate = t.Seasons
                            .OrderBy(s => s.Id)
                            .Select(s => s.Episodes.OrderBy(e => e.Number).Select(e => (DateTime?)e.ReleaseDate).FirstOrDefault())
                            .FirstOrDefault()
                    })
                    .ToListAsync(ct);
                return shows.ToDictionary(t => t.Id, t => t.PremieredAt ?? t.FirstEpisodeDate);
            },
            GetHierarchySequenceAsync = NoHierarchyAsync,
            LoadPictureIdsAsync = async (db, ids, ct) =>
            {
                var pictures = await db.TVShows.AsNoTracking()
                    .Where(t => ids.Contains(t.Id))
                    .Select(t => new { t.Id, t.PosterPictureId, t.BannerPictureId, t.FanartPictureId })
                    .ToListAsync(ct);
                return pictures.ToDictionary(t => t.Id, t => t.PosterPictureId ?? t.BannerPictureId ?? t.FanartPictureId);
            }
        },
        [MediaType.MovieCollection] = new MediaTypeHandler
        {
            LoadTitlesAsync = (db, ids, ct) => db.MovieCollections.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct),
            LoadExistingIdsAsync = (db, ids, ct) => db.MovieCollections.AsNoTracking().Where(c => ids.Contains(c.Id)).Select(c => c.Id).ToHashSetAsync(ct),
            LoadCascadeChildrenAsync = async (db, id, ct) =>
            {
                var movieIds = await db.Movies.AsNoTracking().Where(m => m.MovieCollectionId == id).Select(m => m.Id).ToListAsync(ct);
                return movieIds.Select(movieId => (MediaType.Movie, movieId)).ToList();
            },
            LoadReleaseDateAsync = (db, ids, ct) => db.MovieCollections.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.ReleaseDate ?? c.PremieredAt, ct),
            GetHierarchySequenceAsync = NoHierarchyAsync,
            LoadPictureIdsAsync = async (db, ids, ct) =>
            {
                var pictures = await db.MovieCollections.AsNoTracking()
                    .Where(c => ids.Contains(c.Id))
                    .Select(c => new { c.Id, c.PosterPictureId, c.BannerPictureId, c.FanartPictureId })
                    .ToListAsync(ct);
                return pictures.ToDictionary(c => c.Id, c => c.PosterPictureId ?? c.BannerPictureId ?? c.FanartPictureId);
            }
        }
    };

    private static Task<Dictionary<long, (long? ParentId, int? SequenceNumber)>> NoHierarchyAsync(ApplicationDbContext db, IReadOnlyCollection<long> ids, CancellationToken cancellationToken)
        => Task.FromResult(ids.ToDictionary(id => id, id => ((long?)null, (int?)null)));

    /// <summary>
    /// Parses and validates a raw media type string (one of the values in <c>MediaTypeValues</c>)
    /// into the internal <see cref="MediaType"/> enum.
    /// </summary>
    /// <param name="mediaType">The raw media type string to parse.</param>
    /// <returns>The parsed <see cref="MediaType"/> value.</returns>
    private static MediaType ParseMediaType(string mediaType)
    {
        if (!TryParseKnownMediaType(mediaType, out var parsed))
            throw new InvalidOperationException("Ungueltiger Medientyp.");

        return parsed;
    }

    private static bool TryParseKnownMediaType(string mediaType, out MediaType parsed)
    {
        return Enum.TryParse(mediaType, ignoreCase: true, out parsed) && MediaTypeHandlers.ContainsKey(parsed);
    }

    private async Task<(List<PlaylistEntry> EntriesToAdd, int SkippedDuplicateCount, PlaylistEntry? TopLevelEntry, int ExistingEntryCount)> BuildEntriesToAddAsync(
        long playlistId, MediaType parsedMediaType, string normalizedMediaType, long mediaId, CancellationToken cancellationToken)
    {
        var existingKeys = (await _db.PlaylistEntries
            .AsNoTracking()
            .Where(e => e.PlaylistId == playlistId)
            .Select(e => new { e.MediaType, e.MediaId })
            .ToListAsync(cancellationToken))
            .Select(e => (e.MediaType, e.MediaId))
            .ToHashSet();

        var now = DateTime.UtcNow;
        var entriesToAdd = new List<PlaylistEntry>();
        var skippedDuplicateCount = 0;
        PlaylistEntry? topLevelEntry = null;

        if (existingKeys.Contains((normalizedMediaType, mediaId)))
        {
            skippedDuplicateCount++;
        }
        else
        {
            topLevelEntry = new PlaylistEntry
            {
                PlaylistId = playlistId,
                MediaType = normalizedMediaType,
                MediaId = mediaId,
                ParentMediaType = null,
                ParentMediaId = null,
                AddedAt = now
            };
            entriesToAdd.Add(topLevelEntry);
        }

        var cascadeEntries = await GetCascadeMediaIdsAsync(parsedMediaType, mediaId, cancellationToken);
        foreach (var (childMediaType, childMediaId) in cascadeEntries)
        {
            var normalizedChildMediaType = childMediaType.ToString();
            if (existingKeys.Contains((normalizedChildMediaType, childMediaId)))
            {
                skippedDuplicateCount++;
                continue;
            }

            entriesToAdd.Add(new PlaylistEntry
            {
                PlaylistId = playlistId,
                MediaType = normalizedChildMediaType,
                MediaId = childMediaId,
                ParentMediaType = normalizedMediaType,
                ParentMediaId = mediaId,
                AddedAt = now
            });
        }

        return (entriesToAdd, skippedDuplicateCount, topLevelEntry, existingKeys.Count);
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
        if (mediaIds.Count == 0 || !TryParseKnownMediaType(mediaType, out var parsedType))
            return new Dictionary<long, string>();

        var handler = MediaTypeHandlers[parsedType];
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
        if (mediaIds.Count == 0 || !TryParseKnownMediaType(mediaType, out var parsedType))
            return new HashSet<long>();

        var handler = MediaTypeHandlers[parsedType];
        return await handler.LoadExistingIdsAsync(_db, mediaIds, cancellationToken);
    }

    /// <summary>
    /// Builds the <see cref="DtoPlaylistEntry"/> array for the given entries, resolving titles, resolved
    /// picture ids and unlock/access status in bulk (a handful of queries scaled to media type, not to
    /// the number of entries), analogous to <see cref="LoadTitlesForMediaRefsAsync"/>.
    /// </summary>
    /// <param name="entries">The playlist entries to convert.</param>
    /// <param name="userId">The id of the user the accessibility check is performed for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resulting <see cref="DtoPlaylistEntry"/> array, in the same order as <paramref name="entries"/>.</returns>
    private async Task<DtoPlaylistEntry[]> BuildEntryDtosAsync(List<PlaylistEntry> entries, string userId, CancellationToken cancellationToken)
    {
        var titlesByType = await LoadTitlesForMediaRefsAsync(entries.Select(e => (e.MediaType, e.MediaId)), cancellationToken);
        var parentTitlesByType = await LoadTitlesForMediaRefsAsync(GetParentMediaRefs(entries), cancellationToken);
        var pictureIdsByType = await LoadPictureIdsForMediaRefsAsync(entries.Select(e => (e.MediaType, e.MediaId)), cancellationToken);
        var (unlockedMovieCollectionIds, unlockedTVShowIds) = await LoadUnlockedMediaIdsAsync(userId, cancellationToken);

        return entries
            .Select(entry => new DtoPlaylistEntry
            {
                Id = entry.Id,
                PlaylistId = entry.PlaylistId,
                MediaType = entry.MediaType,
                MediaId = entry.MediaId,
                MediaTitle = titlesByType[entry.MediaType][entry.MediaId],
                ParentMediaType = entry.ParentMediaType,
                ParentMediaId = entry.ParentMediaId,
                ParentMediaTitle = GetParentTitle(entry, parentTitlesByType),
                AddedAt = entry.AddedAt,
                ResolvedPictureId = GetPictureId(entry, pictureIdsByType),
                IsAccessible = IsEntryAccessible(entry, unlockedMovieCollectionIds, unlockedTVShowIds)
            })
            .ToArray();
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
        IEnumerable<(string MediaType, long MediaId)> mediaRefs, CancellationToken cancellationToken)
    {
        var idsByType = new Dictionary<string, HashSet<long>>();
        foreach (var (mediaType, mediaId) in mediaRefs)
            AddMediaId(idsByType, mediaType, mediaId);

        var pictureIdsByType = new Dictionary<string, Dictionary<long, long?>>();
        foreach (var (mediaType, mediaIds) in idsByType)
        {
            if (!TryParseKnownMediaType(mediaType, out var parsedType))
                continue;

            var handler = MediaTypeHandlers[parsedType];
            pictureIdsByType[mediaType] = await handler.LoadPictureIdsAsync(_db, mediaIds, cancellationToken);
        }

        return pictureIdsByType;
    }

    private static long? GetPictureId(PlaylistEntry entry, Dictionary<string, Dictionary<long, long?>> pictureIdsByType)
        => pictureIdsByType.TryGetValue(entry.MediaType, out var pictureIds) && pictureIds.TryGetValue(entry.MediaId, out var pictureId)
            ? pictureId
            : null;

    // Bulk-loads the ids of all movie collections and TV shows currently unlocked for the given user,
    // via IUnlockedMediaService.GetUnlockedMovieCollectionIdsForUserAsync and
    // IUnlockedMediaService.GetUnlockedTVShowIdsForUserAsync (one query each, regardless of the number
    // of playlist entries), so accessibility can then be checked per entry in-memory.
    private async Task<(HashSet<long> UnlockedMovieCollectionIds, HashSet<long> UnlockedTVShowIds)> LoadUnlockedMediaIdsAsync(
        string userId, CancellationToken cancellationToken)
    {
        var unlockedMovieCollectionIds = await _unlockedMediaService.GetUnlockedMovieCollectionIdsForUserAsync(userId, cancellationToken);
        var unlockedTVShowIds = await _unlockedMediaService.GetUnlockedTVShowIdsForUserAsync(userId, cancellationToken);
        return (unlockedMovieCollectionIds.ToHashSet(), unlockedTVShowIds.ToHashSet());
    }

    /// <summary>
    /// Checks whether the given entry is accessible, based on the given sets of unlocked movie
    /// collection and TV show ids. Only movie collections and TV shows can be unlocked; other media
    /// types (movies, seasons, episodes) are therefore never reported as accessible.
    /// </summary>
    /// <param name="entry">The playlist entry to check.</param>
    /// <param name="unlockedMovieCollectionIds">The ids of movie collections unlocked for the user.</param>
    /// <param name="unlockedTVShowIds">The ids of TV shows unlocked for the user.</param>
    /// <returns><c>true</c> if the entry is accessible; otherwise, <c>false</c>.</returns>
    private static bool IsEntryAccessible(PlaylistEntry entry, HashSet<long> unlockedMovieCollectionIds, HashSet<long> unlockedTVShowIds)
    {
        if (!TryParseKnownMediaType(entry.MediaType, out var parsedType))
            return false;

        return parsedType switch
        {
            MediaType.MovieCollection => unlockedMovieCollectionIds.Contains(entry.MediaId),
            MediaType.TVShow => unlockedTVShowIds.Contains(entry.MediaId),
            _ => false
        };
    }

    /// <inheritdoc />
    public async Task<DtoPlaylistEntriesPagedResult> GetPlaylistEntriesPagedAsync(long playlistId, string userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var playlist = await GetOwnedPlaylistAsync(playlistId, userId, cancellationToken);

        var validEntries = await LoadValidPlaylistEntriesAsync(playlistId, cancellationToken);

        var sortedEntries = playlist.SortMode == PlaylistSortMode.ByReleaseDate
            ? await SortPlaylistEntriesByReleaseDateAsync(validEntries, cancellationToken)
            : validEntries.OrderBy(e => e.AddedAt).ToList();

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

        var mediaIdsByType = new Dictionary<string, HashSet<long>>();
        foreach (var entry in entries)
            AddMediaId(mediaIdsByType, entry.MediaType, entry.MediaId);

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
        IEnumerable<(string MediaType, long MediaId)> mediaRefs, CancellationToken cancellationToken)
    {
        var idsByType = new Dictionary<string, HashSet<long>>();
        foreach (var (mediaType, mediaId) in mediaRefs)
            AddMediaId(idsByType, mediaType, mediaId);

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
    private static IEnumerable<(string MediaType, long MediaId)> GetParentMediaRefs(IEnumerable<PlaylistEntry> entries) =>
        entries
            .Where(e => e.ParentMediaType is not null && e.ParentMediaId is not null)
            .Select(e => (e.ParentMediaType!, e.ParentMediaId!.Value));

    private static string? GetParentTitle(PlaylistEntry entry, Dictionary<string, Dictionary<long, string>> parentTitlesByType)
    {
        if (entry.ParentMediaType is null || entry.ParentMediaId is null)
            return null;

        return parentTitlesByType.TryGetValue(entry.ParentMediaType, out var titles) && titles.TryGetValue(entry.ParentMediaId.Value, out var title)
            ? title
            : null;
    }

    private static void AddMediaId(Dictionary<string, HashSet<long>> map, string mediaType, long mediaId)
    {
        if (!map.TryGetValue(mediaType, out var ids))
            map[mediaType] = ids = new HashSet<long>();
        ids.Add(mediaId);
    }

    private async Task<List<PlaylistEntry>> SortPlaylistEntriesByReleaseDateAsync(List<PlaylistEntry> entries, CancellationToken cancellationToken)
    {
        var mediaIdsByType = new Dictionary<string, HashSet<long>>();
        foreach (var entry in entries)
            AddMediaId(mediaIdsByType, entry.MediaType, entry.MediaId);

        var releaseDatesByType = new Dictionary<string, Dictionary<long, DateTime?>>();
        var hierarchyByType = new Dictionary<string, Dictionary<long, (long? ParentId, int? SequenceNumber)>>();
        foreach (var (mediaType, mediaIds) in mediaIdsByType)
        {
            if (!TryParseKnownMediaType(mediaType, out var parsedType))
                continue;

            var handler = MediaTypeHandlers[parsedType];
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
