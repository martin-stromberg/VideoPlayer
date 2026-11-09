namespace VideoWebPlayer.Controllers;

/// <summary>
/// Maps a media file name to the content type used by the stream and download endpoints.
/// Shared by <see cref="ItemsController"/> and <see cref="InternalMediaStreamController"/>.
/// </summary>
internal static class MediaFileContentTypes
{
    /// <summary>
    /// Returns the content type for the media file with the given name, derived from its
    /// extension. Unknown extensions are served as <c>application/octet-stream</c>.
    /// </summary>
    /// <param name="fileName">The media file name including its extension.</param>
    /// <returns>The MIME content type.</returns>
    public static string ForFileName(string fileName)
        => Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".mp4" => "video/mp4",
            ".mkv" => "video/x-matroska",
            ".avi" => "video/x-msvideo",
            ".mpeg" => "video/mpeg",
            _ => "application/octet-stream"
        };
}
