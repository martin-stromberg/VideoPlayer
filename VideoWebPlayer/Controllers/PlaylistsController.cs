using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Configuration;
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
    private readonly PlaylistSettings _playlistSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistsController"/> class.
    /// </summary>
    /// <param name="playlistService">Playlist service.</param>
    /// <param name="authService">Authentication service.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="playlistSettings">Playlist configuration.</param>
    public PlaylistsController(IPlaylistService playlistService, IAuthService authService, ILogger<PlaylistsController> logger, IOptions<PlaylistSettings> playlistSettings)
        : base(authService, logger)
    {
        _playlistService = playlistService;
        _playlistSettings = playlistSettings.Value;
    }

    /// <summary>
    /// Runs <paramref name="action"/> (which is expected to call <see cref="ApiBaseController.CheckLogedIn"/>
    /// itself before touching the playlist service) and maps the exceptions common to every playlist
    /// endpoint to the corresponding HTTP response, centralizing the try/catch block that was previously
    /// duplicated across all playlist actions.
    /// </summary>
    /// <param name="action">The endpoint logic to run.</param>
    /// <param name="logContext">A German gerund phrase describing the action, used in log messages (e.g. "Abrufen der Playlists").</param>
    /// <param name="mapInvalidOperation">
    /// Optional mapping for a plain <see cref="InvalidOperationException"/> to an <see cref="IActionResult"/>.
    /// Defaults to <see cref="BadRequestObjectResult"/> when omitted.
    /// </param>
    /// <returns>The result produced by <paramref name="action"/>, or the mapped error response.</returns>
    private async Task<IActionResult> ExecuteAsync(
        Func<Task<IActionResult>> action,
        string logContext,
        Func<InvalidOperationException, IActionResult>? mapInvalidOperation = null)
    {
        try
        {
            return await action();
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Eintrag nicht gefunden beim {LogContext}", logContext);
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Zugriff ohne Anmeldung beim {LogContext}", logContext);
            return Unauthorized(ex.Message);
        }
        catch (PlaylistAccessDeniedException ex)
        {
            Logger.LogWarning(ex, "Zugriff verweigert beim {LogContext}", logContext);
            return Forbid(JwtBearerDefaults.AuthenticationScheme);
        }
        catch (ManualSortOrderConfirmationRequiredException ex)
        {
            Logger.LogInformation(ex, "Bestaetigung erforderlich beim {LogContext}", logContext);
            return Conflict(new DtoChangeSortModeConflictResponse { IsLossOfDataConfirmationRequired = true });
        }
        catch (PlaylistNotInManualSortModeException ex)
        {
            Logger.LogWarning(ex, "Playlist nicht im manuellen Sortiermodus beim {LogContext}", logContext);
            return Conflict(ex.Message);
        }
        catch (PlaylistNameAlreadyExistsException ex)
        {
            Logger.LogWarning(ex, "Name bereits vergeben beim {LogContext}", logContext);
            return Conflict(ex.Message);
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning(ex, "Ungueltige Eingabe beim {LogContext}", logContext);
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Fehler beim {LogContext}", logContext);
            return mapInvalidOperation?.Invoke(ex) ?? BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Fehler beim {LogContext}", logContext);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Same as <see cref="ExecuteAsync(Func{Task{IActionResult}}, string, Func{InvalidOperationException, IActionResult}?)"/>,
    /// additionally rejecting a <see langword="null"/> <paramref name="request"/> body with 400 Bad
    /// Request before invoking <paramref name="action"/>, centralizing the null-body check that was
    /// previously duplicated at the start of every endpoint with a request body.
    /// </summary>
    /// <typeparam name="TRequest">The request body type.</typeparam>
    /// <param name="request">The deserialized request body, or <see langword="null"/> if the body was empty/invalid.</param>
    /// <param name="action">The endpoint logic to run once <paramref name="request"/> is known to be non-null.</param>
    /// <param name="logContext">A German gerund phrase describing the action, used in log messages.</param>
    /// <param name="mapInvalidOperation">Optional mapping for a plain <see cref="InvalidOperationException"/>, see the base overload.</param>
    /// <returns>The result produced by <paramref name="action"/>, or the mapped error response.</returns>
    private Task<IActionResult> ExecuteAsync<TRequest>(
        TRequest? request,
        Func<TRequest, Task<IActionResult>> action,
        string logContext,
        Func<InvalidOperationException, IActionResult>? mapInvalidOperation = null)
        where TRequest : class
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();
            if (request is null)
                return BadRequest("Der Anfrage-Body darf nicht leer sein.");

            return await action(request);
        }, logContext, mapInvalidOperation);
    }

    /// <summary>
    /// Gets all playlists for the current user.
    /// </summary>
    [HttpGet]
    public Task<IActionResult> GetPlaylists()
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();
            var result = await _playlistService.GetPlaylistsAsync(CurrentUser!.Id, HttpContext.RequestAborted);
            return Ok(result);
        }, "Abrufen der Playlists");
    }

    /// <summary>
    /// Gets a single playlist for the current user.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    [HttpGet("{id}")]
    public Task<IActionResult> GetPlaylist(long id)
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();
            var result = await _playlistService.GetPlaylistAsync(id, CurrentUser!.Id, HttpContext.RequestAborted);
            if (result is null)
                return NotFound();

            return Ok(result);
        }, $"Abrufen der Playlist {id}");
    }

    /// <summary>
    /// Creates a new playlist for the current user.
    /// </summary>
    /// <param name="request">The create request.</param>
    [HttpPost]
    public Task<IActionResult> CreatePlaylist([FromBody] DtoCreatePlaylistRequest request)
    {
        return ExecuteAsync(request, async req =>
        {
            var result = await _playlistService.CreatePlaylistAsync(
                CurrentUser!.Id, req.Name, req.Description, req.SortMode, HttpContext.RequestAborted);
            return Ok(result);
        }, "Erstellen der Playlist");
    }

    /// <summary>
    /// Updates an existing playlist for the current user.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <param name="request">The update request.</param>
    [HttpPut("{id}")]
    public Task<IActionResult> UpdatePlaylist(long id, [FromBody] DtoUpdatePlaylistRequest request)
    {
        return ExecuteAsync(request, async req =>
        {
            var result = await _playlistService.UpdatePlaylistAsync(
                id, CurrentUser!.Id, req.Name, req.Description, req.SortMode, HttpContext.RequestAborted);
            return Ok(result);
        }, $"Aktualisieren der Playlist {id}");
    }

    /// <summary>
    /// Deletes a playlist for the current user.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    [HttpDelete("{id}")]
    public Task<IActionResult> DeletePlaylist(long id)
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();
            await _playlistService.DeletePlaylistAsync(id, CurrentUser!.Id, HttpContext.RequestAborted);
            return NoContent();
        }, $"Loeschen der Playlist {id}");
    }

    /// <summary>
    /// Adds a media entry to a playlist for the current user.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <param name="request">The add request.</param>
    [HttpPost("{id}/entries")]
    public Task<IActionResult> AddMediaToPlaylist(long id, [FromBody] DtoAddMediaToPlaylistRequest request)
    {
        return ExecuteAsync(request, async req =>
        {
            var result = await _playlistService.AddMediaToPlaylistAsync(
                id, CurrentUser!.Id, req.MediaType, req.MediaId, HttpContext.RequestAborted);
            return Ok(result);
        }, $"Hinzufuegen zur Playlist {id}");
    }

    /// <summary>
    /// Removes a media entry from a playlist for the current user.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <param name="mediaType">The media type of the entry to remove.</param>
    /// <param name="mediaId">The media identifier of the entry to remove.</param>
    [HttpDelete("{id}/entries/{mediaType}/{mediaId}")]
    public Task<IActionResult> RemoveMediaFromPlaylist(long id, string mediaType, long mediaId)
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();
            await _playlistService.RemoveMediaFromPlaylistAsync(id, CurrentUser!.Id, mediaType, mediaId, HttpContext.RequestAborted);
            return NoContent();
        }, $"Entfernen aus Playlist {id}");
    }

    /// <summary>
    /// Gets all entries of a playlist for the current user.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    [HttpGet("{id}/entries")]
    public Task<IActionResult> GetPlaylistEntries(long id)
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();
            var result = await _playlistService.GetPlaylistEntriesAsync(id, CurrentUser!.Id, HttpContext.RequestAborted);
            return Ok(result);
        }, $"Abrufen der Eintraege von Playlist {id}");
    }

    /// <summary>
    /// Gets a sorted, paginated page of entries of a playlist for the current user.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <param name="pageSize">The number of entries per page. Defaults to <see cref="PlaylistSettings.DefaultPageSize"/> when omitted.</param>
    [HttpGet("{id}/entries/paged")]
    public Task<IActionResult> GetPlaylistEntriesPaged(long id, int pageNumber = 1, int? pageSize = null)
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();

            var resolvedPageSize = pageSize ?? _playlistSettings.DefaultPageSize;

            if (pageNumber < 1)
                return BadRequest("pageNumber muss groesser oder gleich 1 sein.");

            if (resolvedPageSize < 1 || resolvedPageSize > _playlistSettings.MaxPageSize)
                return BadRequest($"pageSize muss zwischen 1 und {_playlistSettings.MaxPageSize} liegen.");

            var result = await _playlistService.GetPlaylistEntriesPagedAsync(id, CurrentUser!.Id, pageNumber, resolvedPageSize, HttpContext.RequestAborted);
            return Ok(result);
        }, $"Abrufen der paginierten Eintraege von Playlist {id}");
    }

    /// <summary>
    /// Changes the manual sort order of a single entry of a playlist for the current user.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <param name="entryId">The playlist entry identifier.</param>
    /// <param name="request">The reorder request.</param>
    [HttpPut("{id}/entries/{entryId}/order")]
    public Task<IActionResult> ReorderPlaylistEntry(long id, long entryId, [FromBody] DtoReorderPlaylistEntryRequest request)
    {
        return ExecuteAsync(request, async req =>
        {
            await _playlistService.ReorderPlaylistEntryAsync(id, CurrentUser!.Id, entryId, req.NewSortOrder, HttpContext.RequestAborted);
            return Ok();
        }, $"Umordnen von Eintrag {entryId} in Playlist {id}");
    }

    /// <summary>
    /// Atomically changes the manual sort order of multiple entries of a playlist for the current user.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <param name="request">The batch reorder request.</param>
    [HttpPost("{id}/entries/batch-reorder")]
    public Task<IActionResult> BatchReorderPlaylistEntries(long id, [FromBody] DtoBatchReorderPlaylistEntriesRequest request)
    {
        return ExecuteAsync(request, async req =>
        {
            var reorderOperations = req.ReorderOperations
                .Select(op => (op.EntryId, op.NewSortOrder))
                .ToList();
            var result = await _playlistService.BatchReorderPlaylistEntriesAsync(id, CurrentUser!.Id, reorderOperations, HttpContext.RequestAborted);
            return Ok(result);
        }, $"Batch-Umordnen von Playlist {id}", ex => Conflict(ex.Message));
    }

    /// <summary>
    /// Gets the current maximum manual sort order across the entire playlist (not just a loaded/
    /// virtualized page of it), for the "move to end" quick action.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    [HttpGet("{id}/entries/max-sort-order")]
    public Task<IActionResult> GetMaxSortOrder(long id)
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();
            var result = await _playlistService.GetMaxSortOrderAsync(id, CurrentUser!.Id, HttpContext.RequestAborted);
            return Ok(new DtoMaxSortOrderResult { MaxSortOrder = result });
        }, $"Abrufen der maximalen SortOrder von Playlist {id}");
    }

    /// <summary>
    /// Moves a single entry of a playlist to the true beginning of the manual order (shifting every other
    /// entry's manual sort order up by one first), for the "move to beginning" quick action.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <param name="entryId">The playlist entry identifier.</param>
    [HttpPost("{id}/entries/{entryId}/move-to-beginning")]
    public Task<IActionResult> MoveEntryToBeginning(long id, long entryId)
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();
            var result = await _playlistService.MoveEntryToBeginningAsync(id, CurrentUser!.Id, entryId, HttpContext.RequestAborted);
            return Ok(result);
        }, $"Verschieben von Eintrag {entryId} in Playlist {id} an den Anfang");
    }

    /// <summary>
    /// Moves a single entry of a playlist to an arbitrary target position (shifting every other entry
    /// between the entry's current and target position by one), for drag & drop reordering.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <param name="entryId">The playlist entry identifier.</param>
    /// <param name="request">The move request, carrying the target sort order.</param>
    [HttpPost("{id}/entries/{entryId}/move-between")]
    public Task<IActionResult> MoveEntryBetween(long id, long entryId, [FromBody] DtoReorderPlaylistEntryRequest request)
    {
        return ExecuteAsync(request, async req =>
        {
            await _playlistService.MoveEntryBetweenAsync(id, CurrentUser!.Id, entryId, req.NewSortOrder, HttpContext.RequestAborted);
            return Ok();
        }, $"Verschieben von Eintrag {entryId} in Playlist {id} zwischen Positionen");
    }

    /// <summary>
    /// Changes the sort mode of a playlist for the current user.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <param name="request">The sort mode change request.</param>
    [HttpPatch("{id}/sort-mode")]
    public Task<IActionResult> ChangeSortMode(long id, [FromBody] DtoChangeSortModeRequest request)
    {
        return ExecuteAsync(request, async req =>
        {
            var result = await _playlistService.ChangeSortModeAsync(
                id, CurrentUser!.Id, req.NewSortMode, req.ConfirmLossOfManualOrder, HttpContext.RequestAborted);
            return Ok(result);
        }, $"Aendern des Sortiermodus von Playlist {id}");
    }
}
