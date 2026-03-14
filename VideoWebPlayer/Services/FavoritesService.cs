using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Implements favorites operations backed by <see cref="ApplicationDbContext"/>.
/// </summary>
public sealed class FavoritesService : IFavoritesService
{
	private readonly ApplicationDbContext _db;
	private readonly MediaUpdateNotificationService _notificationService;

	/// <summary>
	/// Initializes a new instance of the <see cref="FavoritesService"/> class.
	/// </summary>
	/// <param name="db">Database context.</param>
	/// <param name="notificationService">SignalR notification service.</param>
	public FavoritesService(ApplicationDbContext db, MediaUpdateNotificationService notificationService)
	{
		_db = db;
		_notificationService = notificationService;
	}

	/// <inheritdoc />
	public async Task<DtoFavoriteEntry[]> GetFavoritesAsync(string userId, CancellationToken cancellationToken = default)
	{
		var favorites = await _db.FavoriteEntries
			.AsNoTracking()
			.Where(f => f.UserId == userId)
			.ToListAsync(cancellationToken);

		var result = new List<DtoFavoriteEntry>(favorites.Count);

		foreach (var rec in favorites)
		{
			var favDto = Create<DtoFavoriteEntry>(rec);
			favDto.Entry = await LoadFavoriteEntryDtoAsync(rec, cancellationToken);
			if (favDto.Entry != null)
				result.Add(favDto);
		}

		return result.ToArray();
	}

	/// <inheritdoc />
	public async Task AddFavoriteAsync(string userId, FavoriteEntry entry, CancellationToken cancellationToken = default)
	{
		entry.UserId = userId;
		entry.CreatedAt = DateTime.UtcNow;
		_db.FavoriteEntries.Add(entry);
		await _db.SaveChangesAsync(cancellationToken);
		await _notificationService.NotifyFavoritesChangedAsync(userId, cancellationToken);
	}

	/// <inheritdoc />
	public async Task RemoveFavoriteAsync(string userId, FavoriteEntry entry, CancellationToken cancellationToken = default)
	{
		var fav = await _db.FavoriteEntries.FirstOrDefaultAsync(
			f => f.UserId == userId &&
				((entry.MovieCollectionId != null && f.MovieCollectionId == entry.MovieCollectionId) ||
				 (entry.TVShowId != null && f.TVShowId == entry.TVShowId) ||
				 (entry.TVShowSeasonId != null && f.TVShowSeasonId == entry.TVShowSeasonId) ||
				 (entry.TVShowEpisodeId != null && f.TVShowEpisodeId == entry.TVShowEpisodeId) ||
				 (entry.MovieId != null && f.MovieId == entry.MovieId)),
			cancellationToken);

		if (fav != null)
		{
			_db.FavoriteEntries.Remove(fav);
			await _db.SaveChangesAsync(cancellationToken);
			await _notificationService.NotifyFavoritesChangedAsync(userId, cancellationToken);
		}
	}

	/// <inheritdoc />
	public async Task<bool> ToggleFavoriteAsync(string userId, DtoMediaEntry entry, CancellationToken cancellationToken = default)
	{
		var exists = await GetFavoriteEntryAsync(userId, entry, cancellationToken);
		if (exists is null)
		{
			await AddFavoriteAsync(userId, new FavoriteEntry
			{
				UserId = userId,
				CreatedAt = DateTime.UtcNow,
				MovieCollectionId = entry is DtoMovieCollection ? entry.Id : null,
				TVShowId = entry is DtoTVShow ? entry.Id : null,
				TVShowSeasonId = entry is DtoTVShowSeason ? entry.Id : null,
				TVShowEpisodeId = entry is DtoTVShowEpisode ? entry.Id : null,
				MovieId = entry is DtoMovie ? entry.Id : null
			}, cancellationToken);
		}
		else
		{
			_db.FavoriteEntries.Remove(exists);
			await _db.SaveChangesAsync(cancellationToken);
			await _notificationService.NotifyFavoritesChangedAsync(userId, cancellationToken);
		}

		exists = await GetFavoriteEntryAsync(userId, entry, cancellationToken);
		return exists is not null;
	}

