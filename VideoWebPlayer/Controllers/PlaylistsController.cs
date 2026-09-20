using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
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
        catch (ContinueWatchingConfirmationRequiredException ex)
        {
            Logger.LogInformation(ex, "Bestaetigung erforderlich (Weiterschauen-Bezug) beim {LogContext}", logContext);
            return Conflict(new DtoRemovePlaylistEntryConflictResponse { IsContinueWatchingConfirmationRequired = true });
        }
        catch (UploadedCoverReplacementConfirmationRequiredException ex)
        {
            Logger.LogInformation(ex, "Bestaetigung erforderlich (hochgeladenes Cover wuerde ersetzt) beim {LogContext}", logContext);
            return Conflict(new DtoRegeneratePlaylistCoverConflictResponse { IsUploadedCoverReplacementConfirmationRequired = true });
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
    /// Gets all playlists for the current user, optionally restricted to those carrying the given genre.
    /// </summary>
    /// <param name="genreId">Optional genre id to restrict results to.</param>
    /// <returns>The playlists as <see cref="IEnumerable{DtoPlaylist}"/>, or an error result.</returns>
    [HttpGet]
    public Task<IActionResult> GetPlaylists([FromQuery] long? genreId = null)
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();
            var result = await _playlistService.GetPlaylistsAsync(CurrentUser!.Id, genreId, HttpContext.RequestAborted);
            return Ok(result);
        }, "Abrufen der Playlists");
    }

    /// <summary>
    /// Gets every playlist currently marked public (Entwicklungsschritt 11), optionally restricted to those
    /// carrying the given genre. Available to every logged-in user (READ).
    /// </summary>
    /// <param name="genreId">Optional genre id to restrict results to.</param>
    /// <returns>The public playlists as <see cref="IEnumerable{DtoPlaylist}"/>, or an error result.</returns>
    [HttpGet("public")]
    public Task<IActionResult> GetPublicPlaylists([FromQuery] long? genreId = null)
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();
            var result = await _playlistService.GetPublicPlaylistsAsync(CurrentUser!.Id, genreId, HttpContext.RequestAborted);
            return Ok(result);
        }, "Abrufen der oeffentlichen Playlists");
    }

    /// <summary>
    /// Sets or clears the "public" flag of a playlist (Entwicklungsschritt 11). Only an administrator who
    /// OWNS the playlist may do so: a regular user, and an administrator who is not the owner, receive 403.
    /// The administrator status is read from the user record in the database (not from the possibly stale
    /// token claim), so a revoked administrator loses the ability at once. Clearing the flag revokes other
    /// users' access immediately.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <param name="request">The request carrying the new flag value.</param>
    /// <returns>The updated playlist as <see cref="DtoPlaylist"/>, or an error result.</returns>
    [HttpPut("{id}/public")]
    public Task<IActionResult> SetPlaylistPublic(long id, [FromBody] DtoSetPlaylistPublicRequest request)
    {
        return ExecuteAsync(request, async req =>
        {
            var result = await _playlistService.SetPlaylistPublicAsync(
                id, CurrentUser!.Id, CurrentUser.IsAdmin, req.IsPublic, HttpContext.RequestAborted);
            return Ok(result);
        }, $"Aendern der Oeffentlich-Kennzeichnung von Playlist {id}");
    }

    /// <summary>
    /// Gets a single playlist for the current user (READ: the owner, or any user while the playlist is public).
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <returns>The playlist as <see cref="DtoPlaylist"/>, or an error result.</returns>
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
    /// <returns>The created playlist as <see cref="DtoPlaylist"/>, or an error result.</returns>
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
    /// <returns>The updated playlist as <see cref="DtoPlaylist"/>, or an error result.</returns>
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
    /// <returns>No content on success, or an error result.</returns>
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
    /// <returns>The result of the add operation as <see cref="DtoPlaylistAddResult"/>, or an error result.</returns>
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
    /// Removes a media entry from a playlist for the current user. If a continue-watching (Weiterschauen)
    /// entry bound to this same playlist still references the entry being removed, the caller must set
    /// <paramref name="confirmContinueWatchingRemoval"/> to <see langword="true"/> - otherwise this returns
    /// 409 Conflict with <see cref="DtoRemovePlaylistEntryConflictResponse"/> instead of removing anything.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <param name="mediaType">The media type of the entry to remove.</param>
    /// <param name="mediaId">The media identifier of the entry to remove.</param>
    /// <param name="confirmContinueWatchingRemoval">
    /// Whether the user confirmed removal despite an existing continue-watching reference. Ignored when no
    /// such reference exists.
    /// </param>
    /// <returns>No content on success, a conflict response if confirmation is required, or another error result.</returns>
    [HttpDelete("{id}/entries/{mediaType}/{mediaId}")]
    public Task<IActionResult> RemoveMediaFromPlaylist(long id, string mediaType, long mediaId, bool confirmContinueWatchingRemoval = false)
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();
            await _playlistService.RemoveMediaFromPlaylistAsync(
                id, CurrentUser!.Id, mediaType, mediaId, confirmContinueWatchingRemoval, HttpContext.RequestAborted);
            return NoContent();
        }, $"Entfernen aus Playlist {id}");
    }

    /// <summary>
    /// Gets all entries of a playlist for the current user.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <returns>The playlist entries, or an error result.</returns>
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
    /// <returns>The requested page of playlist entries, or an error result.</returns>
    [HttpGet("{id}/entries/paged")]
    public Task<IActionResult> GetPlaylistEntriesPaged(long id, int pageNumber = 1, int? pageSize = null)
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();

            var resolvedPageSize = pageSize ?? _playlistSettings.DefaultPageSize;

            if (pageNumber < 1)
                return BadRequest("pageNumber muss größer oder gleich 1 sein.");

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
    /// <returns>An empty success result, or an error result.</returns>
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
    /// <returns>The reordered playlist entries, or an error result.</returns>
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
    /// <returns>The current maximum manual sort order as <see cref="DtoMaxSortOrderResult"/>, or an error result.</returns>
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
    /// <returns>The moved playlist entry, or an error result.</returns>
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
    /// <returns>An empty success result, or an error result.</returns>
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
    /// <returns>The updated playlist as <see cref="DtoPlaylist"/>, or an error result.</returns>
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

    /// <summary>
    /// Manually overrides the genres of a playlist for the current user, replacing every automatically
    /// derived or previously manually assigned genre.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <param name="request">The set-genres request.</param>
    /// <returns>The updated playlist as <see cref="DtoPlaylist"/>, or an error result.</returns>
    [HttpPut("{id}/genres")]
    public Task<IActionResult> SetPlaylistGenres(long id, [FromBody] DtoSetPlaylistGenresRequest request)
    {
        return ExecuteAsync(request, async req =>
        {
            var result = await _playlistService.SetPlaylistGenresAsync(id, CurrentUser!.Id, req.GenreIds, HttpContext.RequestAborted);
            return Ok(result);
        }, $"Ueberschreiben der Genres von Playlist {id}");
    }

    /// <summary>
    /// Clears a previous manual genre override (if any) of a playlist for the current user, immediately
    /// recomputing its genres from its current contents.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <returns>The updated playlist as <see cref="DtoPlaylist"/>, or an error result.</returns>
    [HttpPost("{id}/genres/reset")]
    public Task<IActionResult> ResetPlaylistGenres(long id)
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();
            var result = await _playlistService.ResetPlaylistGenresAsync(id, CurrentUser!.Id, HttpContext.RequestAborted);
            return Ok(result);
        }, $"Zuruecksetzen der Genres von Playlist {id}");
    }

    /// <summary>
    /// Starts playback of a playlist for the current user, at the given entry (or at the first playable
    /// and accessible entry if <paramref name="entryId"/> is omitted).
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <param name="entryId">The entry to start playback at, or <c>null</c> to start at the first playable entry.</param>
    /// <returns>The playback start information as <see cref="DtoPlaylistPlaybackStart"/>, or an error result.</returns>
    [HttpPost("{id}/play")]
    public Task<IActionResult> StartPlaylist(long id, long? entryId = null)
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();
            var result = await _playlistService.StartPlaylistAsync(id, CurrentUser!.Id, entryId, HttpContext.RequestAborted);
            return Ok(result);
        }, $"Starten der Wiedergabe von Playlist {id}");
    }

    /// <summary>
    /// Gets the next playable and accessible entry after <paramref name="currentEntryId"/> for the
    /// current user, for manual "next" navigation within a playlist.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <param name="currentEntryId">The id of the playlist entry currently playing.</param>
    /// <returns>The next playable and accessible entry, or no content if there is none, or an error result.</returns>
    [HttpPost("{id}/play/next")]
    public Task<IActionResult> GetNextPlaylistEntry(long id, long currentEntryId)
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();
            var result = await _playlistService.GetNextPlaylistEntryAsync(id, CurrentUser!.Id, currentEntryId, HttpContext.RequestAborted);
            return result is null ? NoContent() : Ok(result);
        }, $"Ermitteln des naechsten Playlist-Eintrags von Playlist {id}");
    }

    /// <summary>
    /// Gets the previous playable and accessible entry before <paramref name="currentEntryId"/> for the
    /// current user, for manual "previous" navigation within a playlist.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <param name="currentEntryId">The id of the playlist entry currently playing.</param>
    /// <returns>The previous playable and accessible entry, or no content if there is none, or an error result.</returns>
    [HttpPost("{id}/play/previous")]
    public Task<IActionResult> GetPreviousPlaylistEntry(long id, long currentEntryId)
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();
            var result = await _playlistService.GetPreviousPlaylistEntryAsync(id, CurrentUser!.Id, currentEntryId, HttpContext.RequestAborted);
            return result is null ? NoContent() : Ok(result);
        }, $"Ermitteln des vorherigen Playlist-Eintrags von Playlist {id}");
    }

    /// <summary>
    /// Advances playback of a playlist from <paramref name="currentEntryId"/> to the next entry for the
    /// current user, for automatic advance when a title finishes playing.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <param name="currentEntryId">The id of the playlist entry that just finished playing.</param>
    /// <returns>The next playable and accessible entry, or no content if there is none, or an error result.</returns>
    [HttpPost("{id}/play/advance")]
    public Task<IActionResult> AdvancePlaylist(long id, long currentEntryId)
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();
            var result = await _playlistService.AdvancePlaylistAsync(id, CurrentUser!.Id, currentEntryId, HttpContext.RequestAborted);
            return result is null ? NoContent() : Ok(result);
        }, $"Automatisches Weiterschalten von Playlist {id}");
    }

    /// <summary>
    /// Uploads a cover image for a playlist, replacing its current cover (if any). Validation (format,
    /// size, whether the file is a genuine image) happens in <see cref="IPlaylistService.SetPlaylistCoverAsync"/>;
    /// a validation failure surfaces as HTTP 400 with the validator's message as the response body, via the
    /// same generic <see cref="InvalidOperationException"/> mapping every other playlist endpoint already
    /// uses (see <see cref="ExecuteAsync(Func{Task{IActionResult}}, string, Func{InvalidOperationException, IActionResult}?)"/>) -
    /// deliberately not the <c>{ success, message }</c> shape used for the endpoint's success response, to
    /// avoid a second, endpoint-specific error contract next to the controller's established one. No
    /// <c>[RequestSizeLimit]</c> attribute is used: the maximum size is configurable at runtime
    /// (<see cref="PlaylistSettings.MaxCoverImageSizeBytes"/>), so it is enforced by the validator instead
    /// of a compile-time attribute value that could drift out of sync with it.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <param name="file">The uploaded image file.</param>
    /// <returns>The upload result as <see cref="DtoPlaylistCoverResult"/>, or an error result.</returns>
    /// <remarks>
    /// Checks <paramref name="file"/>'s reported <see cref="IFormFile.Length"/> against
    /// <see cref="PlaylistSettings.MaxCoverImageSizeBytes"/> <b>before</b> touching its stream at all, so an
    /// oversized upload is rejected without ever being buffered into memory - only once that check passes
    /// is the (now known-bounded) content copied into a byte array for
    /// <see cref="IPlaylistService.SetPlaylistCoverAsync"/>, which re-validates via
    /// <see cref="PlaylistCover.PlaylistCoverValidator"/> regardless.
    /// </remarks>
    [HttpPost("{id}/cover/upload")]
    public Task<IActionResult> UploadPlaylistCover(long id, IFormFile file)
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();

            if (file is null || file.Length == 0)
                return BadRequest("Es wurde keine Datei ausgewählt.");

            if (file.Length > _playlistSettings.MaxCoverImageSizeBytes)
                return BadRequest($"Die Datei ist zu groß. Maximal erlaubt sind {_playlistSettings.MaxCoverImageSizeBytes} Bytes.");

            byte[] fileBytes;
            await using (var stream = file.OpenReadStream())
            {
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream, HttpContext.RequestAborted);
                fileBytes = memoryStream.ToArray();
            }

            var pictureId = await _playlistService.SetPlaylistCoverAsync(id, CurrentUser!.Id, fileBytes, file.ContentType, HttpContext.RequestAborted);
            return Ok(new DtoPlaylistCoverResult { Success = true, Message = "Bild erfolgreich hochgeladen.", PictureId = pictureId });
        }, $"Hochladen des Covers von Playlist {id}");
    }

    /// <summary>
    /// Regenerates a playlist's cover as a collage of its current contents (the "Neu erzeugen" UI action).
    /// If the current cover was uploaded by the user, the caller must set
    /// <paramref name="confirmReplaceUploadedCover"/> to <see langword="true"/> - otherwise this returns
    /// 409 Conflict with <see cref="DtoRegeneratePlaylistCoverConflictResponse"/> instead of replacing
    /// anything ("Ein hochgeladenes Bild hat immer Vorrang").
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <param name="confirmReplaceUploadedCover">
    /// Whether the user confirmed replacing an uploaded cover image. Ignored when the current cover is not
    /// user-uploaded.
    /// </param>
    /// <returns>The regeneration result as <see cref="DtoPlaylistCoverResult"/>, a conflict response if confirmation is required, or another error result.</returns>
    [HttpPost("{id}/cover/regenerate")]
    public Task<IActionResult> RegeneratePlaylistCover(long id, bool confirmReplaceUploadedCover = false)
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();
            var pictureId = await _playlistService.GeneratePlaylistCoverAsync(id, CurrentUser!.Id, confirmReplaceUploadedCover, HttpContext.RequestAborted);
            return pictureId is null
                ? Ok(new DtoPlaylistCoverResult { Success = false, Message = "Keine Bilder verfügbar." })
                : Ok(new DtoPlaylistCoverResult { Success = true, Message = "Cover neu erzeugt.", PictureId = pictureId });
        }, $"Neuerzeugen des Covers von Playlist {id}");
    }

    /// <summary>
    /// Gets a playlist's cover image (uploaded or generated). READ access (Entwicklungsschritt 11): the owner,
    /// or any logged-in user while the playlist is public; the cover of a private playlist of somebody else is
    /// refused with 403 (a generated cover is a collage of the playlist's contents). Mutating the cover
    /// always requires ownership.
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <returns>The cover image content, or 404 Not Found if the playlist has no cover set.</returns>
    [HttpGet("{id}/cover")]
    public Task<IActionResult> GetPlaylistCover(long id)
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();

            var picture = await _playlistService.GetPlaylistCoverAsync(id, CurrentUser!.Id, HttpContext.RequestAborted);
            if (picture is null || picture.Data is null || picture.Data.Length == 0)
                return NotFound();

            return File(picture.Data, picture.ContentType ?? "image/jpeg");
        }, $"Abrufen des Covers von Playlist {id}");
    }

    /// <summary>
    /// Deletes a playlist's cover (if any).
    /// </summary>
    /// <param name="id">The playlist identifier.</param>
    /// <returns>The deletion result as <see cref="DtoPlaylistCoverResult"/>, or an error result.</returns>
    [HttpDelete("{id}/cover")]
    public Task<IActionResult> DeletePlaylistCover(long id)
    {
        return ExecuteAsync(async () =>
        {
            CheckLogedIn();
            await _playlistService.DeletePlaylistCoverAsync(id, CurrentUser!.Id, HttpContext.RequestAborted);
            return Ok(new DtoPlaylistCoverResult { Success = true });
        }, $"Loeschen des Covers von Playlist {id}");
    }
}
