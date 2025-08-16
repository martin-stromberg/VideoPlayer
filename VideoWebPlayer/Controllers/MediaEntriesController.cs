using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [ConnectionCheck]
    public class MediaEntriesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public MediaEntriesController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<ActionResult<List<MediaEntryDto>>> Get(
            [FromQuery] long? mediaSourceId,
            [FromQuery] int page = 0,
            [FromQuery] int size = 30)
        {
            var movieCollections = (await _db.MovieCollections
                .Where(mc => !mediaSourceId.HasValue || mc.MediaSourceId == mediaSourceId)
                .OrderBy(e => e.Name)
                .Skip(0)
                .Take((page + 1) * size)
                .Select(mc => new MediaEntryDto
                {
                    Id = mc.Id,
                    Title = mc.Name,
                    Description = "",
                    Url = $"/moviecollection/{mc.Id}",
                    CreatedAt = mc.CreatedAt,
                    PictureId = mc.PosterPictureId,
                    ItemCount = _db.Movies.Count(m => m.MovieCollectionId == mc.Id)
                })
                .ToListAsync());

            var tvShows = await _db.TVShows
                .Where(ts => !mediaSourceId.HasValue || ts.MediaSourceId == mediaSourceId)
                .OrderBy(e => e.Name)
                .Skip(0)
                .Take((page + 1) * size)
                .Select(ts => new MediaEntryDto
                {
                    Id = ts.Id,
                    Title = ts.Name,
                    Description = ts.Plot,
                    Url = $"/tvshow/{ts.Id}", // Hier wird die URL gesetzt
                    CreatedAt = ts.CreatedAt,
                    PictureId = ts.PosterPictureId
                })
                .ToListAsync();

            var entries = movieCollections
                .Concat(tvShows)
                .OrderBy(e => e.Title)
                .Skip(page * size)
                .Take(size)
                .ToList();

            return Ok(entries);
        }

        [HttpGet("/api/pictures/{id}")]
        public async Task<IActionResult> GetPicture(long id)
        {
            var picture = await _db.Pictures.FindAsync(id);
            if (picture != null && picture.Data.Length > 0)
                return File(picture.Data, picture.ContentType ?? "image/jpg");

            // Optional: Platzhalterbild
            var placeholderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/placeholder.png");
            if (System.IO.File.Exists(placeholderPath))
            {
                var bytes = await System.IO.File.ReadAllBytesAsync(placeholderPath);
                return File(bytes, "image/png");
            }
            return NotFound();
        }
    }
}