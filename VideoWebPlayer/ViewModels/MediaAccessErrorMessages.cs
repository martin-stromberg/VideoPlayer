using System.Net;

namespace VideoWebPlayer.ViewModels;

/// <summary>
/// Turns the final error answers of the media and playlist endpoints — 403 Forbidden (signed in, but the
/// title is not unlocked for this user) and 404 Not Found (the title does not exist any more or has no
/// video file) — into the message the user reads. Both are final: nothing is retried and no sign-in is
/// offered, because neither is a session problem. Shared by the playlist detail page and the player so
/// both name the same cause with the same words.
/// </summary>
internal static class MediaAccessErrorMessages
{
    /// <summary>Message shown when the server refused the title with 403 Forbidden.</summary>
    public const string Forbidden = "Sie haben keinen Zugriff auf diesen Titel.";

    /// <summary>Message shown when the server answered 404 Not Found for the title.</summary>
    public const string NotFound = "Dieser Titel existiert nicht oder hat keine Videodatei.";

    /// <summary>
    /// Message for a video the browser could not play. Used where only the fact of the failure is known
    /// and not its cause: the <c>error</c> event of the <c>&lt;video&gt;</c> element fires for every
    /// loading problem — a refused or missing stream just as much as a format the browser cannot decode
    /// (the stream endpoint also serves Matroska, AVI and MPEG) or a broken connection. The wording
    /// names the causes shown elsewhere by their own messages (<see cref="Forbidden"/>,
    /// <see cref="NotFound"/>) rather than asserting one of them; a 403 answered directly on the stream
    /// URL still falls back to this generic text, since the video element does not expose the status code.
    /// </summary>
    public const string PlaybackFailed =
        "Der Titel konnte nicht abgespielt werden: Er existiert nicht, hat keine Videodatei, oder der Browser kann das Format nicht wiedergeben.";

    /// <summary>
    /// Returns the user-facing message for <paramref name="exception"/> if it is a final 403 or 404
    /// answer, otherwise <see langword="null"/> so the caller keeps its own generic error text.
    /// </summary>
    /// <param name="exception">The exception a client call failed with.</param>
    /// <returns>The message to display, or <see langword="null"/> if the exception is not a 403/404 answer.</returns>
    public static string? Describe(Exception exception)
        => exception is HttpRequestException { StatusCode: { } status }
            ? status switch
            {
                HttpStatusCode.Forbidden => Forbidden,
                HttpStatusCode.NotFound => NotFound,
                _ => null
            }
            : null;
}
