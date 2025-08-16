using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [ConnectionCheck]
    public class MediaItemsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly SftpMediaSourceReader _sftpReader;

        public MediaItemsController(ApplicationDbContext db, SftpMediaSourceReader sftpReader)
        {
            _db = db;
            _sftpReader = sftpReader;
        }

        [HttpGet("{id}/stream")]
        public async Task<IActionResult> StreamMediaItem(long id)
        {
            var mediaItem = await _db.MediaItems
                .Include(mi => mi.MediaCollection)
                .ThenInclude(mc => mc.MediaSource)
                .FirstOrDefaultAsync(mi => mi.Id == id);

            if (mediaItem == null)
                return NotFound();

            var fileName = Path.GetFileName(mediaItem.Path);
            var stream = _sftpReader.GetSftpFileStream(mediaItem.MediaCollection, fileName);
            if (stream == null)
                return NotFound();

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            var contentType = ext switch
            {
                ".mp4" => "video/mp4",
                ".mkv" => "video/x-matroska",
                ".avi" => "video/x-msvideo",
                ".mpeg" => "video/mpeg",
                _ => "application/octet-stream"
            };

            // enableRangeProcessing: true für Video-Streaming
            return File(stream, contentType, enableRangeProcessing: true);
        }

        [HttpGet("{id}/download")]
        public async Task<IActionResult> Download(long id)
        {
            var mediaItem = await _db.MediaItems
                .FirstOrDefaultAsync(mi => mi.Id == id);
            if (mediaItem == null)
                return NotFound();

            var fileStreamResult = await StreamMediaItem(id) as FileStreamResult;
            if (fileStreamResult == null)
                return NotFound();

            // Optional: Dateiname auslesen
            var fileName = !string.IsNullOrWhiteSpace(fileStreamResult.FileDownloadName) ? fileStreamResult.FileDownloadName : Path.GetFileName(mediaItem.Path) ?? $"video_{mediaItem.Id}.mp4";
            return File(fileStreamResult.FileStream, "application/octet-stream", fileName);
        }
    }
}