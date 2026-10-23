using System;
using System.Collections.Generic;
using System.IO;

namespace VideoWebPlayer.Utils;

/// <summary>
/// Shared list of file extensions that are treated as playable video files.
/// </summary>
public static class VideoFileExtensions
{
    /// <summary>
    /// Gets the recognized video file extensions (with leading dot, lower case).
    /// </summary>
    public static IReadOnlyCollection<string> All { get; } = new[] { ".mp4", ".avi", ".mkv", ".mpeg" };

    /// <summary>
    /// Determines whether the given file path or name has a recognized video file extension.
    /// </summary>
    /// <param name="path">The file path or file name to check.</param>
    /// <returns><c>true</c> when the extension is a recognized video extension; otherwise <c>false</c>.</returns>
    public static bool IsVideoFileName(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return All.Contains(Path.GetExtension(path));
    }
}
