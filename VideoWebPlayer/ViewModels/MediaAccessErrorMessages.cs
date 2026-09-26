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
