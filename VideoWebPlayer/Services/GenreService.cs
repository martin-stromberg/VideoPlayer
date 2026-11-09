using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Implements genre-related domain logic backed by <see cref="ApplicationDbContext"/>.
/// </summary>
public sealed class GenreService : IGenreService
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenreService"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="clock">The server clock.</param>
    public GenreService(ApplicationDbContext db, TimeProvider? clock = null)
    {
        _db = db;
        _clock = clock ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task MarkGenresAsChangedAsync(CancellationToken cancellationToken = default)
    {
        var setup = await _db.Setups.FirstOrDefaultAsync(cancellationToken);
        if (setup != null)
        {
            setup.GenresChanged = true;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<List<Genre>> GetSeasonalGenresAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var genres = await _db.Genres.AsNoTracking().ToListAsync(cancellationToken);
        return genres.Where(g => SeasonalGenreWindow.GetExpiry(g.StartDate, g.EndDate, now).HasValue).ToList();
    }
}
