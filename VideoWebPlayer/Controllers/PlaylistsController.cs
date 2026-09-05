using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;

/// <summary>
/// Provides endpoints for managing playlists.
/// </summary>
[ApiController]
[Route("api/playlists")]
[BearerTokenCheck]
public class PlaylistsController : ApiBaseController
{
    private readonly IPlaylistService _playlistService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistsController"/> class.
    /// </summary>
    /// <param name="playlistService">Playlist service.</param>
    /// <param name="authService">Authentication service.</param>
    /// <param name="logger">Logger instance.</param>
    public PlaylistsController(IPlaylistService playlistService, IAuthService authService, ILogger<PlaylistsController> logger)
        : base(authService, logger)
    {
        _playlistService = playlistService;
    }

    /// <summary>
    /// Gets all playlists for the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPlaylists()
    {
        try
        {
            CheckLogedIn();
            var result = await _playlistService.GetPlaylistsAsync(CurrentUser!.Id, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff ohne Anmeldung beim Abrufen der Playlists");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Abrufen der Playlists");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets a single playlist for the current user.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetPlaylist(long id)
    {
        try
        {
            CheckLogedIn();
            var result = await _playlistService.GetPlaylistAsync(id, CurrentUser!.Id, HttpContext.RequestAborted);
            if (result is null)
                return NotFound();

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff ohne Anmeldung beim Abrufen der Playlist {PlaylistId}", id);
            return Unauthorized(ex.Message);
        }
        catch (PlaylistAccessDeniedException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen der Playlist {PlaylistId}", id);
            return StatusCode(403, ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Abrufen der Playlist {PlaylistId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Creates a new playlist for the current user.
    /// </summary>
    /// <param name="request">The create request.</param>
    [HttpPost]
    public async Task<IActionResult> CreatePlaylist([FromBody] DtoCreatePlaylistRequest request)
    {
        try
        {
            CheckLogedIn();
            var result = await _playlistService.CreatePlaylistAsync(
                CurrentUser!.Id, request.Name, request.Description, request.SortMode, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff ohne Anmeldung beim Erstellen der Playlist");
            return Unauthorized(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Fehler beim Erstellen der Playlist");
            return MapInvalidOperationException(ex);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Erstellen der Playlist");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Updates an existing playlist for the current user.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <param name="request">The update request.</param>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePlaylist(long id, [FromBody] DtoUpdatePlaylistRequest request)
    {
        try
        {
            CheckLogedIn();
            var result = await _playlistService.UpdatePlaylistAsync(
                id, CurrentUser!.Id, request.Name, request.Description, request.SortMode, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Playlist {PlaylistId} wurde beim Aktualisieren nicht gefunden", id);
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff ohne Anmeldung beim Aktualisieren der Playlist {PlaylistId}", id);
            return Unauthorized(ex.Message);
        }
        catch (PlaylistAccessDeniedException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Aktualisieren der Playlist {PlaylistId}", id);
            return StatusCode(403, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Fehler beim Aktualisieren der Playlist {PlaylistId}", id);
            return MapInvalidOperationException(ex);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Aktualisieren der Playlist {PlaylistId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Deletes a playlist for the current user.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePlaylist(long id)
    {
        try
        {
            CheckLogedIn();
            await _playlistService.DeletePlaylistAsync(id, CurrentUser!.Id, HttpContext.RequestAborted);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Playlist {PlaylistId} wurde beim Loeschen nicht gefunden", id);
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff ohne Anmeldung beim Loeschen der Playlist {PlaylistId}", id);
            return Unauthorized(ex.Message);
        }
        catch (PlaylistAccessDeniedException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Loeschen der Playlist {PlaylistId}", id);
            return StatusCode(403, ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Loeschen der Playlist {PlaylistId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    private static IActionResult MapInvalidOperationException(InvalidOperationException ex, string conflictSubstring = "existiert bereits")
    {
        if (ex.Message.Contains(conflictSubstring, StringComparison.OrdinalIgnoreCase))
            return new ConflictObjectResult(ex.Message);

        return new BadRequestObjectResult(ex.Message);
    }

    /// <summary>
    /// Adds a media entry to a playlist for the current user.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <param name="request">The add request.</param>
    [HttpPost("{id}/entries")]
    public async Task<IActionResult> AddMediaToPlaylist(long id, [FromBody] DtoAddMediaToPlaylistRequest request)
    {
        try
        {
            CheckLogedIn();
            var result = await _playlistService.AddMediaToPlaylistAsync(
                id, CurrentUser!.Id, request.MediaType, request.MediaId, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Medieninhalt oder Playlist {PlaylistId} wurde beim Hinzufuegen nicht gefunden", id);
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff ohne Anmeldung beim Hinzufuegen zur Playlist {PlaylistId}", id);
            return Unauthorized(ex.Message);
        }
        catch (PlaylistAccessDeniedException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Hinzufuegen zur Playlist {PlaylistId}", id);
            return StatusCode(403, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Fehler beim Hinzufuegen zur Playlist {PlaylistId}", id);
            return MapInvalidOperationException(ex);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Hinzufuegen zur Playlist {PlaylistId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Removes a media entry from a playlist for the current user.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <param name="mediaType">The media type of the entry to remove.</param>
    /// <param name="mediaId">The media identifier of the entry to remove.</param>
    [HttpDelete("{id}/entries/{mediaType}/{mediaId}")]
    public async Task<IActionResult> RemoveMediaFromPlaylist(long id, string mediaType, long mediaId)
    {
        try
        {
            CheckLogedIn();
            await _playlistService.RemoveMediaFromPlaylistAsync(id, CurrentUser!.Id, mediaType, mediaId, HttpContext.RequestAborted);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Eintrag wurde beim Entfernen aus Playlist {PlaylistId} nicht gefunden", id);
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff ohne Anmeldung beim Entfernen aus Playlist {PlaylistId}", id);
            return Unauthorized(ex.Message);
        }
        catch (PlaylistAccessDeniedException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Entfernen aus Playlist {PlaylistId}", id);
            return StatusCode(403, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Fehler beim Entfernen aus Playlist {PlaylistId}", id);
            return MapInvalidOperationException(ex);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Entfernen aus Playlist {PlaylistId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets all entries of a playlist for the current user.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    [HttpGet("{id}/entries")]
    public async Task<IActionResult> GetPlaylistEntries(long id)
    {
        try
        {
            CheckLogedIn();
            var result = await _playlistService.GetPlaylistEntriesAsync(id, CurrentUser!.Id, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Playlist {PlaylistId} wurde beim Abrufen der Eintraege nicht gefunden", id);
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff ohne Anmeldung beim Abrufen der Eintraege von Playlist {PlaylistId}", id);
            return Unauthorized(ex.Message);
        }
        catch (PlaylistAccessDeniedException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim Abrufen der Eintraege von Playlist {PlaylistId}", id);
            return StatusCode(403, ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim Abrufen der Eintraege von Playlist {PlaylistId}", id);
            return StatusCode(500, "Internal server error");
        }
    }
}