	private async Task<FavoriteEntry?> GetFavoriteEntryAsync(string userId, DtoMediaEntry entry, CancellationToken cancellationToken)
	{
		var query = _db.FavoriteEntries.AsNoTracking().Where(f => f.UserId == userId);
		if (entry is DtoMovie)
			return await query.FirstOrDefaultAsync(f => f.MovieId == entry.Id, cancellationToken);
		if (entry is DtoMovieCollection)
			return await query.FirstOrDefaultAsync(f => f.MovieCollectionId == entry.Id, cancellationToken);
		if (entry is DtoTVShow)
			return await query.FirstOrDefaultAsync(f => f.TVShowId == entry.Id, cancellationToken);
		if (entry is DtoTVShowSeason)
			return await query.FirstOrDefaultAsync(f => f.TVShowSeasonId == entry.Id, cancellationToken);
		if (entry is DtoTVShowEpisode)
			return await query.FirstOrDefaultAsync(f => f.TVShowEpisodeId == entry.Id, cancellationToken);
		return null;
	}

	private async Task<DtoMediaEntry?> LoadFavoriteEntryDtoAsync(FavoriteEntry rec, CancellationToken cancellationToken)
	{
		if (rec.MovieId is not null)
		{
			var movieEntity = await _db.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.Id == rec.MovieId, cancellationToken);
			if (movieEntity == null)
				return null;

			var movieDto = Create<DtoMovie>(movieEntity);
			if (movieEntity.MovieCollectionId is not null)
			{
				var collectionEntity = await _db.MovieCollections.AsNoTracking()
					.FirstOrDefaultAsync(mc => mc.Id == movieEntity.MovieCollectionId, cancellationToken);
				movieDto.Collection = collectionEntity != null ? Create<DtoMovieCollection>(collectionEntity) : null;
			}

			return movieDto;
		}

		if (rec.MovieCollectionId is not null)
		{
			var collectionEntity = await _db.MovieCollections.AsNoTracking().FirstOrDefaultAsync(m => m.Id == rec.MovieCollectionId, cancellationToken);
			return collectionEntity != null ? Create<DtoMovieCollection>(collectionEntity) : null;
		}

		if (rec.TVShowId is not null)
		{
			var showEntity = await _db.TVShows.AsNoTracking().FirstOrDefaultAsync(m => m.Id == rec.TVShowId, cancellationToken);
			return showEntity != null ? Create<DtoTVShow>(showEntity) : null;
		}

		if (rec.TVShowSeasonId is not null)
		{
			var seasonEntity = await _db.TVShowSeasons.AsNoTracking().FirstOrDefaultAsync(m => m.Id == rec.TVShowSeasonId, cancellationToken);
			if (seasonEntity == null)
				return null;

			var seasonDto = Create<DtoTVShowSeason>(seasonEntity);
			var showEntity = await _db.TVShows.AsNoTracking().FirstOrDefaultAsync(m => m.Id == seasonEntity.TVShowId, cancellationToken);
			seasonDto.Show = showEntity != null ? Create<DtoTVShow>(showEntity) : null;
			return seasonDto;
		}

		if (rec.TVShowEpisodeId is not null)
		{
			var episodeEntity = await _db.TVShowEpisodes.AsNoTracking().FirstOrDefaultAsync(m => m.Id == rec.TVShowEpisodeId, cancellationToken);
			if (episodeEntity == null)
				return null;

			var episodeDto = Create<DtoTVShowEpisode>(episodeEntity);
			var seasonEntity = await _db.TVShowSeasons.AsNoTracking().FirstOrDefaultAsync(s => s.Id == episodeEntity.TVShowSeasonId, cancellationToken);
			if (seasonEntity != null)
			{
				var seasonDto = Create<DtoTVShowSeason>(seasonEntity);
				var showEntity = await _db.TVShows.AsNoTracking().FirstOrDefaultAsync(m => m.Id == seasonEntity.TVShowId, cancellationToken);
				seasonDto.Show = showEntity != null ? Create<DtoTVShow>(showEntity) : null;
				episodeDto.Season = seasonDto;
			}

			return episodeDto;
		}

		return null;
	}

	private static T Create<T>(object ms)
	{
		var sourceType = ms.GetType();
		var record = Activator.CreateInstance<T>();
		foreach (var prop in typeof(T).GetProperties().Where(p => !p.GetCustomAttributes(typeof(IgnoreAssignPropertyAttribute), false).Any()))
		{
			var sourceProp = sourceType.GetProperty(prop.Name);
			if (sourceProp != null && sourceProp.CanRead)
			{
				var value = sourceProp.GetValue(ms);
				prop.SetValue(record, value);
			}
		}
		return record;
	}
}
