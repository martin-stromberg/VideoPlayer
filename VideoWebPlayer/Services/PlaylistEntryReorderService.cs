using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Implements the manual-sort-order mutation logic for playlist entries (single and batch reorder, plus
/// the "current maximum SortOrder" lookup shared by the "move to end" quick action and by
/// <see cref="PlaylistService"/> when appending newly added entries). Extracted out of
/// <see cref="PlaylistService"/> - analogous to <see cref="PlaylistEntryAccessResolver"/> - so that class
/// is not also responsible for validating and persisting <see cref="PlaylistEntry.SortOrder"/> mutations
/// on top of CRUD, cascade-add, paging and DTO enrichment. Deliberately does not build
/// <see cref="Client.Models.DtoPlaylistEntry"/> results itself: callers (only <see cref="PlaylistService"/>)
/// convert the returned entities via their own DTO-building pipeline, so this class stays free of the
/// title/picture/access-check concerns that pipeline needs.
/// </summary>
internal sealed class PlaylistEntryReorderService
{
    private readonly ApplicationDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistEntryReorderService"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    public PlaylistEntryReorderService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Throws <see cref="PlaylistNotInManualSortModeException"/> unless the given playlist is in
    /// <see cref="PlaylistSortMode.Manual"/> mode. Shared by every operation that mutates
    /// <see cref="PlaylistEntry.SortOrder"/>, so the check is not duplicated at each call site.
    /// </summary>
    /// <param name="playlist">The playlist to check.</param>
    public static void EnsureManualSortMode(Playlist playlist)
    {
        if (playlist.SortMode != PlaylistSortMode.Manual)
            throw new PlaylistNotInManualSortModeException("Playlist befindet sich nicht im manuellen Sortiermodus.");
    }

    /// <summary>
    /// Returns the current maximum <see cref="PlaylistEntry.SortOrder"/> across the entire playlist (not
    /// just a page/subset of it), or <c>null</c> if the playlist has no entries with a SortOrder yet.
    /// </summary>
    /// <param name="playlistId">The playlist identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current maximum <see cref="PlaylistEntry.SortOrder"/>, or <c>null</c> if none is set.</returns>
    public Task<long?> GetMaxSortOrderAsync(long playlistId, CancellationToken cancellationToken)
        => _db.PlaylistEntries
            .Where(e => e.PlaylistId == playlistId)
            .MaxAsync(e => (long?)e.SortOrder, cancellationToken);

    /// <summary>
    /// Changes the manual sort order of a single entry of the given (already ownership-checked) playlist.
    /// </summary>
    /// <param name="playlist">The owning playlist, which must be in <see cref="PlaylistSortMode.Manual"/> mode.</param>
    /// <param name="entryId">The playlist entry identifier.</param>
    /// <param name="newSortOrder">The new sort order; must be non-negative.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated entry.</returns>
    public async Task<PlaylistEntry> ReorderEntryAsync(Playlist playlist, long entryId, long newSortOrder, CancellationToken cancellationToken)
    {
        EnsureManualSortMode(playlist);

        if (newSortOrder < 0)
            throw new ArgumentException("newSortOrder darf nicht negativ sein.");

        // Anders als BatchReorderEntriesAsync (das mehrere Eintraege atomar in einer Anfrage neu anordnet
        // und daher eindeutige Werte innerhalb der Anfrage erzwingt) prueft diese Methode absichtlich
        // nicht, ob newSortOrder bereits von einem anderen Eintrag verwendet wird: Drag & Drop setzt
        // bewusst denselben Wert wie der Ziel-Eintrag, Gleichstaende werden beim Lesen ueber
        // ThenBy(AddedAt) aufgeloest. Die "An Anfang"-Schnellaktion nutzt dagegen die dedizierte
        // MoveEntryToBeginningAsync-Methode unten, da ein Gleichstand bei SortOrder=0 wegen dieses
        // Tiebreaks nie zuverlaessig zur sichtbaren ersten Position fuehrt (der ohnehin an Position 0
        // stehende Eintrag hat praktisch immer das frühere AddedAt und würde die Gleichstand-Aufloesung
        // gewinnen).
        var entry = await _db.PlaylistEntries
            .FirstOrDefaultAsync(e => e.Id == entryId && e.PlaylistId == playlist.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Playlist-Eintrag wurde nicht gefunden.");

        entry.SortOrder = newSortOrder;
        await _db.SaveChangesAsync(cancellationToken);

        return entry;
    }

    /// <summary>
    /// Moves a single entry of the given (already ownership-checked) playlist to the true beginning of the
    /// manual order. Unlike <see cref="ReorderEntryAsync"/> with <c>newSortOrder = 0</c> (which would only
    /// collide with whichever entry already occupies position 0, and lose the resulting
    /// <c>ThenBy(AddedAt)</c> tie-break every time, since the current front entry's <c>AddedAt</c> is
    /// always earlier), this is just <see cref="MoveEntryBetweenAsync"/> with a target position of 0: only
    /// the entries between the moved entry's current position and the beginning are shifted, rather than
    /// every other entry in the playlist.
    /// </summary>
    /// <param name="playlist">The owning playlist, which must be in <see cref="PlaylistSortMode.Manual"/> mode.</param>
    /// <param name="entryId">The playlist entry identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated entry.</returns>
    public Task<PlaylistEntry> MoveEntryToBeginningAsync(Playlist playlist, long entryId, CancellationToken cancellationToken)
        => MoveEntryBetweenAsync(playlist, entryId, 0, cancellationToken);

    /// <summary>
    /// Atomically changes the manual sort order of multiple entries of the given (already
    /// ownership-checked) playlist. Relies on <see cref="DbContext.SaveChangesAsync(CancellationToken)"/>'s
    /// own implicit transaction for atomicity (a single <c>SaveChangesAsync</c> call is already all-or-
    /// nothing), so no explicit <see cref="Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction"/>
    /// is needed around it.
    /// </summary>
    /// <param name="playlist">The owning playlist, which must be in <see cref="PlaylistSortMode.Manual"/> mode.</param>
    /// <param name="reorderOperations">The (entry id, new sort order) pairs to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated entries.</returns>
    public async Task<List<PlaylistEntry>> BatchReorderEntriesAsync(
        Playlist playlist, List<(long EntryId, long NewSortOrder)> reorderOperations, CancellationToken cancellationToken)
    {
        EnsureManualSortMode(playlist);

        if (reorderOperations.Count == 0)
            throw new ArgumentException("Die Liste der Umordnungs-Operationen darf nicht leer sein.");

        if (reorderOperations.Any(op => op.NewSortOrder < 0))
            throw new ArgumentException("newSortOrder darf nicht negativ sein.");

        var entryIds = reorderOperations.Select(op => op.EntryId).ToList();
        if (entryIds.Count != entryIds.Distinct().Count())
            throw new ArgumentException("Doppelte EntryId in der Anfrage.");

        var sortOrders = reorderOperations.Select(op => op.NewSortOrder).ToList();
        if (sortOrders.Count != sortOrders.Distinct().Count())
            throw new InvalidOperationException("Doppelte SortOrder in den Umordnungs-Operationen.");

        var entries = await _db.PlaylistEntries
            .Where(e => e.PlaylistId == playlist.Id && entryIds.Contains(e.Id))
            .ToListAsync(cancellationToken);
        if (entries.Count != reorderOperations.Count)
            throw new KeyNotFoundException("Ein oder mehrere Playlist-Eintraege wurden nicht gefunden.");

        foreach (var operation in reorderOperations)
        {
            var entry = entries.First(e => e.Id == operation.EntryId);
            entry.SortOrder = operation.NewSortOrder;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return entries;
    }

    /// <summary>
    /// Moves a single entry of the given (already ownership-checked) playlist to an arbitrary target
    /// position, shifting every other entry's <see cref="PlaylistEntry.SortOrder"/> between the entry's
    /// current and target position by one first - the general case <see cref="MoveEntryToBeginningAsync"/>
    /// delegates to for a target position of 0. Used by drag & drop reordering, which - unlike
    /// <see cref="ReorderEntryAsync"/> - must not simply collide with the target entry's SortOrder (that
    /// would leave the resulting order to the unpredictable <c>ThenBy(AddedAt)</c> tie-break).
    /// </summary>
    /// <param name="playlist">The owning playlist, which must be in <see cref="PlaylistSortMode.Manual"/> mode.</param>
    /// <param name="entryId">The playlist entry identifier.</param>
    /// <param name="targetSortOrder">The target sort order; must be non-negative.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated entry.</returns>
    public async Task<PlaylistEntry> MoveEntryBetweenAsync(Playlist playlist, long entryId, long targetSortOrder, CancellationToken cancellationToken)
    {
        EnsureManualSortMode(playlist);

        if (targetSortOrder < 0)
            throw new ArgumentException("targetSortOrder darf nicht negativ sein.");

        var entry = await _db.PlaylistEntries
            .FirstOrDefaultAsync(e => e.Id == entryId && e.PlaylistId == playlist.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Playlist-Eintrag wurde nicht gefunden.");

        if (entry.SortOrder is not { } currentSortOrder)
            throw new InvalidOperationException("Eintrag hat keine gueltige Sortierreihenfolge.");

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        if (targetSortOrder < currentSortOrder)
        {
            await _db.PlaylistEntries
                .Where(e => e.PlaylistId == playlist.Id && e.Id != entryId
                    && e.SortOrder >= targetSortOrder && e.SortOrder < currentSortOrder)
                .ExecuteUpdateAsync(setters => setters.SetProperty(e => e.SortOrder, e => e.SortOrder + 1), cancellationToken);
        }
        else
        {
            await _db.PlaylistEntries
                .Where(e => e.PlaylistId == playlist.Id && e.Id != entryId
                    && e.SortOrder > currentSortOrder && e.SortOrder <= targetSortOrder)
                .ExecuteUpdateAsync(setters => setters.SetProperty(e => e.SortOrder, e => e.SortOrder - 1), cancellationToken);
        }

        entry.SortOrder = targetSortOrder;
        await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return entry;
    }
}
