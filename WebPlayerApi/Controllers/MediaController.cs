using Microsoft.AspNetCore.Mvc;
using System.IO;
using WebPlayerApi.Models;
using WebPlayerApi.Service.Data.SFtp;
using WebPlayerApi.Services;
using static System.Net.WebRequestMethods;

namespace WebPlayerApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MediaController : Controller
    {
        private readonly IMediaService _mediaService;

        public MediaController(IMediaService mediaService)
        {
            _mediaService = mediaService;
        }

        [HttpGet("directories")]
        public ActionResult<List<MediaDirectory>> GetDirectories()
        {
            //return _mediaService.GetConfiguredDirectories();
            throw new NotImplementedException();
        }

        [HttpGet("items")]
        public ActionResult<PagedResult<MediaItemDto>> GetMediaItems(
            [FromQuery] string directory,
            [FromQuery] int offset = 1,
            [FromQuery] int count = 20)
        {
            var result = _mediaService.GetMediaItems(directory, offset, count);
            return Ok(result);
        }

        [HttpGet("item")]
        public ActionResult<CardResult<MediaItemDetailsDto>> GetMediaItems([FromQuery] string id)
        {
            var result = _mediaService.GetMediaItem(id);
            return Ok(result);
        }

        [HttpGet("stream")]
        public IActionResult StreamVideo([FromQuery] string id, [FromQuery] string parentId)
        {
            var stream = _mediaService.GetMediaStream(parentId, id);
            if (stream is null)
                return NotFound();
            return File(stream, "video/mp4", enableRangeProcessing: true); // wichtig für Streaming
        }

        [HttpGet("download")]
        public IActionResult DownloadVideo([FromQuery] string id, [FromQuery] string parentId)
        {
            try
            {
                var parent = _mediaService.GetMediaItem(parentId);
                if (parent is null)
                    return NotFound();
                var mediaItem = parent.Item.Children.FirstOrDefault(i => i.Id == id);
                if (mediaItem is null)
                    return NotFound();

                var stream = _mediaService.GetMediaStream(parentId, id);
                if (stream is null)
                    return NotFound();
                var fileName = $"{mediaItem.Title}.mp4";
                return File(stream, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500);
            }
        }

        [HttpGet("reload")]
        public IActionResult ReloadSources([FromQuery] string source)
        {
            try
            {
                _mediaService.ReloadAsync(source);
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500);
            }
        }
    }
}
