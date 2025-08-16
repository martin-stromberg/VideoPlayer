using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services; // Namespace für SftpMediaSourceReader

[ApiController]
[Route("api/episodes")]
[ConnectionCheck]
public class EpisodesController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly SftpMediaSourceReader _sftpReader;

    public EpisodesController(ApplicationDbContext dbContext, SftpMediaSourceReader sftpReader)
    {
        _dbContext = dbContext;
        _sftpReader = sftpReader;
    }

    [HttpGet("{id}/stream")]
    public async Task<IActionResult> StreamEpisode(long id)
    {
        var episode = await _dbContext.TVShowEpisodes
            .Include(e => e.TVShowEpisodeMediaItems)
            .ThenInclude(mi => mi.MediaItem)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (episode == null)
            return NotFound();

        var assignment = await _dbContext.TVShowEpisodeMediaItems.FirstOrDefaultAsync(rec => rec.TVShowEpisodeId == episode.Id);
        if (assignment is null)
            return NotFound();

        var mediaItem = await _dbContext.MediaItems.FirstOrDefaultAsync(mi => mi.Id == assignment.MediaItemId);
        if (mediaItem == null || string.IsNullOrEmpty(mediaItem.Path))
            return NotFound();

        // Hole die MediaSource aus der MediaCollection
        var mediaCollection = await _dbContext.MediaCollections
            .Include(mc => mc.MediaSource)
            .FirstOrDefaultAsync(mc => mc.Id == mediaItem.MediaCollectionId);

        // Hole den Stream über den SftpMediaSourceReader
        var stream = _sftpReader.GetSftpFileStream(mediaCollection, Path.GetFileName(mediaItem.Path));

        if (stream == null)
            return NotFound();
        var ext = Path.GetExtension(mediaItem.Path).ToLowerInvariant();
        var contentType = $"video/{ext}"; // Optional: dynamisch bestimmen

        // Stream mit Range-Processing (falls unterstützt)
        return File(stream, contentType);
    }
}