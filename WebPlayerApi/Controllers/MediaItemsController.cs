using Microsoft.AspNetCore.Mvc;
using WebPlayerApi.Models;
using WebPlayerApi.Services;

namespace WebPlayerApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MediaItemsController : Controller
    {
        private readonly ISourceService sourceService;

        public MediaItemsController(ISourceService sourceService)
        {
            this.sourceService = sourceService;
        }

        [HttpGet("{sourceId}")]
        public ActionResult<IEnumerable<MediaItemDto>> Index(string sourceId, [FromQuery] int offset = 0, [FromQuery] int count = 10)
        {
            var source = sourceService.Get(sourceId);
            if (source is null)
                return NotFound();
            var mediaService = sourceService.GetMediaService(source);
            return Ok(mediaService.Items.Skip(offset).Take(count > 0 ? count : int.MaxValue).Select(mi => new MediaItemDto()
            {
                FilePath = mi.FilePath,
                Id = mi.Id,
                ImagePaths = mi.ImagePaths,
                PictureBase64 = mi.Picture is null ? string.Empty : Convert.ToBase64String(mi.Picture),
                Plot = mi.Plot,
                ReleaseDate = mi.ReleaseDate,
                Title = mi.Title,
                Type = mi.Type
            }).ToList());
        }

        [HttpGet("details/{id}")]
        public ActionResult<MediaItemDetailsDto> Details(string id)
        { 
            foreach (var source in sourceService.Items)
            {
                var mediaService = sourceService.GetMediaService(source);
                var item = mediaService.Get(id);
                if (item is null)
                    item = mediaService.Items.FirstOrDefault(i => i.Children is not null && i.Children.Any(c => c.Id == id));
                if (item is not null)
                    return Ok(new MediaItemDetailsDto()
                    {
                        FilePath = item.FilePath,
                        Id = item.Id,
                        ImagePaths = item.ImagePaths,
                        Title = item.Title,
                        Type = item.Type,
                        ReleaseDate = item.ReleaseDate,
                        PictureBase64 = item.Picture is null ? string.Empty : Convert.ToBase64String(item.Picture),
                        Children = item.Children?.Select(i => new MediaItemDto()
                        {
                            FilePath = i.FilePath,
                            Id = i.Id,
                            ImagePaths = i.ImagePaths,
                            Title = i.Title,
                            Type = i.Type,
                            Plot = i.Plot,
                            PictureBase64 = i.Picture is null ? string.Empty : Convert.ToBase64String(i.Picture),
                            ReleaseDate = i.ReleaseDate
                        }).ToArray()
                    });
            }
            return null;
        }


        [HttpGet("stream/{id}")]
        public IActionResult StreamVideo(string id)
        {
            foreach(var source in sourceService.Items)
            {
                var mediaService = sourceService.GetMediaService(source);
                var stream = mediaService.GetMediaStream(id);
                if ( stream is not null)
                    return File(stream, "video/mp4", enableRangeProcessing: true); // wichtig für Streaming
            }
            return NotFound();
        }

        [HttpGet("download/{id}")]
        public IActionResult DownloadVideo(string id)
        {
            foreach (var source in sourceService.Items)
            {
                var mediaService = sourceService.GetMediaService(source);
                var item = mediaService.Items.Where(i => i.Children is not null).SelectMany(i => i.Children).FirstOrDefault(c => c.Id == id);
                if (item is null)
                    continue;
                var stream = mediaService.GetMediaStream(id);
                var fileName = $"{item.Title}.mp4";
                return File(stream, "application/octet-stream", fileName);
            }
            return NotFound();
        }
    }
}
