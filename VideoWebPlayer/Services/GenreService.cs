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

	/// <summary>
	/// Initializes a new instance of the <see cref="GenreService"/> class.
	/// </summary>
	/// <param name="db">Database context.</param>
	public GenreService(ApplicationDbContext db)
	{
		_db = db;
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
		var now = DateTime.UtcNow.Date;
		var genres = await _db.Genres
			.AsNoTracking()
			.ToListAsync(cancellationToken);

		var active = new List<Genre>();

		foreach (var genre in genres)
		{
			if (genre.StartDate == null || genre.EndDate == null)
				continue;

			var start = genre.StartDate.Value;
			var end = genre.EndDate.Value;

			while (end < now)
			{
				start = start.AddYears(1);
				end = end.AddYears(1);
			}

			if (start <= now && end >= now)
				active.Add(genre);
		}

		return active;
	}
}
