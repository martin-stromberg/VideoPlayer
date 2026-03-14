using VideoWebPlayer.Maui.Models;
using VideoWebPlayer.Maui.ViewModels;

namespace VideoWebPlayer.Maui.Services;

/// <summary>
/// Provides a persistent cache for MediaCarouselViewModel items.
/// Carousel items are stored in the local database so they can be displayed
/// immediately on startup before the server responds.
/// </summary>
public class ElementCacheService
{
    private static ElementCacheService? _instance;
    public static ElementCacheService Instance => _instance ??= new ElementCacheService();

    private readonly ClientDatabase _db = ClientDatabase.Instance;

    /// <summary>
    /// Returns all cached items for the given carousel, ordered by their stored position.
    /// </summary>
    public async Task<List<CachedCarouselItem>> GetCachedItemsAsync(string carouselName)
    {
        await _db.Lock.WaitAsync();
        try
        {
            return await _db.Database.Table<CachedCarouselItem>()
                .Where(c => c.CarouselName == carouselName)
                .OrderBy(c => c.SortOrder)
                .ToListAsync();
        }
        finally
        {
            _db.Lock.Release();
        }
    }

    /// <summary>
    /// Replaces all cached items for the given carousel with the supplied items.
    /// </summary>
    public async Task SaveCachedItemsAsync(string carouselName, IEnumerable<MediaItemViewModel> items)
    {
        await _db.Lock.WaitAsync();
        try
        {
            await _db.Database.ExecuteAsync(
                "DELETE FROM CarouselCache WHERE CarouselName = ?", carouselName);

            int order = 0;
            foreach (var item in items)
            {
                var cached = new CachedCarouselItem
                {
                    CarouselName = carouselName,
                    SortOrder = order++,
                    EntryId = item.EntryId ?? 0,
                    MediaType = item.MediaType,
                    Title = item.Title,
                    ImageUrl = item.ImageUrl,
                    PosterPictureId = item.PosterPictureId,
                    SeasonId = item.SeasonId,
                    EpisodeId = item.EpisodeId,
                    CachedAt = DateTime.Now
                };
                await _db.Database.InsertAsync(cached);
            }

            System.Diagnostics.Debug.WriteLine($"[ElementCacheService] Saved {order} items for '{carouselName}'");
        }
        finally
        {
            _db.Lock.Release();
        }
    }
}
