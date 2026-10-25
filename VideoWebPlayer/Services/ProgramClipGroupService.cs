using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Utils;

namespace VideoWebPlayer.Services;

/// <summary>
/// Implements <see cref="IProgramClipGroupService"/>: CRUD for placeholder clip groups and their
/// clip memberships, with server-side administrator enforcement and validation (name rules,
/// reserved "Trailer" name, system-group locks, unclassified-video-only membership).
/// </summary>
public sealed class ProgramClipGroupService : IProgramClipGroupService
{
    private readonly ApplicationDbContext _db;
    private readonly IProgramScheduleSignal? _scheduleSignal;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="scheduleSignal">Optional wake-up line for the schedule worker; fired after
    /// every group change so the plan is re-evaluated immediately (<see langword="null"/> in
    /// tests / when unused).</param>
    public ProgramClipGroupService(ApplicationDbContext db, IProgramScheduleSignal? scheduleSignal = null)
    {
        _db = db;
        _scheduleSignal = scheduleSignal;
    }

    /// <inheritdoc />
    public async Task<List<ProgramClipGroup>> GetGroupsAsync(string userId, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(userId, cancellationToken);
        return await _db.ProgramClipGroups
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ProgramClipGroup> CreateGroupAsync(string userId, string name, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(userId, cancellationToken);
        await ValidateGroupNameAsync(name, null, cancellationToken);

        var now = DateTime.UtcNow;
        var group = new ProgramClipGroup { Name = name.Trim(), CreatedAt = now, UpdatedAt = now };
        _db.ProgramClipGroups.Add(group);
        await _db.SaveChangesAsync(cancellationToken);
        _scheduleSignal?.NotifyScheduleChanged();
        return group;
    }

    /// <inheritdoc />
    public async Task<ProgramClipGroup> RenameGroupAsync(string userId, long groupId, string name, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(userId, cancellationToken);
        var group = await _db.ProgramClipGroups.FindAsync([groupId], cancellationToken)
            ?? throw new KeyNotFoundException("Die Clip-Gruppe wurde nicht gefunden.");
        if (group.IsSystem)
            throw new InvalidOperationException("System-Clip-Gruppen können nicht umbenannt werden.");

        await ValidateGroupNameAsync(name, groupId, cancellationToken);
        group.Name = name.Trim();
        group.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        _scheduleSignal?.NotifyScheduleChanged();
        return group;
    }

    /// <inheritdoc />
    public async Task DeleteGroupAsync(string userId, long groupId, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(userId, cancellationToken);
        var group = await _db.ProgramClipGroups.FindAsync([groupId], cancellationToken)
            ?? throw new KeyNotFoundException("Die Clip-Gruppe wurde nicht gefunden.");
        if (group.IsSystem)
            throw new InvalidOperationException("System-Clip-Gruppen können nicht gelöscht werden.");

        _db.ProgramClipGroups.Remove(group);
        await _db.SaveChangesAsync(cancellationToken);
        _scheduleSignal?.NotifyScheduleChanged();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<long, int>> GetGroupCountsAsync(string userId, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(userId, cancellationToken);
        return await _db.ProgramClipGroupItems
            .AsNoTracking()
            .GroupBy(i => i.ProgramClipGroupId)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<ProgramClipGroupClip>> GetGroupClipsAsync(string userId, long groupId, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(userId, cancellationToken);
        return await _db.ProgramClipGroupItems
            .AsNoTracking()
            .Where(i => i.ProgramClipGroupId == groupId)
            .OrderBy(i => i.MediaItem.Name)
            .ThenBy(i => i.MediaItemId)
            .Select(i => new ProgramClipGroupClip(
                i.MediaItemId,
                i.MediaItem.Name,
                i.MediaItem.MediaCollection.MediaSource.Name,
                i.MediaItem.Duration,
                i.MediaItem.DurationProbedAt,
                i.MediaItem.DurationProbeAttempts))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<ProgramClipGroupCandidate>> GetClipCandidatesAsync(
        string userId, string? search, long? mediaSourceId, int skip, int take, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(userId, cancellationToken);

        var query = _db.MediaItems
            .AsNoTracking()
            .Where(mi => !_db.MovieMediaItems.Any(l => l.MediaItemId == mi.Id)
                      && !_db.TVShowEpisodeMediaItems.Any(l => l.MediaItemId == mi.Id));

        if (mediaSourceId is { } sourceId)
            query = query.Where(mi => mi.MediaCollection.MediaSourceId == sourceId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Wildcard-Escaping wie ItemsController.ApplySearchFilter — %, _ und \ im
            // Suchtext sind keine LIKE-Platzhalter, sondern literal gemeint.
            var escaped = search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            query = query.Where(mi => EF.Functions.Like(mi.Name, $"%{escaped}%", "\\"));
        }

        // The recognized video extensions are evaluated in memory (Path.GetExtension is not
        // translatable); the candidate list per source is small, so the full page is filtered
        // before slicing.
        var items = await query
            .OrderBy(mi => mi.Name)
            .ThenBy(mi => mi.Id)
            .Select(mi => new
            {
                mi.Id,
                mi.Name,
                mi.Path,
                mi.MediaCollection.MediaSourceId,
                SourceName = mi.MediaCollection.MediaSource.Name,
                mi.Duration,
                mi.DurationProbedAt,
                mi.DurationProbeAttempts
            })
            .ToListAsync(cancellationToken);

        return items
            .Where(mi => VideoFileExtensions.IsVideoFileName(mi.Path))
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 200))
            .Select(mi => new ProgramClipGroupCandidate(
                mi.Id, mi.Name, mi.MediaSourceId, mi.SourceName,
                mi.Duration, mi.DurationProbedAt, mi.DurationProbeAttempts))
            .ToList();
    }

    /// <inheritdoc />
    public async Task AddClipsToGroupAsync(string userId, long groupId, IReadOnlyCollection<long> mediaItemIds, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(userId, cancellationToken);
        var group = await _db.ProgramClipGroups.FindAsync([groupId], cancellationToken)
            ?? throw new KeyNotFoundException("Die Clip-Gruppe wurde nicht gefunden.");
        if (group.IsSystem)
            throw new InvalidOperationException("System-Clip-Gruppen können nicht manuell befüllt werden.");

        var ids = mediaItemIds.Distinct().ToList();
        if (ids.Count == 0)
            return;

        var items = await _db.MediaItems
            .Where(mi => ids.Contains(mi.Id))
            .Select(mi => new { mi.Id, mi.Path })
            .ToListAsync(cancellationToken);
        var found = items.Select(i => i.Id).ToHashSet();
        if (ids.Except(found).Any())
            throw new KeyNotFoundException("Ein gewählter Clip wurde nicht gefunden.");

        var linkedMovieItems = (await _db.MovieMediaItems
                .Where(l => ids.Contains(l.MediaItemId))
                .Select(l => l.MediaItemId)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var linkedEpisodeItems = (await _db.TVShowEpisodeMediaItems
                .Where(l => ids.Contains(l.MediaItemId))
                .Select(l => l.MediaItemId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var item in items)
        {
            if (!VideoFileExtensions.IsVideoFileName(item.Path))
                throw new ArgumentException("Nur Videodateien können einer Clip-Gruppe zugeordnet werden.");
            if (linkedMovieItems.Contains(item.Id) || linkedEpisodeItems.Contains(item.Id))
                throw new ArgumentException("Nur nicht klassifizierte Clips können einer Clip-Gruppe zugeordnet werden.");
        }

        var existing = (await _db.ProgramClipGroupItems
                .Where(i => i.ProgramClipGroupId == groupId && ids.Contains(i.MediaItemId))
                .Select(i => i.MediaItemId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var id in ids.Where(id => !existing.Contains(id)))
            _db.ProgramClipGroupItems.Add(new ProgramClipGroupItem { ProgramClipGroupId = groupId, MediaItemId = id });

        await _db.SaveChangesAsync(cancellationToken);
        _scheduleSignal?.NotifyScheduleChanged();
    }

    /// <inheritdoc />
    public async Task RemoveClipFromGroupAsync(string userId, long groupId, long mediaItemId, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(userId, cancellationToken);
        var group = await _db.ProgramClipGroups.FindAsync([groupId], cancellationToken)
            ?? throw new KeyNotFoundException("Die Clip-Gruppe wurde nicht gefunden.");
        if (group.IsSystem)
            throw new InvalidOperationException("Aus System-Clip-Gruppen können keine Clips entfernt werden.");

        var item = await _db.ProgramClipGroupItems.FindAsync([groupId, mediaItemId], cancellationToken);
        if (item is not null)
        {
            _db.ProgramClipGroupItems.Remove(item);
            await _db.SaveChangesAsync(cancellationToken);
        }
        _scheduleSignal?.NotifyScheduleChanged();
    }

    private async Task ValidateGroupNameAsync(string name, long? excludeGroupId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Der Name der Clip-Gruppe darf nicht leer sein.");
        var trimmed = name.Trim();
        if (trimmed.Length > ProgramClipGroup.NameMaxLength)
            throw new ArgumentException($"Der Name der Clip-Gruppe darf höchstens {ProgramClipGroup.NameMaxLength} Zeichen lang sein.");
        if (string.Equals(trimmed, ProgramClipGroup.TrailerGroupName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Der Name „{ProgramClipGroup.TrailerGroupName}“ ist für die reservierte System-Gruppe vorbehalten.");
        if (await _db.ProgramClipGroups.AnyAsync(g => g.Name == trimmed && g.Id != excludeGroupId, cancellationToken))
            throw new ArgumentException($"Eine Clip-Gruppe mit dem Namen „{trimmed}“ existiert bereits.");
    }

    private async Task RequireAdminAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Sie sind nicht angemeldet.");

        if (!user.IsAdmin)
            throw new ForbiddenAccessException("Nur Administratoren dürfen Clip-Gruppen verwalten.");
    }
}
